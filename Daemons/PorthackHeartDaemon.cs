using Hacknet;
using Hacknet.Effects;
using Hacknet.Gui;
using Hacknet.Extensions;
using HarmonyLib;
using KernelExtensions.Configs;
using KernelExtensions.Utilities;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Pathfinder.Daemon;
using Pathfinder.Util;

namespace KernelExtensions.Daemons
{
    /// <summary>
    /// 复刻原版 PorthackHeartDaemon（可配置剧情版）。
    ///
    /// ── 节点 XML 用法（全部参数可选）──────────────────────────────────────────
    /// &lt;Computer&gt;
    ///     &lt;PorthackHeartDaemon Title="PortHack.Heart" Music="Music/Ambient/AmbientDrone_Clipped"
    ///         FadeoutDelay="1" FadeoutDuration="10" AlignTime="2.5" HeartDuration="30"
    ///         FlashOutTime="3.8" OnComplete="Actions/HeartBroken" LockInput="true"
    ///         AutoOnPorthack="false"/&gt;
    /// &lt;/Computer&gt;
    /// ── 参数说明（均可选，带默认；字符串项支持 NONE 约定：写 NONE/留空 = 禁用该功能）──
    ///   Title          — 默认态闪烁标题文字（NONE/空=不显示）。默认 "PortHack.Heart"。
    ///   Music          — 心碎时切换的歌曲（原版 Content 路径；NONE/空=不切歌）。
    ///                     默认 "Music/Ambient/AmbientDrone_Clipped"。
    ///   FadeoutDelay   — 心碎后周围黑幕淡入延迟（秒）。默认 1。
    ///   FadeoutDuration— 周围黑幕淡入时长（秒）。默认 10。
    ///   AlignTime      — 立方体旋转对齐到正位时长（秒）。默认 2.5。
    ///   HeartDuration  — 心形序列总时长（秒）。默认 30。
    ///   FlashOutTime   — 心形完成后白色淡出时长（秒）。默认 3.8。
    ///   OnComplete     — 序列结束后加载的 Action 文件（相对扩展根，对齐 CompleteAction；
    ///                     NONE/空=不执行）。默认不执行。
    ///   OnHeartbreak   — 开始碎心（BreakHeart 触发）时加载的 Action 文件（相对扩展根，
    ///                     可与 OnComplete 搭配做"碎心开场/结束后收尾"；NONE/空=不执行）。
    ///                     默认不执行。
    ///   LockInput      — 心碎期间是否锁定输入/禁用顶栏按钮。默认 true（对齐原版）。
    ///   AutoOnPorthack — 是否由 porthack 破解（进度&gt;50%）自动触发心碎。默认 false。
    ///
    /// ── 行为特性 ─────────────────────────────────────────────────────────────
    ///   · 默认态: 旋转 3D 线框立方体 + 闪烁标题（对齐原版）
    ///   · BreakHeart(): 取消追踪/清弹窗/（可配）锁输入 → 切歌 → 启动音效
    ///     （SFX/TraceKill）+ 18s 后 glow（SFX/Ending/PorthackSpindown，原版资源，
    ///     缺失静默）→ 立方体对齐 → 心形序列 → 白色淡出
    ///   · **不触发原版结局**（原版 endingSequence 是硬编码结局，对扩展无用；自定义
    ///     结局画面由 AC 模组实现，届时搬入）。对齐原版"心碎=终结"：序列结束进入
    ///     终结态（暗屏），自动做通用清理——解锁输入 + 断开玩家 + heart 节点失效
    ///     （移除可见/disabled/清 daemon/换随机 IP），然后执行 OnComplete 交给剧情
    ///     接管（flag/结局任务/音乐/存档等由作者在 OnComplete 里自行定义）；
    ///     ResetHeartbreak() 可主动复位重玩
    ///   · 音效不开放配置（用户定）：直接用原版 Content 路径，try-catch 缺失静默
    ///   · 触发入口: BreakHeartAction（剧情显式）/ AutoOnPorthack（porthack 破解）
    ///   · 存档: BaseDaemon.GetSaveElement（[XMLStorage] 字段自动序列化）
    /// </summary>
    public class PorthackHeartDaemon : BaseDaemon
    {
        // ====== XML 可配置（[XMLStorage] 字段，全部可选带默认） ======
        [XMLStorage] public string Title = "PortHack.Heart";
        [XMLStorage] public string Music = "Music/Ambient/AmbientDrone_Clipped";
        [XMLStorage] public float FadeoutDelay = 1f;
        [XMLStorage] public float FadeoutDuration = 10f;
        [XMLStorage] public float AlignTime = 2.5f;
        [XMLStorage] public float HeartDuration = 30f;
        [XMLStorage] public float FlashOutTime = 3.8f;
        [XMLStorage] public string OnComplete;
        [XMLStorage] public string OnHeartbreak; // 开始碎心（BreakHeart 触发）时加载的 Action 文件（相对扩展根）
        [XMLStorage] public bool LockInput = true;
        [XMLStorage] public bool AutoOnPorthack = false;

