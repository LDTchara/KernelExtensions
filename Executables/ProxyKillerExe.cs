using Hacknet;
using Hacknet.Gui;
using KernelExtensions.Configs;
using KernelExtensions.Managers;
using KernelExtensions.Utilities;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Pathfinder.Executable;

namespace KernelExtensions.Executables;

/// <summary>
/// ProxyKiller —— 把目标"压到过载"的**展示**程序。
///
/// <para>
/// 定位说清楚：**过载倍率是扩展作者在配置里定义的，本程序不调任何东西**。
/// 它做的事只有一件 —— 按配置把 RAM 从起始值线性爬到目标值，同时用一个背景动画
/// 把这个过程表达出来（粒子随爬升由混沌趋于整齐），并提供退出按钮。
/// 所以界面上**不显示任何文字与数字**：爬升本身看得见，再叠读数就是噪声。
/// </para>
///
/// <para>
/// RAM 爬升逻辑照搬原版 <c>DLCTraceSlower</c>（<c>SignalScramble</c>）——
/// 那是原版唯一一个"自己把 RAM 吃掉"的程序，也是同类表达的先例：
/// 它从 50 爬到 600，速率 200/s（约 2.75 秒）。本程序用同样的机制，
/// 但把目标值与速率都做成可配。
/// </para>
///
/// <para>
/// <c>chaosPercent</c> 随爬升从 1 降到 0：这就是"burst"在视觉上的全部表达 ——
/// 开始的粒子是乱的，被压住后趋于整齐。
/// </para>
/// </summary>
public class ProxyKillerExe : GameExecutable
{
    /// <summary>Locked 状态的停留时长（秒）——给玩家看清锁定的原因。</summary>
    private const float LOCKED_EXIT_SECONDS = 3f;

    private const string LogTag = "[ProxyKiller]";

    // ---- 运行时状态 ----
    private ProxyKillerConfig _cfg;
    private bool _locked;
    private string _lockedReason;
    private float _lockedTimer;

    /// <summary>当前实际 RAM 占用（浮点，爬升过程需要小数）。</summary>
    private float _ram;
    private float _ramStart;
    private float _target;
    private float _rampRate;

    /// <summary>启动时所在节点的 ip —— 用它判断玩家有没有走开（对齐原版跟着连接走的语义）。</summary>
    private string _homeIp;

    // ---- 颜色与背景 ----
    private Color _accent = Color.White;
    private string _accentStr;
    private Hacknet.Effects.DepthDotGridEffect _depthGrid;
    private Hacknet.Effects.HexGridBackground _hexGrid;
    private bool _bgWarned;

    private const int ID_EXIT = 910;

    public ProxyKillerExe() : base()
    {
        this.name = ProxyKillerConfig.DefaultProgramName;
        // ⚠️ 窗口顶部标题画的是 IdentifierName（原版 ExeModule.drawTarget："app:" + IdentifierName），
        // 不是 name —— 不设它就显示 app:UNKNOWN。
        this.IdentifierName = ProxyKillerConfig.DefaultProgramName;
        this.ramCost = ProxyKillerConfig.DefaultRamCost;
    }

    public override void OnInitialize()
    {
        base.OnInitialize();

        List<string> flags = FindConfigFlags();
        if (flags.Count != 1)
        {
            if (flags.Count == 0)
            {
                Lock(KELoc.Loc("PROXY_KILLER_LOCKED", "ProxyKiller: no configuration is armed."));
                KELog.Info($"{LogTag} no config flag present - locked.");
            }
            else
            {
                Lock(KELoc.Loc("PROXY_KILLER_MULTI_FLAG",
                    "ProxyKiller: multiple configurations are armed (authoring error)."));
                KELog.Error($"{LogTag} {flags.Count} config flags present, expected exactly one: "
                    + string.Join(", ", flags));
            }
            return;
        }

        _cfg = ProxyKillerConfig.LoadByFlag(flags[0]);
        if (_cfg == null)
        {
            Lock(KELoc.Loc("PROXY_KILLER_LOAD_FAILED",
                "ProxyKiller: configuration could not be loaded."));
            return;
        }

        // 程序名与注册名一致（作者可改显示名，注册名 #PROXYKILLER# 不受影响）
        if (!string.IsNullOrWhiteSpace(_cfg.ProgramName))
        {
            name = _cfg.ProgramName;
            IdentifierName = _cfg.ProgramName;
        }

        _accentStr = _cfg.AccentColorRaw;
        RefreshAccent();

        _ramStart = _cfg.RamCost;
        _target = Math.Max(_ramStart, _cfg.BurstTarget);   // 目标小于起始时就不爬，避免反向下滑
        _rampRate = _cfg.RampRate;
        _ram = _ramStart;
        ramCost = (int)_ram;

        _homeIp = CurrentTargetIp();

        KELog.Info($"{LogTag} '{_cfg.SourceName}' started: ram {_ramStart} -> {_target}MB "
            + $"at {_rampRate.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}MB/s.");
    }

