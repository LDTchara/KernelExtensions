using Hacknet;
using Hacknet.Effects;
using Hacknet.Extensions;
using KernelExtensions.Configs;
using KernelExtensions.Utilities;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Media;
using NVorbis;

namespace KernelExtensions.Modules;

/// <summary>
/// 自定义结局 Module —— 继承 EndingSequenceModule，由 OS 原生驱动 Update/Draw。
///
/// 直接设置 os.endingSequence = new CustomEndingModule(os.fullscreen, os, config) 即可生效。
/// OS 会自动处理：
///   - endingSequence.Update(num)     每帧调用
///   - PostProcessor + SpriteBatch + drawScanlines 的包裹
///   无需任何 Event 或 HarmonyPatch。
///
/// 配置见 Configs/EndingConfig（独立 &lt;Ending&gt; XML 或旧属性用法）。
/// 资源文件位于扩展根目录（路径可配，默认 Docs/）：
///   - SpeechFile    语音（.wav 走 SoundEffect；.ogg 走 NVorbis 解码 → PCM SoundEffect，
///                    波形由解码采样自绘，不再依赖原版 WaveformRenderer 反射）
///   - Speech.txt    演讲文本（#=1s, %=0.5s）
///   - CreditsData.txt 报幕名单（%/^/$ 前缀）
///
/// SpeechTime 语义（EndingConfig.SpeechTime）：
///   -1/缺省 —— 有语音跟随音频时长；无语音静默 30s 兜底（Warn）
///    0      —— 跳过演讲阶段，加载后直接进报幕
///    &gt;0    —— 演讲上限 N 秒：音频先播完则提前进报幕，N 先到则截断
/// </summary>
public class CustomEndingModule : EndingSequenceModule
{
    // ========================================================================
    //  配置与展示字段（由 EndingConfig 注入；保留 public 供动态覆盖）
    // ========================================================================

    public string Titletext = "Hacknet";
    public string endingText = "Thanks For Playing";
    public string onCreditMusic = "";   // 报幕阶段音乐，空=原版 Music\Bit(Ending)
    public string afterMusic = "";      // 回游戏后音乐，空=原版 Music\Bit(Ending)

    // ========================================================================
    //  结束回调 — 报幕完成后触发下一个 Action（实例字段，Action 注入）
    // ========================================================================

    internal Action OnCompleteCallback;

    // ========================================================================
    //  演讲阶段
    // ========================================================================

    private new const float SpeechTextHashDelay = 1.0f;
    private new const float SpeechTextPercDelay = 0.5f;
    private new const float SpeechTextCharDelay = 0.05f;

    /// <summary>演讲计时模式（由 SpeechTime 决定）。</summary>
    private enum SpeechTiming { FollowAudio, FixedLimit, SkipSpeech }
    private SpeechTiming timingMode = SpeechTiming.FollowAudio;

    private new SoundEffect speech;
    private SoundEffectInstance speechInstance;
    private bool hasVoice = false;
    private bool noSpeechWarned = false;
    private float speechLimit = 30f;    // 演讲阶段时长上限（秒）；Skip 模式为 0
    private float voiceDuration = 0f;   // 语音时长（秒）

    private string bitSpeechText;
    private int speechTextIndex = 0;
    private float speechTextTimer = 0f;

    // ---- 波形可视化（自实现，wav/ogg 统一）----
    private float[] waveSamples;        // 单声道归一化采样（-1~1）
    private int waveSampleRate = 44100;

    // ========================================================================
    //  报幕阶段
    // ========================================================================

    private string[] creditsData;
    private new float creditsScroll;
    private float hacknetTitleFreezeTime = 10f;
    private new float creditsPixelsScrollPerSecond = 65f;
    private bool endingTextReachedCenter = false;
    private float endingPauseTimer = 0f;
    private const float EndingPauseDuration = 5f;
    private const float EndingTextBottomOffset = 350f;

    // ========================================================================
    //  通用状态（与基类 private 同名互不干扰，去 new）
    // ========================================================================