        // ====== 运行时状态（不序列化） ======
        private RenderTarget2D rendertarget;
        private SpriteBatch rtSpritebatch;
        private float playTimeExpended = 0f;
        private bool PlayingHeartbreak = false;
        private readonly PortHackCubeSequence pcs = new PortHackCubeSequence();
        private bool IsFlashingOut = false;
        private float flashOutTime = 0f;
        private SoundEffect spinDownEffect;
        private SoundEffect glowSoundEffect;
        private bool heartbreakTriggered = false; // 一次性触发标志（对齐原版 hasCheckedForheart，防 AutoOnPorthack 每帧重复触发）
        private bool heartbreakFinished = false;  // 终结态：心碎序列已结束

        public PorthackHeartDaemon(Computer c, string serviceName, OS os)
            : base(c, serviceName, os)
        {
            name = "Porthack.Heart";
            // 原版资源路径，扩展模式 os.content 仍含原版 Content；缺失静默（不崩）
            try { spinDownEffect = os.content.Load<SoundEffect>("SFX/TraceKill"); } catch { }
            try { glowSoundEffect = os.content.Load<SoundEffect>("SFX/Ending/PorthackSpindown"); } catch { }
        }

        public override string Identifier => "Porthack.Heart";

        private static bool IsNone(string s) => ConfigValue.IsNone(s);

        /// <summary>触发"心碎"序列（对齐原版 BreakHeart；不触发原版结局）。</summary>
        public void BreakHeart()
        {
            if (heartbreakTriggered || heartbreakFinished) return;
            heartbreakTriggered = true;

            if (os.TraceDangerSequence.IsActive)
                os.TraceDangerSequence.CancelTraceDangerSequence();
            os.RequestRemovalOfAllPopups();
            PlayingHeartbreak = true;

            if (LockInput)
            {
                os.terminal.inputLocked = true;
                os.netMap.inputLocked = true;
                os.ram.inputLocked = true;
                os.DisableTopBarButtons = true;
            }

            if (!IsNone(Music))
            {
                try { MusicManager.transitionToSong(Music); } catch { }
            }
            try { spinDownEffect?.Play(); } catch { }

            // 开始碎心：执行 OnHeartbreak Action（若有）
            ExecuteActionFile(OnHeartbreak);

            os.delayer.Post(ActionDelayer.Wait(18.0), delegate
            {
                try { glowSoundEffect?.Play(); } catch { }
            });

            if (ConfigLoader.Debug)
                KELog.Info($"[PorthackHeart] '{Identifier}' heartbreak started");
        }

        /// <summary>供扩展/测试复位（恢复默认态，允许再次触发）。</summary>
        public void ResetHeartbreak()
        {
            heartbreakTriggered = false;
            heartbreakFinished = false;
            PlayingHeartbreak = false;
            IsFlashingOut = false;
            playTimeExpended = 0f;
            flashOutTime = 0f;
            pcs.Reset();
            PostProcessor.EndingSequenceFlashOutActive = false;
            PostProcessor.EndingSequenceFlashOutPercentageComplete = 0f;
            UnlockInputIfLocked();
        }

        private void UnlockInputIfLocked()
        {
            if (!LockInput) return;
            os.terminal.inputLocked = false;
            os.netMap.inputLocked = false;
            os.ram.inputLocked = false;
            os.DisableTopBarButtons = false;
        }

