using Hacknet;
using Hacknet.Effects;
using Hacknet.Gui;
using KernelExtensions.Configs;
using KernelExtensions.Managers;
using KernelExtensions.Utilities;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Pathfinder.Executable;
using System.Globalization;
using System.Reflection;

namespace KernelExtensions.Executables;

/// <summary>
/// ProxyTuner（注册名 #PROXYTUNER#）—— 游戏内实时调节**代理破解倍数**的调参程序。
///
/// 定位：给扩展作者调参用的工具，不是入侵程序。它做两件事：
///   1. 用原版现成的 <see cref="DepthDotGridEffect"/> 铺一块可实时改参数的动画区；
///   2. 把「过载倍数」实时作用到当前连接机器的代理上（当作自动过载驱动器），
///      让作者当场看到 1.0 倍 = 原版速度、2.0 倍 = 两倍速的效果。
///
/// 入口：存档 flag <c>ProxyTuner_&lt;文件名&gt;</c> → 扩展根 <c>ProxyTuner/&lt;文件名&gt;.xml</c>。
/// 无 flag / 多 flag / 配置加载失败 → 终端提示 + 窗口停留 3 秒后自动退出。
///
/// 调参结果**不写盘**（对齐 RamDisplay 的「不存档即临时状态」决定）：退出后配置仍是原值，
/// 作者调好后自己把数值抄进 XML。
///
/// 有意不调 <c>hostileActionTaken()</c>：本程序是调参工具，且只有作者自己会跑，
/// 原版同类调试程序也没有这个惯例。
/// </summary>
public class ProxyTunerExe : GameExecutable
{
    private const string LogTag = "[ProxyTuner]";

    /// <summary>锁定 / 报错提示在窗口里的停留秒数（终端提示会留在终端里，可回看）。</summary>
    private const float LockedSeconds = 3f;

    /// <summary>刚被调整过的那一行高亮多久（秒）。</summary>
    private const float FlashSeconds = 0.35f;

    // DepthDotGridEffect 还有两个参数没有对外开放（视觉方向未定）：
    // pixelsInOnRecurse / MaxDepthEffectDistance 取同项目 EffectsPlayerExe 的取值
    // （原版 DLC 的 SignalScramble 用的是另一组，观感差别很大）。
    private const float PixelsInOnRecurse = 1f;
    private const float MaxDepthEffectDistance = 30f;

    /// <summary>程序状态：锁定提示 / 正常调参 / 退出倒计时。</summary>
    private enum Phase
    {
        /// <summary>没有入口牌或配置加载失败：显示提示，<see cref="LockedSeconds"/> 秒后退出。</summary>
        Locked,

        /// <summary>正常调参。</summary>
        Running,

        /// <summary>已按 Exit：走 <c>CompleteDelay</c> 秒的退出倒计时（期间可 Skip 立即退出）。</summary>
        Closing,
    }

    // ---- Button ID 偏移（同一个 exe 内唯一）----
    private const int IdReset = 11;
    private const int IdExit = 12;
    private const int IdRowBase = 100;   // 每行两个：IdRowBase + i*2 =「-」，+1 =「+」

    // ---- 运行时状态 ----
    private Phase _phase = Phase.Locked;
    private string _lockedMessage = string.Empty;
    private float _phaseTimer;      // Locked / Closing 倒计时
    private float _animTimer;       // 动画推进量（按 AnimationSpeed 累积）
    private float _flashTimer;      // 行高亮剩余时间
    private int _flashRow = -1;

    private ProxyTunerConfig _cfg;
    private Color _accent;
    /// <summary>强调色的原始配置串（动态色要每帧拿它重算）。</summary>
    private string _accentStr;

    /// <summary>
    /// 重算强调色。动态色（CC 预设 / Rainbow / Gradient）随时间变化，必须逐帧调用；
    /// 留空 / NONE 时 CustomColorManager 直接返回默认色（白色）。
    /// </summary>
    private void RefreshAccent()
        => _accent = CustomColorManager.GetDynamicColor(_accentStr, ProxyTunerConfig.DefaultAccentColor);
    private DepthDotGridEffect _grid;
    private readonly List<TuneRow> _rows = new();

    // ---- 运行时可调值（Reset 时从 _cfg 的解析结果恢复）----
    private float _overloadMul;
    private float _animSpeed;
    private int _depth;
    private float _dotSize;
    private float _dotSep;
    private float _chaos;
    private int _ramMb;
    private float _hold;