    private new float elapsedTime = 0f;
    private bool isInCredits = false;
    private bool resourcesLoaded = false;

    // ========================================================================
    //  ██████  构造函数  ██████
    // ========================================================================

    public CustomEndingModule(Rectangle location, OS operatingSystem, EndingConfig config)
        : base(location, operatingSystem)
    {
        // 基类 EndingSequenceModule(location, os) 已设置 spriteBatch/os/bounds 并加载原版音效/文本
        Titletext = config.Title;
        endingText = config.EndingText;
        onCreditMusic = config.OnCreditMusic;
        afterMusic = config.AfterMusic;
        SpeechFile = config.SpeechFile;
        TextFile = config.TextFile;
        CreditsFile = config.CreditsFile;
        ConfigureSpeechTiming(config.SpeechTime);
    }

    /// <summary>演讲计时配置（路径可配，供 StartEnding 阶段前决定跳/跟/限）。</summary>
    public string SpeechFile = "Docs/EndingSpeech.wav";
    public string TextFile = "Docs/Speech.txt";
    public string CreditsFile = "Docs/CreditsData.txt";

    // ========================================================================
    //  ██████  入口 — StartEnding  ██████
    // ========================================================================

    /// <summary>触发自定义结局。调用后 OS 原生驱动 Update/Draw。</summary>
    public void StartEnding()
    {
        IsActive = true;
        isInCredits = false;
        elapsedTime = 0f;
        speechTextIndex = 0;
        speechTextTimer = 0f;
        creditsScroll = os.fullscreen.Height / 2;
        endingTextReachedCenter = false;
        endingPauseTimer = 0f;
        resourcesLoaded = false;

        // ---- 关键：设 canRunContent=false 让 OS 走入 else 分支调用 Update/Draw ----
        os.canRunContent = false;

        KELog.Info($"[CustomEndingModule] Started. timing={timingMode} canRunContent=false");
    }

    private void ConfigureSpeechTiming(float speechTime)
    {
        if (speechTime == 0f) timingMode = SpeechTiming.SkipSpeech;
        else if (speechTime < 0f) timingMode = SpeechTiming.FollowAudio;
        else timingMode = SpeechTiming.FixedLimit;
        if (timingMode == SpeechTiming.FixedLimit) speechLimit = speechTime;
        else speechLimit = 30f; // FollowAudio 无语音时的兜底（加载后可能被 Warn 覆盖）
    }

    // ========================================================================
    //  ██████  Update — 由 OS 每帧自动调用  ██████
    // ========================================================================

    public override void Update(float t)
    {
        if (!IsActive) return;

        if (!resourcesLoaded) { LoadResources(); resourcesLoaded = true; }

        if (!isInCredits) UpdateSpeech(t);
        else UpdateCredits(t);
    }

    // ========================================================================
    //  ██████  Draw — 由 OS 在 PostProcessor + SpriteBatch 包裹内自动调用  ██████
    // ========================================================================

    public override void Draw(float t)
    {
        if (!IsActive) return;

        // 全屏黑色背景
        spriteBatch.Draw(Utils.white, os.fullscreen, Color.Black);

        if (!isInCredits) DrawSpeech();
        else DrawCredits();
    }

    // ========================================================================
    // ██  资源加载（文本/报幕/语音——wav 与 ogg 分流）██
    // ========================================================================