        private void UpdateForTime(Rectangle bounds, SpriteBatch sb)
        {
            if (heartbreakFinished) return; // 已终结：不再更新（OnComplete 剧情接管）

            if (playTimeExpended > FadeoutDelay)
            {
                float fade = Math.Min(1f, (playTimeExpended - FadeoutDelay) / FadeoutDuration);
                var correctedbounds = new Rectangle(bounds.X, bounds.Y - Module.PANEL_HEIGHT,
                    bounds.Width, bounds.Height + Module.PANEL_HEIGHT);
                os.postFXDrawActions += delegate
                {
                    Utils.FillEverywhereExcept(correctedbounds, os.fullscreen, sb, Color.Black * fade * 0.8f);
                };
            }

            if (pcs.HeartFadeSequenceComplete)
            {
                IsFlashingOut = true;
                flashOutTime += (float)os.lastGameTime.ElapsedGameTime.TotalSeconds;
                if (flashOutTime > FlashOutTime)
                {
                    flashOutTime = FlashOutTime;
                    PostProcessor.EndingSequenceFlashOutActive = false;
                    PostProcessor.EndingSequenceFlashOutPercentageComplete = 0f;
                    // KE 版：不触发原版结局（endingSequence 硬编码原版剧情）。
                    // 对齐原版"心碎=终结"：进入终结态（暗屏），做通用清理（解锁/断开/
                    // 节点失效），执行 OnComplete 交给剧情接管（flag/结局任务/音乐/保存
                    // 由作者在 OnComplete 里自行定义）；不复位。
                    CompleteHeartbreak();
                    return;
                }
                PostProcessor.EndingSequenceFlashOutPercentageComplete = flashOutTime / FlashOutTime;
            }
            else
            {
                IsFlashingOut = false;
            }
            PostProcessor.EndingSequenceFlashOutActive = IsFlashingOut;
        }

        /// <summary>心碎序列终结收尾（对齐原版 CompleteAndReturnToMenu 的通用清理部分）。</summary>
        private void CompleteHeartbreak()
        {
            heartbreakFinished = true;
            UnlockInputIfLocked();

            // 显式关闭错误状态中的 porthackexe（os.exes 里的 PortHackExe → isExiting=true；
            // ExeModule internal 无法直接访问，反射处理）——porthack 处于 UNKNOWN ERROR
            // 特殊状态且 progress 永不完成，断开/清理前必须关闭，否则界面残留
            try { ClosePorthackExe(); } catch { }

            // 断开玩家 + 清理 heart 节点（移除可见/禁用/清 daemon/换随机 IP），
            // 防止悬空连接与重复访问；flag/结局任务/音乐/存档等交给 OnComplete 自定义
            try { Programs.disconnect(new string[0], os); } catch { }
            try
            {
                var computer = comp;
                if (computer != null)
                {
                    int idx = os.netMap.nodes.IndexOf(computer);
                    if (idx >= 0)
                        os.netMap.visibleNodes.Remove(idx);
                    computer.disabled = true;
                    computer.daemons.Clear();
                    computer.ip = NetworkMap.generateRandomIP();
                    // 对齐 FlightDaemon 坠机改 IP 的坑：Pathfinder 的 getComputer/connect 走
                    // ComputerLookup 缓存（NodeLookup patch），改 IP 后必须刷新缓存，否则旧 IP
                    // 仍可解析到该节点（原版由 OnAircraftDaemonChangeIP 自动处理，但该 patch
                    // 绑定原版 AircraftDaemon，KE 自实现类需手动调用——2026-08-24 修复）
                    Pathfinder.Util.ComputerLookup.RebuildLookups();
                }
            }
            catch { }

            ExecuteOnComplete();
        }

