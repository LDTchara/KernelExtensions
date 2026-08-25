using System;
using System.IO;
using System.Reflection;
using Hacknet;
using Hacknet.Effects;
using Hacknet.Extensions;
using KernelExtensions.Utilities;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Media;

namespace KernelExtensions.Modules;

/// <summary>
/// 自定义结局 Module —— 继承 EndingSequenceModule，由 OS 原生驱动 Update/Draw。
///
/// 直接设置 os.endingSequence = new CustomEndingModule(os.fullscreen, os) 即可生效。
/// OS 会自动处理：
///   - endingSequence.Update(num)     每帧调用
///   - PostProcessor + SpriteBatch + drawScanlines 的包裹
///   无需任何 Event 或 HarmonyPatch。
///
/// 资源文件位于扩展根目录（路径可配，默认 Docs/）：
///   - EndingSpeech.wav    语音文件（可选）
///   - Speech.txt          演讲文本（#=1s, %=0.5s）
///   - CreditsData.txt     报幕名单（%/^/$ 前缀）
/// </summary>
public class CustomEndingModule : EndingSequenceModule
{
    // ========================================================================
    //  公开标志
    // ========================================================================

    /// <summary>表示当前正在播放自定义扩展结局。</summary>
    public static bool IsCustomExtensionEnding = false;
    public string Titletext;
    public string endingText = "";
    public string onCreditMusic = "";   // 报幕阶段音乐，空=原版 Music\Bit(Ending)
    public string afterMusic = "";      // 回游戏后音乐，空=原版 Music\Bit(Ending)
    // ========================================================================
    //  通用状态
    // ========================================================================

    /// <summary>资源文件路径（相对扩展根，可配；默认 Docs/）。</summary>
    public string SpeechFile = "Docs/EndingSpeech.wav";
    public string TextFile = "Docs/Speech.txt";
    public string CreditsFile = "Docs/CreditsData.txt";

    private bool resourcesLoaded = false;
    private MethodInfo drawScanlinesMethod;

    // ========================================================================
    //  结束回调 — 报幕完成后触发下一个 Action
    // ========================================================================

    internal Action OnCompleteCallback;

    // ========================================================================
    //  演讲阶段
    // ========================================================================

    private new const float SpeechTextHashDelay = 1.0f;
    private new const float SpeechTextPercDelay = 0.5f;
    private new const float SpeechTextCharDelay = 0.05f;
    private float speechDurationFallback = 30f;

    private new SoundEffect speech;
    private SoundEffectInstance speechInstance;
    private bool noSpeechFile = false;
    private string bitSpeechText;
    private int speechTextIndex = 0;
    private float speechTextTimer = 0f;

    // 波形可视化（反射）
    private new object waveRender;
    private Type waveRenderType;
    private MethodInfo renderWaveformMethod;

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
    //  ██████  构造函数  ██████
    // ========================================================================

    public CustomEndingModule(Rectangle location, OS operatingSystem)
        : base(location, operatingSystem)
    {
        // 基类 EndingSequenceModule(location, os) 已经:
        //   1. 调用 Module(location, os) → 设置 spriteBatch、os、bounds
        //   2. 加载 spinUpEffect、traceDownEffect、BitSpeechText（原版资源）
        // 我们自己的资源在首次 Update 时懒加载
    }

    // ========================================================================
    //  ██████  入口 — StartEnding  ██████
    // ========================================================================

    /// <summary>触发自定义结局。调用后 OS 原生驱动 Update/Draw。</summary>
    public void StartEnding(float speechDurationFallback = 30f)
    {
        this.speechDurationFallback = speechDurationFallback;

        IsCustomExtensionEnding = true;
        IsActive = true;
        isInCredits = false;
        elapsedTime = 0f;
        speechTextIndex = 0;
        speechTextTimer = 0f;
        creditsScroll = os.fullscreen.Height / 2;
        endingTextReachedCenter = false;
        endingPauseTimer = 0f;
        resourcesLoaded = false;
        noSpeechFile = false;

        InitWaveformRendererReflection();
        InitDrawScanlinesReflection();

        // ---- 关键：设 canRunContent=false 让 OS 走入 else 分支调用 Update/Draw ----
        os.canRunContent = false;

        KELog.Info($"[CustomEndingModule] Started. DurationFallback={speechDurationFallback}s canRunContent=false");
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
    // ██  资源加载  ██
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

        // ---- 语音 WAV ----
        string wavPath = Path.Combine(ext, SpeechFile);
        if (File.Exists(wavPath))
        {
            try
            {
                using (var fs = new FileStream(wavPath, FileMode.Open, FileAccess.Read))
                    speech = SoundEffect.FromStream(fs);
                speechInstance = speech.CreateInstance();
                speechInstance.IsLooped = false;
                InitWaveformRenderer(wavPath);
                KELog.Info($"[CustomEndingModule] EndingSpeech.wav loaded ({speech.Duration.TotalSeconds:F2}s).");
            }
            catch (Exception ex)
            {
                KELog.Warn($"[CustomEndingModule] Failed to load EndingSpeech.wav: {ex.Message}");
                noSpeechFile = true;
            }
        }
        else
        {
            KELog.Info($"[CustomEndingModule] EndingSpeech.wav NOT found — silent ({speechDurationFallback}s).");
            noSpeechFile = true;
        }

        try { MusicManager.stop(); } catch { }
    }

