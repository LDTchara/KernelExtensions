using Hacknet;
using Hacknet.Extensions;
using Hacknet.Gui;
using HarmonyLib;
using KernelExtensions.Configs;
using KernelExtensions.Managers;
using KernelExtensions.Modules;
using KernelExtensions.Utilities;
using Microsoft.Xna.Framework;
using Pathfinder.Executable;

namespace KernelExtensions.Executables
{
    /// <summary>
    /// PhaseSwift 可执行程序。
    /// 是 Pathfinder GameExecutable 的薄封装，核心逻辑委托给 PhaseSwiftManager。
    ///
    /// 状态机：
    ///   Locked（无 flag/config）→ 3s → 自动退出
    ///   NotStarted（有 flag+config）→ 点击"开始"按钮 → Manager.Start() → Active
    ///   Active → Shift 按钮切换场景 → Manager.SwitchToScene()
    ///   Active → IsComplete → Completing → 3s → 退出
    ///
    /// UI：
    ///   - 锁定画面：点阵背景 + 呼吸锁定文字（无按钮）
    ///   - 准备阶段：点阵背景 + "开始"按钮（文本可配置）
    ///   - 运行阶段：点阵背景 + "Shift"按钮 + 可选场景号
    ///
    /// 配置通过 PhaseSwift_{ConfigName} flag 指定。
    /// </summary>
    public class PhaseSwiftExe : GameExecutable
    {
        public static PhaseSwiftExe CurrentInstance { get; private set; }
        private static readonly List<PhaseSwiftExe> activeInstances = new();
        public static void CleanupAll() { foreach (var e in activeInstances.ToArray()) e.DoCleanup(); }

        /// <summary>是否存在正在运行的实例（事件层无痕互斥检查用，2026-08-23）。</summary>
        public static bool HasRunningInstance() => activeInstances.Any(inst => !inst.isExiting);

        /// <summary>当前正在运行的实例（事件层无痕互斥提示用，与 OnInitialize 兜底查找一致）。</summary>
        public static PhaseSwiftExe RunningInstance => activeInstances.FirstOrDefault(inst => !inst.isExiting);

        // 兼容转发给 Manager（旧 Action 可能通过 Exe 实例调用）
        public void SwitchToScene(int targetScene, float? fadeDuration = null, string overrideTheme = null)
            => PhaseSwiftManager.SwitchToScene(targetScene, fadeDuration, overrideTheme);
        public void SwitchMusicPhase(int phaseId)
            => PhaseSwiftManager.SwitchMusicPhase(phaseId);

        public bool IsComplete { get; set; }

        private enum RunState { Locked, NotStarted, Active, Completing }
        private RunState state = RunState.Active;
        private PhaseSwiftConfig config;
        private float completeTimer;
        private float lockTimer;
        private bool _guardBlocked;
        private bool _cleanedUp;

        public PhaseSwiftExe() : base()
        {
            ramCost = 60;
            IdentifierName = "PhaseSwift";
            name = "PhaseSwift";
            CanBeKilled = true;
            ErrorReturn = null;
            CurrentInstance = this;
            activeInstances.Add(this);
        }

        public override void OnInitialize()
        {
            base.OnInitialize();
            // 互斥检查（兜底防御，2026-08-23 主防线已移到事件层）：
            // KernelExtensions.OnExecutableExecute_Mutex 在 ExecutableExecuteEvent 取消重复启动，
            // 实例根本不会被创建（无痕，对齐 CustomTrial 的处理）；此处在实例仍被直接创建时兜底：
            // 拒绝的实例从 os.exes 移除避免窗口残留（isExiting 的实例仍会被绘制，Draw 靠 config==null
            // 不崩但窗口/ram 回落可见）
            var other = activeInstances.FirstOrDefault(inst => inst != this && !inst.isExiting);
            if (other != null)
            {
                // 恢复第一个实例的引用（构造函数已覆盖过）
                CurrentInstance = (PhaseSwiftExe)other;
                os.write(KELoc.Format("EXECUTABLE_ALREADY_RUNNING", "{0} already running!", other.IdentifierName));
                isExiting = true;
                _guardBlocked = true;
                activeInstances.Remove(this);
                try
                {
                    // ExeModule internal 无法直接访问，反射处理（同 CustomTrialExe 兜底）
                    var exes = AccessTools.Field(typeof(OS), "exes")?.GetValue(os) as System.Collections.IList;
                    exes?.Remove(this);
                }
                catch { }
                return;
            }
            PhaseSwift_onInit();
        }