        /// <summary>关闭 os.exes 中处于 UNKNOWN ERROR 特殊状态的 PortHackExe（isExiting=true）。</summary>
        private void ClosePorthackExe()
        {
            var exes = AccessTools.Field(typeof(OS), "exes")?.GetValue(os) as System.Collections.IList;
            if (exes == null) return;
            for (int i = 0; i < exes.Count; i++)
            {
                var exe = exes[i];
                if (exe != null && exe.GetType().Name == "PortHackExe")
                {
                    var f = exe.GetType().GetField("isExiting", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    f?.SetValue(exe, true);
                }
            }
        }

        private void ExecuteOnComplete()
        {
            ExecuteActionFile(OnComplete);
        }

        /// <summary>执行一个 Action 文件（相对扩展根；NONE/空=不执行，缺失/异常 KELog.Error 不崩）。</summary>
        private void ExecuteActionFile(string path)
        {
            if (IsNone(path)) return;
            try
            {
                string extRoot = ExtensionLoader.ActiveExtensionInfo?.FolderPath?.Replace('\\', '/');
                ActionHelper.ExecuteActionFile(os, path, extRoot);
            }
            catch (Exception ex)
            {
                KELog.Error($"[PorthackHeart] action failed: {ex.Message}");
            }
        }

        public override void draw(Rectangle bounds, SpriteBatch sb)
        {
            base.draw(bounds, sb);

            // 终结态：心已碎，维持暗屏，不再渲染默认立方体/标题；
            // 由 OnComplete 剧情接管（切场景/自定义结局画面）；ResetHeartbreak() 可主动复位重玩
            if (heartbreakFinished)
            {
                try
                {
                    sb.Draw(Utils.white,
                        new Rectangle(bounds.X, bounds.Y - Module.PANEL_HEIGHT,
                            bounds.Width, bounds.Height + Module.PANEL_HEIGHT), Color.Black);
                }
                catch { }
                return;
            }

            try
            {
                int width = bounds.Width;
                int height = bounds.Height;
                if (rendertarget == null || rendertarget.Width != width || rendertarget.Height != height)
                {
                    if (rtSpritebatch == null)
                        rtSpritebatch = new SpriteBatch(sb.GraphicsDevice);
                    if (rendertarget != null)
                        rendertarget.Dispose();
                    rendertarget = new RenderTarget2D(sb.GraphicsDevice, width, height);
                }

                if (!PlayingHeartbreak && !IsNone(Title))
                {
                    TextItem.DrawShadow = false;
                    TextItem.doFontLabel(new Vector2(bounds.X + 6, bounds.Y + 2),
                        Utils.FlipRandomChars(Title, 0.003f), GuiData.font,
                        Utils.AddativeWhite * 0.6f, bounds.Width - 10, 100f);
                    TextItem.doFontLabel(new Vector2(bounds.X + 6, bounds.Y + 2),
                        Utils.FlipRandomChars(Title, 0.1f), GuiData.font,
                        Utils.AddativeWhite * 0.2f, bounds.Width - 10, 100f);
                }

                if (PlayingHeartbreak)
                    playTimeExpended += (float)os.lastGameTime.ElapsedGameTime.TotalSeconds;
                UpdateForTime(bounds, sb);

                RenderTarget2D currentRenderTarget = Utils.GetCurrentRenderTarget();
                sb.GraphicsDevice.SetRenderTarget(rendertarget);
                sb.GraphicsDevice.Clear(Color.Transparent);
                rtSpritebatch.Begin();

                var dest = new Rectangle(0, 0, bounds.Width, bounds.Height);
                var value = new Vector3(MathHelper.ToRadians(35.4f), MathHelper.ToRadians(45f), MathHelper.ToRadians(0f));
                var vector = new Vector3(1f, 1f, 0f) * os.timer * 0.2f
                             + new Vector3(os.timer * 0.1f, os.timer * -0.4f, 0f);

                if (PlayingHeartbreak)
                {
                    if (playTimeExpended < AlignTime)
                    {
                        vector = Vector3.Lerp(Utils.NormalizeRotationVector(vector), value,
                            Utils.QuadraticOutCurve(playTimeExpended / AlignTime));
                        KECube3D.RenderWireframe(sb.GraphicsDevice, Vector3.Zero, 2.6f, vector, Color.White);
                    }
                    else
                    {
                        pcs.DrawHeartSequence(dest, (float)os.lastGameTime.ElapsedGameTime.TotalSeconds, HeartDuration);
                    }
                }
                else
                {
                    KECube3D.RenderWireframe(sb.GraphicsDevice, Vector3.Zero, 2.6f, vector, Color.White);
                }
                rtSpritebatch.End();
                sb.GraphicsDevice.SetRenderTarget(currentRenderTarget);

                var rect = new Rectangle(bounds.X + (bounds.Width - width) / 2,
                    bounds.Y + (bounds.Height - height) / 2, width, height);
                float rarity = Math.Min(1f, playTimeExpended / AlignTime * 0.8f + 0.2f);
                FlickeringTextEffect.DrawFlickeringSprite(sb, rect, rendertarget, 4f, rarity, os, Color.White);
                sb.Draw(rendertarget, rect, Utils.AddativeWhite * 0.7f);
            }
            catch (Exception ex)
            {
                KELog.Error($"[PorthackHeart] draw failed: {ex.Message}");
            }
        }
    }
}