    /// <summary>
    /// 找出存档里所有属于本程序的 flag（<c>ProxyKiller_*</c>）。
    ///
    /// ⚠️ 用**注入的 os 实例**取 flag（对齐 CustomTrialExe 的 <c>os.Flags</c>），
    /// 不用静态的 <c>OS.currentInstance</c> —— 后者是全局，不匹配时会让这里静默变成"没配置"。
    /// </summary>
    private List<string> FindConfigFlags()
    {
        List<string> found = new();
        ProgressionFlags pf = os?.Flags;
        if (pf == null)
        {
            KELog.Warn($"{LogTag} os.Flags is unavailable on this instance; treating as no config.");
            return found;
        }

        // ProgressionFlags 只有 HasFlag / GetFlagStartingWith（后者只给第一个），
        // 要检出多 flag 只能反射读内部 List；反射失败则退化为"取第一个匹配"。
        bool enumerated = false;
        try
        {
            if (_progressionFlagsField?.GetValue(pf) is List<string> all)
            {
                enumerated = true;
                foreach (string f in all)
                    if (f != null && f.StartsWith(ProxyKillerConfig.FlagPrefix, StringComparison.Ordinal))
                        found.Add(f);
            }
        }
        catch (Exception ex)
        {
            KELog.Warn($"{LogTag} could not enumerate flags ({ex.Message}); falling back to first match.");
        }

        if (!enumerated)
        {
            string one = pf.GetFlagStartingWith(ProxyKillerConfig.FlagPrefix);
            if (!string.IsNullOrEmpty(one)) found.Add(one);
        }

        KELog.Info($"{LogTag} flag scan: prefix='{ProxyKillerConfig.FlagPrefix}', "
            + $"enumerated={enumerated}, matched={found.Count}"
            + (found.Count > 0 ? $" [{string.Join(", ", found)}]" : string.Empty));
        return found;
    }