    private void LoadResources()
    {
        string ext = ExtensionLoader.ActiveExtensionInfo.GetFullFolderPath();

        // ---- 演讲文本 ----
        string speechPath = Path.Combine(ext, TextFile);
        if (File.Exists(speechPath))
        {
            bitSpeechText = File.ReadAllText(speechPath);
            KELog.Info($"[CustomEndingModule] Speech.txt loaded ({bitSpeechText.Length} chars).");
        }
        else { bitSpeechText = ""; }

        // ---- 报幕数据 ----
        string creditsPath = Path.Combine(ext, CreditsFile);
        if (File.Exists(creditsPath))
        {
            creditsData = File.ReadAllText(creditsPath)
                .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            KELog.Info($"[CustomEndingModule] CreditsData.txt loaded ({creditsData.Length} lines).");
        }
        else { creditsData = Array.Empty<string>(); }

        // ---- 语音（.wav / .ogg 分流）----
        string voicePath = Path.Combine(ext, SpeechFile);
        if (File.Exists(voicePath))
        {
            try
            {
                bool isOgg = voicePath.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase);
                if (isOgg) LoadVoiceOgg(voicePath);
                else LoadVoiceWav(voicePath);
            }
            catch (Exception ex)
            {
                KELog.Warn($"[CustomEndingModule] Failed to load voice ({SpeechFile}): {ex.Message}");
                hasVoice = false;
                waveSamples = null;
            }
        }
        else
        {
            KELog.Info($"[CustomEndingModule] voice NOT found: {SpeechFile}");
        }

        // ---- 演讲计时终值 ----
        if (hasVoice)
        {
            voiceDuration = (float)(speech?.Duration.TotalSeconds ?? 0.0);
            if (timingMode == SpeechTiming.FollowAudio) speechLimit = voiceDuration;
            else if (timingMode == SpeechTiming.FixedLimit) speechLimit = Math.Min(speechLimit, voiceDuration);
            // SkipSpeech：speechLimit 保持 0
            KELog.Info($"[CustomEndingModule] voice loaded ({voiceDuration:F2}s, timing={timingMode}, limit={speechLimit:F2}s).");
        }
        else
        {
            if (timingMode == SpeechTiming.FollowAudio && !noSpeechWarned)
            {
                noSpeechWarned = true;
                KELog.Warn($"[CustomEndingModule] no voice & SpeechTime<0 — falling back to 30s silent speech.");
            }
            // Skip 模式 limit=0；FixedLimit 用设定值；FollowAudio 用 30s 兜底
            if (timingMode == SpeechTiming.SkipSpeech) speechLimit = 0f;
            else if (timingMode == SpeechTiming.FollowAudio) speechLimit = 30f;
        }