    // ========================================================================
    // ██  反射辅助  ██
    // ========================================================================

    private void InitDrawScanlinesReflection()
    {
        try
        {
            drawScanlinesMethod = typeof(OS).GetMethod("drawScanlines",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        }
        catch { }
    }

    private void InvokeDrawScanlines(OS osInstance)
    {
        if (drawScanlinesMethod != null)
            try { drawScanlinesMethod.Invoke(osInstance, null); } catch { }
    }

    private void InitWaveformRendererReflection()
    {
        try
        {
            var asm = typeof(OS).Assembly;
            waveRenderType = asm.GetType("Hacknet.UIUtils.WaveformRenderer");
            if (waveRenderType != null)
                renderWaveformMethod = waveRenderType.GetMethod("RenderWaveform",
                    new[] { typeof(double), typeof(double), typeof(SpriteBatch), typeof(Rectangle) });
        }
        catch (Exception ex) { KELog.Debug($"[CustomEndingModule] WaveformRenderer refl: {ex.Message}"); }
    }

    private void InitWaveformRenderer(string wavPath)
    {
        if (waveRenderType == null) return;
        try { waveRender = Activator.CreateInstance(waveRenderType, new object[] { wavPath }); }
        catch (Exception ex) { KELog.Debug($"[CustomEndingModule] WaveformRenderer ctor: {ex.Message}"); }
    }

    private void RenderWaveform(double time, double totalTime, SpriteBatch sb, Rectangle bounds)
    {
        if (waveRender == null || renderWaveformMethod == null) return;
        try { renderWaveformMethod.Invoke(waveRender, new object[] { time, totalTime, sb, bounds }); }
        catch { }
    }

    // ========================================================================
    // ██  演讲 Update/Draw  ██
    // ========================================================================

    private void UpdateSpeech(float t)
    {
        if (noSpeechFile)
        {
            elapsedTime += t;
            if (elapsedTime > speechDurationFallback) { RollCredits(); return; }
            AdvanceSpeechText(t);
        }
        else if (speechInstance != null)
        {
            if (speechInstance.State == SoundState.Playing)
            {
                elapsedTime += t;
                if (elapsedTime > (float)speech.Duration.TotalSeconds) { RollCredits(); return; }
                AdvanceSpeechText(t);
            }
            else { speechInstance.Play(); }
        }
    }

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

        if (!noSpeechFile && speech != null && waveRender != null)
        {
            var bounds = new Rectangle(0, os.fullscreen.Height / 2 - h / 2, w, h);
            RenderWaveform(elapsedTime, speech.Duration.TotalSeconds, spriteBatch, bounds);
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
        //os.Flags.AddFlag("Victory");
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

        //try { //ComputerLoader.loadMission("Content/Missions/CreditsMission.xml"); } catch { }
        try { os.threadedSaveExecute(); } catch { }
        MediaPlayer.IsRepeating = true;
        string afterSong = string.IsNullOrEmpty(afterMusic) ? "Music\\Bit(Ending)" : afterMusic;
        try { MusicManager.playSongImmediatley(afterSong); } catch { }

        try { OnCompleteCallback?.Invoke(); }
        catch (Exception ex) { KELog.Warn($"[CustomEndingModule] OnCompleteCallback error: {ex.Message}"); }

        KELog.Info("[CustomEndingModule] Complete — Victory set.");
    }

    // ========================================================================
    //  我们的字段（与基类同名 private 字段互相独立，互不干扰）
    // ========================================================================

    private new float elapsedTime = 0f;
    private bool isInCredits = false;
}