    /// <summary>存档 flag 的内部列表字段（<see cref="ProgressionFlags"/> 只公开 HasFlag / GetFlagStartingWith）。</summary>
    private static readonly FieldInfo _progressionFlagsField = typeof(ProgressionFlags)
        .GetField("Flags", BindingFlags.Instance | BindingFlags.NonPublic);

    /// <summary>构造：RAM 先按配置默认值给，供引擎在 <see cref="OnInitialize"/> 之前做 RAM 校验。</summary>
    public ProxyTunerExe() : base()
    {
        name = "ProxyTuner";
        IdentifierName = "ProxyTuner";
        ramCost = ProxyTunerConfig.DefaultRamCost;
    }

    // ======================= 初始化 =======================

    /// <summary>初始化：找入口牌 → 加载配置 → 建运行时状态与可调项。</summary>
    public override void OnInitialize()
    {
        base.OnInitialize();

        List<string> flags = FindProxyTunerFlags();
        if (flags.Count == 0)
        {
            KELog.Info($"{LogTag} no config flag present - locked.");
            Lock(KELoc.Loc("PROXY_TUNER_LOCKED", "ProxyTuner: no configuration is armed."));
            return;
        }
        if (flags.Count > 1)
        {
            KELog.Error($"{LogTag} {flags.Count} config flags present, expected exactly one: " + string.Join(", ", flags));
            Lock(KELoc.Loc("PROXY_TUNER_MULTI_FLAG", "ProxyTuner: multiple configurations are armed (authoring error)."));
            return;
        }

        _cfg = ProxyTunerConfig.LoadByFlag(flags[0]);
        if (_cfg == null)
        {
            Lock(KELoc.Loc("PROXY_TUNER_LOAD_FAILED", "ProxyTuner: configuration could not be loaded."));
            return;
        }

        if (!string.IsNullOrWhiteSpace(_cfg.ProgramName))
        {
            name = _cfg.ProgramName;
            IdentifierName = _cfg.ProgramName;
        }
        ramCost = _cfg.RamCost;

        // 强调色统一走 CustomColorManager：留空 / NONE = 配置默认（白色），
        // 并支持 CustomColor 预设名与动态色（LDTchara / Rainbow / Gradient:…）。
        // 动态色会随时间变化，故在 Update 里逐帧重算而不是在这里定格一次。
        _accentStr = _cfg.AccentColorRaw;
        RefreshAccent();

        try
        {
            _grid = new DepthDotGridEffect(os.content);
        }
        catch (Exception ex)
        {
            KELog.Error($"{LogTag} could not create the DepthDotGrid effect: {ex.Message}");
            Lock(KELoc.Loc("PROXY_TUNER_LOAD_FAILED", "ProxyTuner: configuration could not be loaded."));
            return;
        }

        RestoreFromConfig();
        BuildRows();
        _phase = Phase.Running;
        _phaseTimer = 0f;

        KELog.Info($"{LogTag} config '{_cfg.SourceName}' loaded (style '{_cfg.AnimationStyle}', "
            + $"overload x{_overloadMul.ToString("0.##", CultureInfo.InvariantCulture)}, ram {_ramMb}MB).");
    }

    /// <summary>
    /// 找出存档里所有属于本程序的 flag（<c>ProxyTuner_*</c>）。
    /// 正常情况下应恰好一个（flag = 入口牌）；0 个 → 锁定；多个 → 作者配置错误。
    ///
    /// ⚠️ 用**注入的 os 实例**取 flag（对齐 CustomTrialExe 的 <c>os.Flags</c>），
    /// 不用静态的 <c>OS.currentInstance</c> —— 后者是全局，不匹配时会让这里静默变成"没配置"。
    /// </summary>
    private List<string> FindProxyTunerFlags()
    {
        List<string> found = new();
        ProgressionFlags pf = os?.Flags;
        if (pf == null)
        {
            KELog.Warn($"{LogTag} os.Flags is unavailable on this instance; treating as no config.");
            return found;
        }

        // ProgressionFlags 只暴露 HasFlag / GetFlagStartingWith（后者仅返回第一个），
        // 没有「枚举全部」的公开 API；要检出多个 flag 只能反射读内部 List。
        // 反射失败时退化为「取第一个匹配」——仍能工作，只是检不出多 flag 的情形。
        bool enumerated = false;
        try
        {
            if (_progressionFlagsField?.GetValue(pf) is List<string> all)
            {
                enumerated = true;
                foreach (string f in all)
                    if (f != null && f.StartsWith(ProxyTunerConfig.FlagPrefix, StringComparison.Ordinal))
                        found.Add(f);
            }
        }
        catch (Exception ex)
        {
            KELog.Warn($"{LogTag} could not enumerate flags ({ex.Message}); falling back to first match.");
        }

        if (!enumerated)
        {
            string one = pf.GetFlagStartingWith(ProxyTunerConfig.FlagPrefix);
            if (!string.IsNullOrEmpty(one)) found.Add(one);
        }

        // 诊断：区分「flag 没进存档 / 前缀写错 / 反射失败」
        KELog.Info($"{LogTag} flag scan: prefix='{ProxyTunerConfig.FlagPrefix}', "
            + $"enumerated={enumerated}, matched={found.Count}"
            + (found.Count > 0 ? $" [{string.Join(", ", found)}]" : string.Empty));
        return found;
    }

    /// <summary>进入锁定态：终端提示（玩家可见）+ 窗口停留数秒后自动退出。</summary>
    private void Lock(string message)
    {
        _lockedMessage = message;
        os.write(message);
        _phase = Phase.Locked;
        _phaseTimer = LockedSeconds;
    }

    /// <summary>把运行时值恢复成配置里的解析结果（Reset 与初始化共用）。</summary>
    private void RestoreFromConfig()
    {
        _overloadMul = _cfg.SpeedMultiplier;
        _animSpeed = _cfg.AnimationSpeed;
        _depth = _cfg.Depth;
        _dotSize = _cfg.DotSize;
        _dotSep = _cfg.DotSeparation;
        _chaos = _cfg.Chaos;
        _ramMb = _cfg.RamCost;
        _hold = _cfg.CompleteDelay;
        ramCost = _ramMb;
    }

    // ======================= 可调项 =======================

    /// <summary>
    /// 右侧面板的一行可调项：名称 + 当前值 +「-」「+」按钮。
    /// 值用委托读写（直接落到运行时字段），所以调节**立即生效**，无需重启程序。
    /// </summary>
    private sealed class TuneRow
    {
        /// <summary>KELoc 词条 key。</summary>
        public string LabelKey;

        /// <summary>KELoc 缺失时的英文兜底。</summary>
        public string LabelFallback;

        /// <summary>日志行模板，<c>{0}</c> = 当前值文本（英文、动作级结果，一次调节一条）。</summary>
        public string LogFormat;

        /// <summary>值文本前缀（只有倍数项用 <c>x</c>）。</summary>
        public string Prefix = string.Empty;

        /// <summary>值文本的数值格式（InvariantCulture）。</summary>
        public string Format = "0.##";

        /// <summary>一次点按的步进：倍数项 0.1，整数项 1。</summary>
        public float Step = 1f;

        /// <summary>取值范围下限（配置契约里的同一范围）。</summary>
        public float Min;

        /// <summary>取值范围上限。</summary>
        public float Max;

        /// <summary>读当前值。</summary>
        public Func<float> Get;

        /// <summary>写当前值（立即生效）。</summary>
        public Action<float> Set;
    }

    /// <summary>按配置契约建可调项列表（顺序 = 面板从上到下的顺序）。</summary>
    private void BuildRows()
    {
        _rows.Clear();

        _rows.Add(new TuneRow
        {
            LabelKey = "PROXY_TUNER_ROW_OVERLOAD",
            LabelFallback = "Overload x",
            // 倍数项：日志里明确写出它实际影响的是「代理过载速度」（自动过载驱动），避免与别的语义混淆
            LogFormat = "overload multiplier = {0} - auto-overload drive: proxyOverloadTicks -= dt * {0} (1.0 = vanilla rate).",
            Prefix = "x",
            Format = "0.0##",
            Step = 0.1f,
            Min = ProxyTunerConfig.MinSpeedMultiplier,
            Max = ProxyTunerConfig.MaxSpeedMultiplier,
            Get = () => _overloadMul,
            Set = v => _overloadMul = v,
        });

        _rows.Add(new TuneRow
        {
            LabelKey = "PROXY_TUNER_ROW_ANIM",
            LabelFallback = "Anim Spd",
            LogFormat = "animation speed = {0}.",
            Format = "0.0##",
            Step = 0.1f,
            Min = ProxyTunerConfig.MinAnimationSpeed,
            Max = ProxyTunerConfig.MaxAnimationSpeed,
            Get = () => _animSpeed,
            Set = v => _animSpeed = v,
        });

        _rows.Add(new TuneRow
        {
            LabelKey = "PROXY_TUNER_ROW_DEPTH",
            LabelFallback = "Depth",
            LogFormat = "render depth = {0}.",
            Format = "0",
            Step = 1f,
            Min = ProxyTunerConfig.MinDepth,
            Max = ProxyTunerConfig.MaxDepth,
            Get = () => _depth,
            Set = v => _depth = (int)Math.Floor(v + 0.5f),
        });

        _rows.Add(new TuneRow
        {
            LabelKey = "PROXY_TUNER_ROW_DOTSIZE",
            LabelFallback = "Dot Size",
            LogFormat = "dot size = {0}px.",
            Format = "0.#",
            Step = 1f,
            Min = ProxyTunerConfig.MinDotSize,
            Max = ProxyTunerConfig.MaxDotSize,
            Get = () => _dotSize,
            Set = v => _dotSize = v,
        });

        _rows.Add(new TuneRow
        {
            LabelKey = "PROXY_TUNER_ROW_DOTSEP",
            LabelFallback = "Dot Sep",
            LogFormat = "dot separation = {0}px.",
            Format = "0.#",
            Step = 1f,
            // 0 会让原版效果的绘制循环原地打转（死循环），所以运行时也只允许到安全下限
            Min = ProxyTunerConfig.MinDotSeparation,
            Max = ProxyTunerConfig.MaxDotSeparation,
            Get = () => _dotSep,
            Set = v => _dotSep = v,
        });

        _rows.Add(new TuneRow
        {
            LabelKey = "PROXY_TUNER_ROW_CHAOS",
            LabelFallback = "Chaos",
            LogFormat = "chaos = {0}.",
            Format = "0.##",
            Step = 0.1f,
            Min = ProxyTunerConfig.MinChaos,
            Max = ProxyTunerConfig.MaxChaos,
            Get = () => _chaos,
            Set = v => _chaos = v,
        });

        _rows.Add(new TuneRow
        {
            LabelKey = "PROXY_TUNER_ROW_RAM",
            LabelFallback = "RAM (MB)",
            LogFormat = "ram cost = {0}MB.",
            Format = "0",
            Step = 1f,
            Min = ProxyTunerConfig.MinRamCost,
            Max = ProxyTunerConfig.MaxRamCost,
            Get = () => _ramMb,
            Set = v =>
            {
                _ramMb = (int)Math.Floor(v + 0.5f);
                ramCost = _ramMb;   // 直接改运行中的占用（原版 SignalScramble 也这样动态改 ramCost）
            },
        });

        _rows.Add(new TuneRow
        {
            LabelKey = "PROXY_TUNER_ROW_HOLD",
            LabelFallback = "Hold (s)",
            LogFormat = "exit delay = {0}s.",
            Format = "0.#",
            Step = 1f,
            Min = ProxyTunerConfig.MinCompleteDelay,
            Max = ProxyTunerConfig.MaxCompleteDelay,
            Get = () => _hold,
            Set = v => _hold = v,
        });
    }

    /// <summary>值文本（面板显示与日志共用）。</summary>
    private static string RowValueText(TuneRow row)
        => row.Prefix + row.Get().ToString(row.Format, CultureInfo.InvariantCulture);

    /// <summary>点一次「-」/「+」：夹到范围、写回运行时字段、记一条 Info（一次调节一条）。</summary>
    private void StepRow(int index, float direction)
    {
        if (index < 0 || index >= _rows.Count) return;
        TuneRow row = _rows[index];

        float v = row.Get() + direction * row.Step;
        // 0.1 步进的行先吸附到步进网格：否则 0.35 ± 0.1 会累积出 0.2500001 这类浮点残留
        if (row.Step < 1f) v = (float)Math.Round(v / row.Step) * row.Step;
        v = MathHelper.Clamp(v, row.Min, row.Max);
        row.Set(v);

        _flashRow = index;
        _flashTimer = FlashSeconds;
        KELog.Info($"{LogTag} " + string.Format(CultureInfo.InvariantCulture, row.LogFormat, RowValueText(row)));
    }

    /// <summary>Reset：把全部可调项恢复成配置里的默认值（不写盘）。</summary>
    private void ResetAll()
    {
        RestoreFromConfig();
        _flashRow = -1;
        _flashTimer = 0f;
        KELog.Info($"{LogTag} tuning values reset to the config defaults ('{_cfg.SourceName}'); nothing was written to disk.");
    }

    // ======================= Update =======================

    /// <summary>每帧：推进倒计时 / 动画，并把过载倍数作用到目标机的代理上。</summary>
    public override void Update(float t)
    {
        base.Update(t);
        if (isExiting) return;

        if (_flashTimer > 0f) _flashTimer -= t;

        // 强调色逐帧刷新（动态色随时间变化）
        RefreshAccent();

        switch (_phase)
        {
            case Phase.Locked:
            case Phase.Closing:
                _phaseTimer -= t;
                if (_phaseTimer <= 0f) isExiting = true;
                break;

            case Phase.Running:
                _animTimer += t * _animSpeed;
                DriveProxyOverload(t);
                break;
        }
    }

    /// <summary>
    /// 把过载倍数作用到**当前连接机器**的代理上（本程序的自动过载驱动器）。
    /// 原版推进过载是 <c>proxyOverloadTicks -= t</c>（1 倍速，见原版 shell 的 Overload），
    /// 这里按倍数推进 —— 所以 1.0 = 原版速度、2.0 = 两倍速、0 = 冻结（便于观察起始状态）。
    /// 只动「正在运行代理」的机器；代理被过载掉（proxyActive = false）后自然停止。
    /// </summary>
    private void DriveProxyOverload(float t)
    {
        Computer target = os.connectedComp;
        if (target == null || !target.hasProxy || !target.proxyActive) return;
        if (_overloadMul <= 0f) return;

        target.proxyOverloadTicks -= t * _overloadMul;
        if (target.proxyOverloadTicks <= 0f)
        {
            // 与原版 shell 的过载完成保持一致：归零并把代理标记为已被过载
            target.proxyOverloadTicks = 0f;
            target.proxyActive = false;
            KELog.Info($"{LogTag} proxy overload completed on '{target.idName}' driven at "
                + $"x{_overloadMul.ToString("0.##", CultureInfo.InvariantCulture)}.");
        }
    }

    // ======================= Draw =======================

    /// <summary>绘制窗口。锁定态只显示提示，其余两态都画调参界面。</summary>
    public override void Draw(float t)
    {
        base.Draw(t);
        drawOutline();
        drawTarget();

        if (_phase == Phase.Locked)
        {
            DrawLocked();
            return;
        }
        DrawTuner();
    }

    /// <summary>锁定 / 加载失败的提示页（居中一行 + 倒计时）。</summary>
    private void DrawLocked()
    {
        int cx = bounds.X + bounds.Width / 2;
        int cy = bounds.Y + bounds.Height / 2;

        string msg = FitText(GuiData.smallfont, _lockedMessage, bounds.Width - 16);
        Vector2 size = GuiData.smallfont.MeasureString(msg);
        spriteBatch.DrawString(GuiData.smallfont, msg,
            new Vector2(cx - size.X / 2f, cy - size.Y / 2f), os.lockedColor, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);

        string closing = KELoc.Format("PROXY_TUNER_CLOSING", "Closing in {0}s", Math.Max(0, (int)Math.Ceiling(_phaseTimer)));
        Vector2 cs = GuiData.detailfont.MeasureString(closing);
        spriteBatch.DrawString(GuiData.detailfont, closing,
            new Vector2(cx - cs.X / 2f, cy + size.Y), Color.White * 0.5f, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);
    }

    /// <summary>调参界面：左上程序名 + 状态，中部动画区，右侧可调项，底部 Reset / Exit。</summary>
    private void DrawTuner()
    {
        int innerX = bounds.X + 2;
        int innerW = Math.Max(40, bounds.Width - 4);
        int contentTop = bounds.Y + PANEL_HEIGHT + 2;
        int contentBottom = bounds.Y + bounds.Height - 2;

        // ---- 左上：程序名 / 标题 ----
        string title = FitText(GuiData.smallfont, name, innerW);
        spriteBatch.DrawString(GuiData.smallfont, title, new Vector2(innerX, contentTop), _accent,
            0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);

        // ---- 动画与面板的几何（窗口可被拖动缩放，全部按当前 bounds 计算并夹紧）----
        int statusY = contentTop + 16;
        int bodyTop = statusY + 24;                       // 状态占两行
        int btnY = contentBottom - 22;
        int bodyH = Math.Max(40, btnY - 4 - bodyTop);
        int animW = Math.Max(40, Math.Min((int)(innerW * 0.38f), Math.Max(40, innerW - 132)));
        int panelX = innerX + animW + 4;
        int panelW = Math.Max(60, innerX + innerW - panelX);

        DrawStatus(innerX, statusY, innerW);

        // ---- 中部：动画区 ----
        DrawAnimation(new Rectangle(innerX, bodyTop, animW, bodyH));

        // ---- 右侧：可调项 ----
        int rowH = Math.Max(12, Math.Min(bodyH / Math.Max(1, _rows.Count), 22));
        for (int i = 0; i < _rows.Count; i++)
        {
            int rowY = bodyTop + i * rowH;
            if (rowY + rowH > bodyTop + bodyH + 1) break;   // 窗口太矮时只画放得下的行
            DrawTuneRow(i, panelX, rowY, panelW, rowH);
        }

        // ---- 底部：Reset / Exit ----
        int btnW = 62;
        int half = innerW / 2;
        if (_phase == Phase.Closing)
        {
            string closing = KELoc.Format("PROXY_TUNER_CLOSING", "Closing in {0}s", Math.Max(0, (int)Math.Ceiling(_phaseTimer)));
            spriteBatch.DrawString(GuiData.detailfont, closing, new Vector2(innerX, btnY + 6), Color.White * 0.7f,
                0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);
            if (Button.doButton(PID + IdExit, innerX + innerW - btnW, btnY, btnW, 20,
                    KELoc.Loc("PROXY_TUNER_BTN_SKIP", "Skip"), os.lockedColor))
                isExiting = true;
        }
        else
        {
            if (Button.doButton(PID + IdReset, innerX + half - btnW - 3, btnY, btnW, 20,
                    KELoc.Loc("PROXY_TUNER_BTN_RESET", "Reset"), os.lockedColor))
                ResetAll();
            if (Button.doButton(PID + IdExit, innerX + half + 3, btnY, btnW, 20,
                    KELoc.Loc("PROXY_TUNER_BTN_EXIT", "Exit"), os.lockedColor))
                RequestExit();
        }
    }

    /// <summary>状态两行：目标机 / 代理状态（含实时 tick 数），以及倍数项到底影响什么。</summary>
    private void DrawStatus(int x, int y, int width)
    {
        Color dim = Color.White * 0.65f;
        Computer target = os.connectedComp;

        string line1;
        if (target == null)
        {
            line1 = KELoc.Loc("PROXY_TUNER_NO_TARGET", "No connection - proxy tuning inactive");
        }
        else
        {
            string state = target.proxyActive
                ? KELoc.Loc("PROXY_TUNER_PROXY_ACTIVE", "ACTIVE")
                : KELoc.Loc("PROXY_TUNER_PROXY_BYPASSED", "BYPASSED");
            line1 = KELoc.Format("PROXY_TUNER_TARGET_LINE", "Target {0} - proxy {1} ({2}/{3} ticks)",
                target.ip,
                target.hasProxy ? state : KELoc.Loc("PROXY_TUNER_PROXY_NONE", "none"),
                target.proxyOverloadTicks.ToString("0.0", CultureInfo.InvariantCulture),
                Math.Max(0f, target.startingOverloadTicks).ToString("0.0", CultureInfo.InvariantCulture));
        }
        spriteBatch.DrawString(GuiData.detailfont, FitText(GuiData.detailfont, line1, width),
            new Vector2(x, y), dim, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);

        // 倍数项实际影响的是「代理过载速度」（本程序按倍数自动推进过载）—— 明确写出来
        string line2 = KELoc.Format("PROXY_TUNER_DRIVE_HINT",
            "Auto-overload: proxyOverloadTicks -= dt * x{0} (1.0 = vanilla rate)",
            _overloadMul.ToString("0.##", CultureInfo.InvariantCulture));
        spriteBatch.DrawString(GuiData.detailfont, FitText(GuiData.detailfont, line2, width),
            new Vector2(x, y + 12), _accent * 0.85f, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);
    }

    /// <summary>动画区：原版 DepthDotGridEffect（纹理 "Circle" 来自游戏本体，无需自带资源）。</summary>
    private void DrawAnimation(Rectangle area)
    {
        spriteBatch.Draw(Utils.white, area, new Color(10, 10, 10));
        if (_grid == null || area.Width < 12 || area.Height < 12) return;

        // 间距再兜一次底：调参面板不能把渲染循环逼成死循环
        float sep = MathHelper.Clamp(_dotSep, ProxyTunerConfig.MinDotSeparation, ProxyTunerConfig.MaxDotSeparation);

        switch (_cfg.AnimationStyle)
        {
            // 目前只有 DepthDotGrid 一种形态；AnimationStyle 是为后续换形态预留的配置位
            case ProxyTunerConfig.StyleDepthDotGrid:
            default:
                _grid.DrawGrid(area, Vector2.Zero, spriteBatch, PixelsInOnRecurse, _depth, _accent,
                    sep, _dotSize, MaxDepthEffectDistance, _animTimer, _chaos);
                break;
        }
    }

    /// <summary>画一行可调项：名称 + 当前值 +「-」「+」。</summary>
    private void DrawTuneRow(int index, int x, int y, int width, int rowH)
    {
        TuneRow row = _rows[index];
        bool flash = _flashRow == index && _flashTimer > 0f;
        Color valueColor = flash ? Color.White : _accent;

        int btnW = 18;
        int btnH = Math.Max(12, Math.Min(18, rowH - 2));
        int btnY = y + (rowH - btnH) / 2;
        int plusX = x + width - btnW;
        int minusX = plusX - btnW - 2;
        int valueRight = minusX - 4;

        // 名称（放不下就截断，避免压到数值）
        string label = KELoc.Loc(row.LabelKey, row.LabelFallback);
        string value = RowValueText(row);
        float valueW = GuiData.detailfont.MeasureString(value).X;
        float labelMax = Math.Max(20f, valueRight - x - valueW - 4f);
        spriteBatch.DrawString(GuiData.detailfont, FitText(GuiData.detailfont, label, labelMax),
            new Vector2(x, y + (rowH - GuiData.detailfont.LineSpacing) / 2f),
            flash ? Color.White : Color.White * 0.8f, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);

        // 当前值（右对齐在按钮左侧）
        spriteBatch.DrawString(GuiData.detailfont, value,
            new Vector2(valueRight - valueW, y + (rowH - GuiData.detailfont.LineSpacing) / 2f),
            valueColor, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);

        //「-」「+」（用半角字符：游戏字体没有 U+2212 这类数学符号的字形）
        if (Button.doButton(PID + IdRowBase + index * 2, minusX, btnY, btnW, btnH, "-", os.lockedColor))
            StepRow(index, -1f);
        if (Button.doButton(PID + IdRowBase + index * 2 + 1, plusX, btnY, btnW, btnH, "+", os.lockedColor))
            StepRow(index, +1f);
    }

    /// <summary>按 Exit：<c>CompleteDelay</c> 秒的退出倒计时（0 = 立即退出），期间可 Skip。</summary>
    private void RequestExit()
    {
        if (_hold <= 0f)
        {
            isExiting = true;
            return;
        }
        _phase = Phase.Closing;
        _phaseTimer = _hold;
        KELog.Info($"{LogTag} exiting in {_hold.ToString("0.#", CultureInfo.InvariantCulture)}s (Hold).");
    }

    /// <summary>把文字裁到指定宽度（放不下时截断加 <c>..</c>），避免小窗口里文字互相重叠。</summary>
    private static string FitText(SpriteFont font, string text, float maxWidth)
    {
        if (string.IsNullOrEmpty(text) || maxWidth <= 0f) return string.Empty;
        if (font.MeasureString(text).X <= maxWidth) return text;
        for (int len = text.Length - 1; len > 0; len--)
        {
            string candidate = text.Substring(0, len) + "..";
            if (font.MeasureString(candidate).X <= maxWidth) return candidate;
        }
        return string.Empty;
    }
}
