using System;
using Hacknet;
using Hacknet.Effects;
using Hacknet.Gui;
using Hacknet.Extensions;
using KernelExtensions.Configs;
using KernelExtensions.Utility;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Pathfinder.Daemon;
using Pathfinder.Util;

namespace KernelExtensions.Daemons
{
    /// <summary>
    /// 9.47 复刻原版 PorthackHeartDaemon（可配置剧情版）。
    ///
    /// ── 节点 XML 用法（全部参数可选）──────────────────────────────────────────
    /// &lt;Computer&gt;
    ///     &lt;PorthackHeartDaemon Title="PortHack.Heart" Music="Music/Ambient/AmbientDrone_Clipped"
    ///         FadeoutDelay="1" FadeoutDuration="10" AlignTime="2.5" HeartDuration="30"
    ///         FlashOutTime="3.8" OnComplete="Actions/HeartBroken" LockInput="true"
    ///         AutoOnPorthack="false"/&gt;
    /// &lt;/Computer&gt;
    /// ── 参数说明（均可选，带默认）────────────────────────────────────────────
    ///   Title          — 默认态闪烁标题文字（空=不显示）。默认 "PortHack.Heart"。
    ///   Music          — 心碎时切换的歌曲（原版 Content 路径；空=不切歌）。
    ///                     默认 "Music/Ambient/AmbientDrone_Clipped"。
    ///   FadeoutDelay   — 心碎后周围黑幕淡入延迟（秒）。默认 1。
    ///   FadeoutDuration— 周围黑幕淡入时长（秒）。默认 10。
    ///   AlignTime      — 立方体旋转对齐到正位时长（秒）。默认 2.5。
    ///   HeartDuration  — 心形序列总时长（秒）。默认 30。
    ///   FlashOutTime   — 心形完成后白色淡出时长（秒）。默认 3.8。
    ///   OnComplete     — 序列结束后加载的 Action 文件（相对扩展根，对齐 9.36）。
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
    ///     结局画面由 AC 模组实现，届时搬入）。序列结束执行 OnComplete 并复位到
    ///     默认态（一次性，防重复触发）
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
        private bool heartbreakFinished = false; // 一次性：心碎序列已结束（防重复触发）

        public PorthackHeartDaemon(Computer c, OS os)
            : base(c, "Porthack.Heart", os)
        {
            name = "Porthack.Heart";
            // 原版资源路径，扩展模式 os.content 仍含原版 Content；缺失静默（不崩）
            try { spinDownEffect = os.content.Load<SoundEffect>("SFX/TraceKill"); } catch { }
            try { glowSoundEffect = os.content.Load<SoundEffect>("SFX/Ending/PorthackSpindown"); } catch { }
        }

        public override string Identifier => "Porthack.Heart";

        /// <summary>触发"心碎"序列（对齐原版 BreakHeart；不触发原版结局）。</summary>
        public void BreakHeart()
        {
            if (heartbreakFinished) return;

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

            if (!string.IsNullOrEmpty(Music))
            {
                try { MusicManager.transitionToSong(Music); } catch { }
            }
            try { spinDownEffect?.Play(); } catch { }

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
                    // KE 版：不触发原版结局（endingSequence 硬编码原版剧情）；
                    // 序列结束 → OnComplete + 复位（一次性，防重复）
                    heartbreakFinished = true;
                    UnlockInputIfLocked();
                    ExecuteOnComplete();
                    ResetHeartbreak();
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

        private void ExecuteOnComplete()
        {
            if (string.IsNullOrEmpty(OnComplete)) return;
            try
            {
                string extRoot = ExtensionLoader.ActiveExtensionInfo?.FolderPath?.Replace('\\', '/');
                ActionHelper.ExecuteActionFile(os, OnComplete, extRoot);
            }
            catch (Exception ex)
            {
                KELog.Error($"[PorthackHeart] OnComplete failed: {ex.Message}");
            }
        }

        public override void draw(Rectangle bounds, SpriteBatch sb)
        {
            base.draw(bounds, sb);
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

                if (!PlayingHeartbreak && !string.IsNullOrEmpty(Title))
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