        private void PhaseSwift_onInit()
        {
            // 注意：base.OnInitialize() 已在 OnInitialize() 中调用过
            string extRoot = "";
            if (ExtensionLoader.ActiveExtensionInfo != null)
                extRoot = ExtensionLoader.ActiveExtensionInfo.FolderPath.Replace('\\', '/');

            // PS already running -> skip to Active
            if (PhaseSwiftManager.IsRunning)
            {
                config = PhaseSwiftManager.Config;
                state = RunState.Active;
                // 读档后 PS 由 AutoRestore 启动（IsRunning=true），
                // 此处仍要应用配置的程序名，否则重启 exe 时显示默认名
                if (config != null && !ConfigValue.IsNone(config.ProgramName))
                {
                    IdentifierName = config.ProgramName;
                    name = config.ProgramName;
                }
                return;
            }

            string flag = os.Flags.GetFlagStartingWith("PhaseSwift_");
            if (string.IsNullOrEmpty(flag))
            {
                state = RunState.Locked;
                lockTimer = 3f;
                return;
            }

            string configName = flag.Substring("PhaseSwift_".Length);
            if (string.IsNullOrEmpty(configName)) configName = "Default";

            PhaseSwiftManager.Initialize(os, configName);
            config = PhaseSwiftManager.Config;

            if (config == null)
            {
                state = RunState.Locked;
                lockTimer = 3f;
                return;
            }

            state = RunState.NotStarted;

            if (!ConfigValue.IsNone(config.ProgramName))
            {
                IdentifierName = config.ProgramName;
                name = config.ProgramName;
            }
        }

        public override void OnUpdate(float dt)
        {
            base.OnUpdate(dt);

            if (state == RunState.Locked)
            {
                lockTimer -= dt;
                if (lockTimer <= 0f)
                {
                    isExiting = true;
                    Result = CompletionResult.Success;
                }
                return;
            }

            if (state == RunState.NotStarted) return;

            if (config == null) return;

            PhaseSwiftManager.UpdateAudioBuffers();
            PhaseSwiftManager.UpdateCrossfade(dt);

            if (IsComplete && state == RunState.Active)
            {
                state = RunState.Completing;
                completeTimer = 3f;
                CanBeKilled = false;
            }

            if (state == RunState.Completing)
            {
                completeTimer -= dt;
                if (completeTimer <= 0f)
                {
                    isExiting = true;
                    Result = CompletionResult.Success;
                }
            }
        }

        public override void Draw(float t)
        {
            base.Draw(t);
            drawTarget();
            drawOutline();

            Rectangle contentRect = new(bounds.X + 2, bounds.Y + Module.PANEL_HEIGHT + 2, bounds.Width - 4, bounds.Height - Module.PANEL_HEIGHT - 4);
            Rectangle bgRect = new(bounds.X + 2, bounds.Y + Module.PANEL_HEIGHT + 10, bounds.Width - 4, bounds.Height - (Module.PANEL_HEIGHT + 6));

            Color bgColor = config != null
                ? PhaseSwiftManager.GetDynamicColor(config.BackgroundColor, os.highlightColor)
                : os.highlightColor;
            Hacknet.Effects.ZoomingDotGridEffect.Render(contentRect, spriteBatch, os.timer, bgColor * 0.4f);

            if (state == RunState.Locked)
            {
                DrawLocked(bgRect);
                return;
            }

            if (state == RunState.NotStarted)
            {
                DrawStartButton(contentRect);
                return;
            }

            if (state == RunState.Active)
                DrawActiveSceneButton(contentRect);
            else if (state == RunState.Completing)
                DrawCompleteText(contentRect);
        }