        try { MusicManager.stop(); } catch { }
    }

    /// <summary>wav：SoundEffect.FromStream + 自解析采样（16-bit PCM）供波形。</summary>
    private void LoadVoiceWav(string wavPath)
    {
        using (var fs = new FileStream(wavPath, FileMode.Open, FileAccess.Read))
            speech = SoundEffect.FromStream(fs);
        speechInstance = speech.CreateInstance();
        speechInstance.IsLooped = false;
        waveSamples = ParseWavSamples(wavPath);
        hasVoice = true;
    }

    /// <summary>ogg：NVorbis 解码 → PCM SoundEffect（public 构造）+ 采样供波形。</summary>
    private void LoadVoiceOgg(string oggPath)
    {
        using var reader = new VorbisReader(oggPath);
        int channels = reader.Channels;
        int sampleRate = reader.SampleRate;
        int total = (int)reader.TotalSamples; // 每声道采样数
        if (total <= 0) throw new InvalidDataException("empty ogg");

        var interleaved = new float[total * channels];
        int read = 0;
        while (read < interleaved.Length)
        {
            int n = reader.ReadSamples(interleaved, read, interleaved.Length - read);
            if (n <= 0) break;
            read += n;
        }
        if (read <= 0) throw new InvalidDataException("no samples decoded");

        // 交错 float → mono 采样 + 16-bit PCM（交错）
        var mono = new float[total];
        var pcm = new byte[total * channels * 2];
        int p = 0;
        for (int i = 0; i < total; i++)
        {
            float l = interleaved[i * channels];
            float r = channels > 1 ? interleaved[i * channels + 1] : l;
            mono[i] = (l + r) * 0.5f;
            for (int c = 0; c < channels; c++)
            {
                short s = (short)Math.Max(-32768, Math.Min(32767, (int)(interleaved[i * channels + c] * 32767f)));
                pcm[p++] = (byte)(s & 0xFF);
                pcm[p++] = (byte)((s >> 8) & 0xFF);
            }
        }

        speech = new SoundEffect(pcm, sampleRate, (AudioChannels)channels);
        speechInstance = speech.CreateInstance();
        speechInstance.IsLooped = false;
        waveSamples = mono;
        waveSampleRate = sampleRate;
        hasVoice = true;
    }

    /// <summary>解析 16-bit PCM wav → 单声道归一化采样（自实现，替代 internal AudioUtils）。</summary>
    private static float[] ParseWavSamples(string path)
    {
        byte[] data = File.ReadAllBytes(path);
        // 跳 RIFF chunk 找 "data"（AudioUtils.openWav 同款遍历）
        int i = 12;
        while (i + 8 <= data.Length &&
               !(data[i] == 100 && data[i + 1] == 97 && data[i + 2] == 116 && data[i + 3] == 97)) // "data"
        {
            int chunkSize = data[i + 4] | (data[i + 5] << 8) | (data[i + 6] << 16) | (data[i + 7] << 24);
            i += 8 + chunkSize;
        }
        int channels = data[22]; // 字节 22 = 声道数（16-bit PCM 假设）
        i += 8; // 跳过 "data" + 大小
        int samplesPerChannel = (data.Length - i) / 2 / Math.Max(1, channels);
        var mono = new float[samplesPerChannel];
        int idx = 0;
        while (i + 1 < data.Length && idx < samplesPerChannel)
        {
            short s = (short)(data[i] | (data[i + 1] << 8));
            mono[idx++] = s / 32768f;
            i += 2 * channels; // 取每帧左声道（或单声道）
        }
        return mono;
    }

    // ========================================================================
    // ██  演讲 Update/Draw  ██
    // ========================================================================

    private void UpdateSpeech(float t)
    {
        // SkipSpeech（SpeechTime=0）：加载完成即进报幕
        if (timingMode == SpeechTiming.SkipSpeech)
        {
            RollCredits();
            return;
        }

        if (speechInstance != null && !hasVoiceStarted)
        {
            speechInstance.Play();
            hasVoiceStarted = true;
        }

        elapsedTime += t;

        // 完成判定：达到 limit（音频自然播完：语音 Stopped 且已开始；限时：elapsed >= limit）
        bool voiceEnded = hasVoice && hasVoiceStarted && speechInstance != null
                          && speechInstance.State == SoundState.Stopped;
        if (voiceEnded || elapsedTime >= speechLimit)
        {
            if (speechInstance != null) try { speechInstance.Stop(); } catch { }
            RollCredits();
            return;
        }

        AdvanceSpeechText(t);
    }

    private bool hasVoiceStarted = false;

    private void AdvanceSpeechText(float t)
    {
        if (speechTextIndex >= bitSpeechText.Length) return;
        speechTextTimer += t;
        char c = bitSpeechText[speechTextIndex];
        if (c == '#')
        {
            if (speechTextTimer >= SpeechTextHashDelay)
            { speechTextTimer -= SpeechTextHashDelay; speechTextIndex++; }
        }
        else if (c == '%')
        {
            if (speechTextTimer >= SpeechTextPercDelay)
            { speechTextTimer -= SpeechTextPercDelay; speechTextIndex++; }
        }
        else
        {
            if (speechTextTimer >= SpeechTextCharDelay)
            { speechTextTimer -= SpeechTextCharDelay; speechTextIndex++; }
        }
    }

    private void DrawSpeech()
    {
        int w = os.fullscreen.Width;
        int h = os.fullscreen.Height;

        // 自绘波形（原版 WaveformRenderer 语义，wav/ogg 统一）
        if (hasVoice && waveSamples != null && waveSamples.Length > 0)
        {
            var bounds = new Rectangle(0, os.fullscreen.Height / 2 - h / 2, w, h);
            RenderWaveform(elapsedTime, Math.Max(0.001, voiceDuration), spriteBatch, bounds);
        }

        if (!string.IsNullOrEmpty(bitSpeechText) && speechTextIndex > 0)
        {
            string[] lines = bitSpeechText.Substring(0, speechTextIndex)
                .Replace("#", "").Replace("%", "")
                .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            var pos = new Vector2(os.fullscreen.X + 150f,
                os.fullscreen.Y + os.fullscreen.Height - 100f);
            float alpha = 1f;
            int idx = lines.Length - 1;
            int cnt = 0;
            while (idx >= 0 && cnt < 5)
            {
                spriteBatch.DrawString(GuiData.smallfont, lines[idx], pos,
                    Utils.AddativeWhite * alpha);
                alpha *= 0.6f;
                pos.Y -= GuiData.ActiveFontConfig.tinyFontCharHeight + 8f;
                idx--; cnt++;
            }
        }
    }

    /// <summary>自绘波形（语义同原版 WaveformRenderer：当前时刻 1/100 秒采样段逐采样画竖条）。</summary>
    private void RenderWaveform(double time, double totalTime, SpriteBatch sb, Rectangle bounds)
    {
        if (waveSamples == null || waveSamples.Length == 0 || totalTime <= 0) return;
        double t = time % totalTime;
        int samplesPerSecond = Math.Max(1, (int)(waveSamples.Length / totalTime));
        int blockSamples = Math.Max(1, samplesPerSecond / 100);
        int start = (int)(t * samplesPerSecond);
        int end = Math.Min(waveSamples.Length - 1, start + blockSamples);
        int count = Math.Max(1, end - start);
        float step = (float)bounds.Width / count;
        for (int i = 0; i < count; i++)
        {
            float v = waveSamples[Math.Min(waveSamples.Length - 1, start + i)];
            if (v == 0f) continue;
            float h = v * bounds.Height;
            var rect = new Rectangle((int)(bounds.X + i * step),
                bounds.Y + bounds.Height / 2 - (int)(h / 2f), Math.Max(1, (int)step), Math.Max(1, (int)h));
            sb.Draw(Utils.white, rect, Color.White * 0.4f);
        }
    }

    // ========================================================================
    // ██  报幕 Update/Draw  ██
    // ========================================================================

    private void UpdateCredits(float t)
    {
        if (endingTextReachedCenter)
        {
            // 停止滚动，elapsedTime 继续走以保持 _ 闪烁，等待暂停时长后结束
            elapsedTime += t;
            endingPauseTimer += t;
            if (endingPauseTimer >= EndingPauseDuration)
                CompleteAndReturnToMenu();
            return;
        }

        elapsedTime += t;
        if (elapsedTime > hacknetTitleFreezeTime)
        {
            float speed = Math.Min(1f, (elapsedTime - hacknetTitleFreezeTime) / 8f);
            creditsScroll -= t * creditsPixelsScrollPerSecond * speed;
        }
    }

    private new void DrawCredits()
    {
        float y = creditsScroll;
        var vector = new Vector2(0f, y);

        // 标题矩形：基于文字实际尺寸 + 边距，避免渲染目标超限
        string titleStr = Titletext ?? "HACKNET";
        var titleSz = GuiData.titlefont.MeasureString(titleStr);
        int rw = (int)titleSz.X + 80;
        int rh = (int)(titleSz.Y * 2.5f);
        var dest = new Rectangle(os.fullscreen.Width / 2 - rw / 2,
            (int)(vector.Y - rh / 2f), rw, rh);
        var bg = new Rectangle(os.fullscreen.X, dest.Y + 65,
            os.fullscreen.Width, dest.Height - 135);

        if (elapsedTime >= 1.71f)
        {
            float fa = 0.2f + Utils.randm(0.05f);
            spriteBatch.Draw(Utils.white, bg,
                Color.Lerp(Utils.AddativeRed, Color.Red, fa) * 0.5f);
            FlickeringTextEffect.DrawLinedFlickeringText(
                dest, titleStr, 16f, 0.4f,
                GuiData.titlefont, os, Color.White, 5);
        }

        vector.Y += os.fullscreen.Height / 2f;
        for (int i = 0; i < creditsData.Length; i++)
        {
            float lh = 20f;  // 默认行高
            string raw = creditsData[i];

            if (!string.IsNullOrEmpty(raw))
            {
                string txt = raw;
                var font = GuiData.font;
                var col = Color.White * 0.7f;

                if (raw.StartsWith("^")) { txt = raw.Substring(1); col = Color.Gray * 0.6f; }
                else if (raw.StartsWith("%")) { txt = raw.Substring(1); font = GuiData.titlefont; lh = 90f; }
                else if (raw.StartsWith("$")) { txt = raw.Substring(1); col = Color.Gray * 0.6f; font = GuiData.smallfont; }

                var sz = font.MeasureString(txt);
                var dp = vector + new Vector2(os.fullscreen.Width / 2f - sz.X / 2f, 0f);
                txt = Utils.CleanStringToRenderable(txt);
                spriteBatch.DrawString(font, txt, dp, col);
                vector.Y += lh;  // 第 1 次：内容行距
            }

            vector.Y += lh;  // 第 2 次：空行距（原版行为，解决 ^/$ 行重叠）
        }

        // ---- 结尾提示行：">xxxx_" 带闪烁光标 ----
        // 放在最后一行下方很远处，随滚动逐渐上移
        if (!string.IsNullOrEmpty(endingText))
        {
            float endY = vector.Y + EndingTextBottomOffset;
            string cursor = (elapsedTime % 1.0f > 0.5f) ? "_" : " ";
            string prompt = "> " + endingText + cursor;
            var endFont = GuiData.font;
            var endSz = endFont.MeasureString(prompt);
            var endPos = new Vector2(os.fullscreen.Width / 2f - endSz.X / 2f, endY);
            spriteBatch.DrawString(endFont, prompt, endPos, Color.White);

            // 当结尾文字到达屏幕中央 → 通知 UpdateCredits 停止滚动
            if (!endingTextReachedCenter && endY <= os.fullscreen.Height / 2f)
                endingTextReachedCenter = true;
        }
        else if (vector.Y < -500f)
        {
            // 无结尾文字时使用原版结束逻辑
            CompleteAndReturnToMenu();
        }
    }

    // ========================================================================
    // ██  过渡 + 结束  ██
    // ========================================================================

    private new void RollCredits()
    {
        if (os.TraceDangerSequence != null && os.TraceDangerSequence.IsActive)
            try { os.TraceDangerSequence.CancelTraceDangerSequence(); } catch { }

        isInCredits = true;
        if (speechInstance != null) try { speechInstance.Stop(); } catch { }

        Settings.soundDisabled = false;
        elapsedTime = 0f;

        os.delayer.Post(ActionDelayer.Wait(1.0), () =>
        {
            string creditSong = string.IsNullOrEmpty(onCreditMusic) ? "Music\\Bit(Ending)" : onCreditMusic;
            try { MusicManager.playSongImmediatley(creditSong); MediaPlayer.IsRepeating = false; }
            catch { }
        });

        KELog.Info("[CustomEndingModule] RollCredits -> Credits.");
    }

    private new void CompleteAndReturnToMenu()
    {
        try { Programs.disconnect(Array.Empty<string>(), os); } catch { }
        try
        {
            var heart = Programs.getComputer(os, "porthackHeart");
            if (heart != null)
            {
                os.netMap.visibleNodes.Remove(os.netMap.nodes.IndexOf(heart));
                heart.disabled = true; heart.daemons.Clear();
                heart.ip = NetworkMap.generateRandomIP();
            }
        }
        catch { }
        os.terminal.inputLocked = false; os.ram.inputLocked = false;
        os.netMap.inputLocked = false; os.DisableTopBarButtons = false;
        os.canRunContent = true;

        IsActive = false;

        try { os.threadedSaveExecute(); } catch { }
        MediaPlayer.IsRepeating = true;
        string afterSong = string.IsNullOrEmpty(afterMusic) ? "Music\\Bit(Ending)" : afterMusic;
        try { MusicManager.playSongImmediatley(afterSong); } catch { }

        try { OnCompleteCallback?.Invoke(); }
        catch (Exception ex) { KELog.Warn($"[CustomEndingModule] OnCompleteCallback error: {ex.Message}"); }

        KELog.Info("[CustomEndingModule] Complete.");
    }
}