    private static readonly System.Reflection.FieldInfo _progressionFlagsField =
        typeof(ProgressionFlags).GetField("Flags",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

    private void Lock(string reason)
    {
        _locked = true;
        _lockedReason = reason;
        _lockedTimer = LOCKED_EXIT_SECONDS;
        os.write(reason);
    }

    /// <summary>当前所连节点的 ip（没连就是本机）——对齐原版 SignalScramble 的取法。</summary>
    private string CurrentTargetIp()
        => (os?.connectedComp ?? os?.thisComputer)?.ip;

    /// <summary>
    /// 刷新强调色。动态色（CC 预设 / Rainbow / Gradient）随时间变化，必须逐帧重算，
    /// 所以在 Update 里刷新一次缓存进字段，Draw 直接用。
    /// </summary>
    private void RefreshAccent()
        => _accent = CustomColorManager.GetDynamicColor(_accentStr, ProxyKillerConfig.DefaultAccentColor);

    // ======================= Update =======================

    public override void Update(float t)
    {
        base.Update(t);
        if (isExiting) return;

        if (_locked)
        {
            _lockedTimer -= t;
            if (_lockedTimer <= 0f) isExiting = true;
            return;
        }
        if (_cfg == null) { isExiting = true; return; }

        RefreshAccent();

        // 离开所在节点就关（可配，默认 true —— 对齐原版 SignalScramble 跟着连接走的语义）
        if (_cfg.ExitOnDisconnect && CurrentTargetIp() != _homeIp)
        {
            KELog.Info($"{LogTag} left the node this program was started on; exiting.");
            isExiting = true;
            return;
        }

        // RAM 线性爬升到目标；已到就不再动（原版 DLCTraceSlower 同款，只是速率与目标可配）
        if (_ram < _target && _rampRate > 0f)
        {
            _ram = Math.Min(_target, _ram + _rampRate * t);
            ramCost = (int)_ram;
        }
    }

    // ======================= Draw =======================

    public override void Draw(float t)
    {
        base.Draw(t);
        drawOutline();
        drawTarget();

        // 退出瞬间按钮立即消失（与 PortHackV2 锁定屏同一约定：不参与淡出）
        if (isExiting) return;

        Rectangle box = Utils.InsetRectangle(
            new Rectangle(bounds.X, bounds.Y + PANEL_HEIGHT, bounds.Width, bounds.Height - PANEL_HEIGHT), 2);
        if (box.Width <= 8 || box.Height <= 8) return;

        DrawBackground(box);

        if (_locked)
        {
            // 锁定时只有一行原因 —— 3 秒后自己关，不给按钮（没什么可操作的）
            if (!string.IsNullOrEmpty(_lockedReason))
                TextItem.doFontLabelToSize(
                    new Rectangle(box.X + 6, box.Y + box.Height / 2 - 10, box.Width - 12, 20),
                    _lockedReason, GuiData.smallfont, Color.White * 0.9f);
            return;
        }

        // 底部退出按钮（原版 SignalScramble 也是这么放的）。点一下立即退出。
        const int btnH = 25;
        Rectangle btn = new(box.X, box.Y + box.Height - btnH, box.Width, btnH);
        spriteBatch.Draw(Utils.white, btn, Color.Black * 0.9f);
        if (Button.doButton(PID + ID_EXIT, btn.X + 1, btn.Y, btn.Width - 2, btn.Height,
                KELoc.Loc("PROXY_KILLER_BTN_EXIT", "Exit"), os.lockedColor))
        {
            isExiting = true;
        }
    }

    /// <summary>
    /// 背景：原版现成的 Effects，不需要自带资源。
    /// 粒子"混沌度"随 RAM 爬升从 1 降到 0 —— 这就是 burst 的全部视觉表达。
    /// </summary>
    private void DrawBackground(Rectangle area)
    {
        if (_cfg == null || _cfg.BackgroundEffect == ProxyKillerBackgroundEffect.None) return;

        float progress = _target > _ramStart ? MathHelper.Clamp((_ram - _ramStart) / (_target - _ramStart), 0f, 1f) : 1f;
        float chaos = 1f - progress;
        Color c = _accent;

        try
        {
            switch (_cfg.BackgroundEffect)
            {
                case ProxyKillerBackgroundEffect.DepthDotGrid:
                    // 参数对齐原版 SignalScramble 的那次调用（30f, 10 层, 60f 间距, 10f 点）
                    _depthGrid ??= new Hacknet.Effects.DepthDotGridEffect(os.content);
                    _depthGrid.DrawGrid(area, Vector2.Zero, spriteBatch, 30f, 10, c, 60f, 10f, 0f, os.timer, chaos);
                    break;

                case ProxyKillerBackgroundEffect.ZoomingDotGrid:
                    Hacknet.Effects.ZoomingDotGridEffect.Render(area, spriteBatch, os.timer, c * 0.4f);
                    break;

                case ProxyKillerBackgroundEffect.HexGrid:
                    _hexGrid ??= new Hacknet.Effects.HexGridBackground(os.content);
                    _hexGrid.Update(1f / 60f);
                    _hexGrid.Draw(area, spriteBatch, Color.Black, c * 0.2f,
                        Hacknet.Effects.HexGridBackground.ColoringAlgorithm.CorrectedSinWash, 0f);
                    break;

                case ProxyKillerBackgroundEffect.Grid:
                    Hacknet.Effects.GridEffect.DrawGridBackground(area, spriteBatch, 12, c * 0.3f);
                    break;
            }
        }
        catch (Exception ex)
        {
            if (!_bgWarned)
            {
                _bgWarned = true;
                KELog.Warn($"{LogTag} background '{_cfg.BackgroundEffect}' failed: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 程序被移除时的收尾。
    /// 这里**不需要释放**背景效果：原版这些 Effects 都不实现 IDisposable，
    /// 它们用的纹理来自游戏本体的 ContentManager（由引擎统一管理）。
    /// 只把引用置空，随本实例一起被回收。
    /// </summary>
    public override void OnComplete()
    {
        _depthGrid = null;
        _hexGrid = null;
        base.OnComplete();
    }
}