        private void DrawStartButton(Rectangle contentRect)
        {
            int btnWidth = Math.Min(300, contentRect.Width - 20);
            int btnHeight = 30;
            Rectangle btnRect = new(
                contentRect.X + (contentRect.Width - btnWidth) / 2,
                contentRect.Y + contentRect.Height / 2 - btnHeight / 2,
                btnWidth, btnHeight
            );
            string text = config?.StartButtonText ?? "开始";
            if (Button.doButton(9002 + PID, btnRect.X, btnRect.Y, btnRect.Width, btnRect.Height, text, new Color?(os.highlightColor)))
            {
                state = RunState.Active;
                PhaseSwiftManager.Start();
            }
        }

        private void DrawActiveSceneButton(Rectangle contentRect)
        {
            if (config == null) return;
            int btnWidth = Math.Min(300, contentRect.Width - 20);
            int btnHeight = 30;
            Rectangle btnRect = new(
                contentRect.X + (contentRect.Width - btnWidth) / 2,
                contentRect.Y + contentRect.Height / 2 - btnHeight / 2,
                btnWidth, btnHeight
            );
            string btnText = config.ShiftButtonText;
            if (config.ShowSceneNumber)
                btnText += " (" + (PhaseSwiftManager.CurrentScene + 1) + "/" + config.Scenes.Count + ")";
            if (Button.doButton(9001 + PID, btnRect.X, btnRect.Y, btnRect.Width, btnRect.Height, btnText, new Color?(os.highlightColor)))
            {
                int next = (PhaseSwiftManager.CurrentScene + 1) % config.Scenes.Count;
                PhaseSwiftManager.SwitchToScene(next);
            }
        }

        private void DrawCompleteText(Rectangle contentRect)
        {
            // 完成计时器即将归零时不绘制文字，避免退出动画期间残留
            if (completeTimer <= 0.15f) return;
            string text = (config != null && !ConfigValue.IsNone(config.CompleteText))
                ? config.CompleteText : KELoc.Loc("PHASE_SWIFT_COMPLETE", "COMPLETE");
            Vector2 size = GuiData.font.MeasureString(text);
            Vector2 pos = new(contentRect.X + (contentRect.Width - size.X) / 2,
                              contentRect.Y + (contentRect.Height - size.Y) / 2);
            spriteBatch.DrawString(GuiData.font, text, pos, Color.LimeGreen);
        }

        private static string GetLocalizedLockedText()
        {
            return KELoc.Loc("PHASE_SWIFT_LOCKED", "LOCKED");
        }

        private void DrawLocked(Rectangle bgRect)
        {
            if (lockTimer <= 0.15f) return;
            string lockText = GetLocalizedLockedText();
            Vector2 textSize = GuiData.font.MeasureString(lockText);
            Vector2 textPos = new(
                bgRect.X + (bgRect.Width - textSize.X) / 2,
                bgRect.Y + (bgRect.Height - textSize.Y) / 2
            );
            // this.fade 处理入场/退出渐变，Math.Sin 做呼吸动效
            float breathing = (float)(Math.Sin(os.timer * 3.0) * 0.3 + 0.7);
            spriteBatch.DrawString(GuiData.font, lockText, textPos, os.highlightColor * breathing);
        }

        public override void OnComplete()
        {
            base.OnComplete();
            if (!_cleanedUp) DoCleanup();
        }

        public override void OnCompleteKilled()
        {
            if (!_cleanedUp)
            {
                _cleanedUp = true;
                activeInstances.Remove(this);
                if (CurrentInstance == this) CurrentInstance = null;
            }
        }

        private void DoCleanup()
        {
            _cleanedUp = true;
            // 被拦截的重实例不调用 Stop（不影响原实例）
            if (!_guardBlocked)
                PhaseSwiftManager.Stop(config?.FinishMode ?? "none");
            activeInstances.Remove(this);
            if (CurrentInstance == this) CurrentInstance = null;
        }
    }
}
