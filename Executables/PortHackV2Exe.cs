using Hacknet;
using Hacknet.Gui;
using KernelExtensions.Configs;
using KernelExtensions.Managers;
using KernelExtensions.Utilities;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Pathfinder.Executable;
using Pathfinder.Port;
using System.Reflection;

namespace KernelExtensions.Executables;

/// <summary>
/// PortHackV2 —— 原版 PortHack 的可配置重制版。
///
/// <para>
/// 入口是**存档 flag**（`PortHackV2_&lt;配置文件名&gt;`），不是命令行参数：
/// 程序启动即校验，没有欢迎页。恰好一个 flag → 加载 `<c>PortHackV2/&lt;名&gt;.xml</c>`；
/// 零个 → 锁定（没被剧本启用）；多个 → 作者配置错误（锁定 + 报错）。
/// </para>
///
/// <para>状态机：</para>
/// <list type="table">
///   <item><term>Locked</term><description>无 flag / 多 flag / 配置加载失败：提示 + 3s 自动退出</description></item>
///   <item><term>Listing</term><description>列前置要求与协议行，全满足后自动进 Cracking（未满足则每帧重查）</description></item>
///   <item><term>Cracking</term><description>多协议**并行**推进，各自按 StartDelay 启动、启动时掷一次 RNG</description></item>
///   <item><term>Complete</term><description>乱序字 + 上下白线展开的演出，CompleteDelay 后退出</description></item>
///   <item><term>Failed</term><description>全部协议都没破开时的收尾演出</description></item>
/// </list>
///
/// <para>
/// 时间语义：根级 `Time` 是 Cracking 的**总时长**（底部进度条走它）。
/// 它是硬上限 —— 到期时仍未跑完的协议记为失败并记 Warn（说明作者没排好时间），
/// 但根级**没有 RNG**，所以总时长跑满即视为整体成功、走 Complete 演出。
/// 每个协议的成功/失败分支各自独立结算（自己的 OnComplete / OnFail 在自己的时长跑完时立即执行）。
/// </para>
/// </summary>
public class PortHackV2Exe : GameExecutable
{
    /// <summary>Locked 状态的停留时长（秒）——给玩家看清锁定的原因。</summary>
    private const float LOCKED_EXIT_SECONDS = 3f;

    /// <summary>失败演出的停留时长（秒）。</summary>
    private const float FAILED_EXIT_SECONDS = 3f;

    public PortHackV2Config CurrentConfig { get; private set; }
    public PortHackV2State CurrentState { get; private set; } = PortHackV2State.Locked;

    /// <summary>程序状态。</summary>
    public enum PortHackV2State
    {
        /// <summary>没被启用 / 配置错误：提示后自动退出。</summary>
        Locked,
        /// <summary>列前置要求与协议，等待满足。</summary>
        Listing,
        /// <summary>破解进行中。</summary>
        Cracking,
        /// <summary>成功演出。</summary>
        Complete,
        /// <summary>中途中断（断连 / 离开目标节点）的收尾演出。</summary>
        Failed
    }

    /// <summary>
    /// 单个协议的运行时状态。
    /// 「启动时掷一次 RNG」意味着分支在协议启动那一刻就定下，但**演出上不暴露** ——
    /// 两条分支的进度条与动画完全一样，只是时长不同。
    /// </summary>
    private sealed class ProtocolRuntime
    {
        /// <summary>实例唯一标识（排错用：区分"_runtimes 被重建过"这类情况）。</summary>
        private static int _nextId;
        public readonly int Id = ++_nextId;

        public PortHackV2ProtocolConfig Def;
        /// <summary>StartDelay 计时（Cracking 开始后累加），到点才启动本协议。</summary>
        public float DelayElapsed;
        /// <summary>本协议启动后经过的时间。</summary>
        public float Elapsed;
        /// <summary>本协议是否已成功（决定根级结算时开不开端口、给不给 admin）。</summary>
        public bool Succeeded;
        /// <summary>
        /// 是否已执行过"启动瞬间"的一次性初始化（掷 RNG / OnStart 钩子 / started 日志）。
        ///
        /// <para>
        /// ⚠️ 必须独立于 <see cref="Started"/>：后者是 <c>DelayElapsed &gt;= StartDelay</c> 的
        /// **状态判断**，在 <c>StartDelay = 0</c> 时第一帧就为 true —— 拿它当"还没初始化"的判据
        /// 会让初始化整块被跳过（实机 bug：delay=0 的协议从不执行 OnStart）。
        /// </para>
        /// </summary>
        public bool Initialized;

        public bool Started => Def != null && DelayElapsed >= Def.StartDelay;
        public bool Finished { get; set; }

        /// <summary>本协议当前分支的总时长（秒）。</summary>
        public float Duration => Succeeded ? Def.TimeIfSuccess : Def.TimeIfFail;

        /// <summary>本协议进度 0~1；时长为 0 或负 → 瞬满。</summary>
        public float Progress
        {
            get
            {
                if (Def == null || Finished) return 1f;
                float d = Duration;
                if (d <= 0f) return 1f;
                return MathHelper.Clamp(Elapsed / d, 0f, 1f);
            }
        }

        public bool IsDone => Finished || (Started && Duration <= 0f) || (Started && Elapsed >= Duration);
    }

    // ---------- 运行时状态 ----------
    private readonly List<ProtocolRuntime> _runtimes = new();
    private float _stateTimer;          // Locked / Failed 的退出倒计时
    private float _crackTime;           // Cracking 已进行时长（根级 Time 的对照量）
    private float _completeElapsed;     // Complete 演出已进行时长（也驱动白线展开）
    private int _successCount;          // 已成功协议数
    private bool _rootHooksFired;       // 根级结算只做一次
    private static readonly Random _rng = new();    // 各协议启动时的成功率掷点

    // ---------- 动画 ----------
    private readonly KPolyhedronSequence _sequence = new();
    private RenderTarget2D _rt;         // 3D 动画的离屏画布
    private SpriteBatch _rtBatch;       // 画进 _rt 用的专用批（主 spriteBatch 由 OS 持有，不能借用）

    // ---------- 界面几何 ----------
    private int InnerX => bounds.X + 2;
    private int InnerW => bounds.Width - 4;
    private int ContentTop => bounds.Y + PANEL_HEIGHT + 2;
    private int ContentBottom => bounds.Y + bounds.Height - 4;

    // Button ID（同 exe 内唯一）
    private const int ID_EXIT = 300;
    private const int ID_CANCEL = 301;

    public PortHackV2Exe() : base()
    {
        this.name = "PortHackV2";
        // ⚠️ 窗口顶部那行标题画的是 IdentifierName（见原版 ExeModule.drawTarget：
        // "app:" + IdentifierName），不是 name —— 不设它就显示 app:UNKNOWN。
        this.IdentifierName = "PortHackV2";
        // 配置加载前先用默认值（与原版 PortHack 同为 246MB）；OnInitialize 里再按 <RamCost> 覆盖
        this.ramCost = PortHackV2Config.DefaultRamCost;
    }

    public override void OnInitialize()
    {
        base.OnInitialize();

        List<string> flags = FindConfigFlags();
        if (flags.Count != 1)
        {
            // 0 个 = 没被剧本启用；多个 = 作者配置错误。两种都进锁定屏，但原因不同
            if (flags.Count == 0)
            {
                // 原因只进日志：锁定屏不显示（见 DrawLocked 的说明）
                KELog.Info("[PortHackV2] no config flag present - locked.");
            }
            else
            {
                KELog.ErrorT($"[PortHackV2] {flags.Count} config flags present, expected exactly one: "
                    + string.Join(", ", flags));
                os.write("PortHackV2: multiple configurations are armed (authoring error).");
            }
            EnterLocked();
            return;
        }

        CurrentConfig = PortHackV2Config.LoadByFlag(flags[0]);
        WarnUnregisteredProtocols(CurrentConfig);
        if (CurrentConfig == null)
        {
            // 具体原因 LoadByFlag 内部已经记进日志（含文件路径与异常）；这里只给玩家一句
            os.write("PortHackV2: configuration could not be loaded.");
            EnterLocked();
            return;
        }

        ramCost = CurrentConfig.RamCost;

        // 动画参数交给序列；每帧还会按总时长重算一次时间轴，所以中途改配置也能立刻生效
        _sequence.Shape = CurrentConfig.PolyhedronShape;
        _sequence.Mode = CurrentConfig.PolyhedronStyle;
        _sequence.SpinSpeed = CurrentConfig.SpinSpeed;
        _sequence.IdleSpinSpeed = CurrentConfig.IdleSpinSpeed;
        _sequence.SpinUpSeconds = CurrentConfig.SpinUpSeconds;
        _sequence.SpinUpRatio = CurrentConfig.SpinUpRatio;
        _sequence.SpinDownSeconds = CurrentConfig.SpinDownSeconds;

        _accent = CurrentConfig.AccentColor;
        _accentStr = CurrentConfig.AccentColorRaw;
        _progressStr = CurrentConfig.ProgressColorRaw;
        _bgStr = CurrentConfig.BackgroundColorRaw;

        CurrentState = PortHackV2State.Listing;
    }

    /// <summary>
    /// 找出存档里所有属于本程序的 flag（`PortHackV2_*`）。
    /// 正常情况下应恰好一个（flag = 入口牌）；0 个 → 锁定；多个 → 作者配置错误。
    ///
    /// ⚠️ 用**注入的 os 实例**取 flag（对齐 CustomTrialExe 的 <c>os.Flags</c>），
    /// 不用静态的 <c>OS.currentInstance</c>：那是个全局，在它没被赋值 / 指向别的实例时，
    /// 会让这里静默变成"没配置"，而真正原因（找错了对象）完全看不出来。
    /// </summary>
    private List<string> FindConfigFlags()
    {
        List<string> found = new();
        ProgressionFlags pf = os?.Flags;
        if (pf == null)
        {
            KELog.Warn("[PortHackV2] os.Flags is unavailable on this instance; treating as no config.");
            return found;
        }

        // ProgressionFlags 只暴露 HasFlag / GetFlagStartingWith（后者仅返回第一个），
        // 没有"枚举全部"的公开 API；要检出多个 flag 只能反射读内部 List。
        // 反射失败时退化为"取第一个匹配"——仍能工作，只是检不出多 flag 的情形。
        bool enumerated = false;
        List<string> all = TryReadProgressionFlags(pf);
        if (all != null)
        {
            enumerated = true;
            foreach (string f in all)
                if (f != null && f.StartsWith(PortHackV2Config.FlagPrefix, StringComparison.Ordinal))
                    found.Add(f);
        }

        if (!enumerated)
        {
            // 反射不可用（成员改名 / 取不到）：退化为"取第一个匹配"，至少单 flag 能跑
            KELog.Warn("[PortHackV2] could not enumerate flags (ProgressionFlags.Flags not readable); "
                + "falling back to first match - multiple config flags will NOT be detected.");
            string one = pf.GetFlagStartingWith(PortHackV2Config.FlagPrefix);
            if (!string.IsNullOrEmpty(one)) found.Add(one);
        }

        // 诊断：作者排查「挂了 flag 却说没配置」时，这条日志能直接区分
        // 「flag 没进存档 / 前缀写错 / 反射失败」三种情况
        KELog.Info($"[PortHackV2] flag scan: prefix='{PortHackV2Config.FlagPrefix}', "
            + $"enumerated={enumerated}, matched={found.Count}"
            + (found.Count > 0 ? $" [{string.Join(", ", found)}]" : string.Empty));
        return found;
    }

    // Pathfinder 的 ProgressionFlags.Flags 是**公开成员**（属性或字段皆有可能）。
    // ⚠️ 旧实现只找 NonPublic **字段**，两种都对不上 → GetField 返回 null →
    //    enumerated=False → 退化成"只取第一个"，**多 flag 永远检不出来**。
    //    2026-10-17 实机抓到（日志 enumerated=False，但存档里确实有 2 个 flag）。
    // 这里属性/字段、公开/非公开都试，任一命中即可。
    private static readonly System.Reflection.PropertyInfo _progressionFlagsProp =
        typeof(ProgressionFlags).GetProperty("Flags",
            System.Reflection.BindingFlags.Instance
            | System.Reflection.BindingFlags.Public
            | System.Reflection.BindingFlags.NonPublic);

    private static readonly FieldInfo _progressionFlagsField = typeof(ProgressionFlags)
        .GetField("Flags",
            System.Reflection.BindingFlags.Instance
            | System.Reflection.BindingFlags.Public
            | System.Reflection.BindingFlags.NonPublic);

    /// <summary>读出全部 flag；两种反射都失败返回 null（调用方退化为"取第一个"）。</summary>
    private static List<string> TryReadProgressionFlags(ProgressionFlags pf)
    {
        try
        {
            if (_progressionFlagsProp?.GetValue(pf) is List<string> viaProp) return viaProp;
        }
        catch { /* 属性读取异常 → 试字段 */ }
        try
        {
            if (_progressionFlagsField?.GetValue(pf) is List<string> viaField) return viaField;
        }
        catch { /* 字段读取异常 → 放弃 */ }
        return null;
    }

    private void EnterLocked()
    {
        CurrentState = PortHackV2State.Locked;
        _stateTimer = LOCKED_EXIT_SECONDS;
    }

    // ======================= 前置条件 =======================

    /// <summary>
    /// 目标机：**按启动时捕获的 IP** 解析，而不是"当前所连的机器"。
    ///
    /// <para>
    /// ⚠️ 这里以前写成 <c>os.connectedComp ?? os.thisComputer</c>，于是**玩家断连后
    /// <c>Target</c> 会变成玩家自己的机器** —— 而那台上通常所有端口都开着、没防火墙、
    /// 没代理、端口全破解过，于是 <see cref="AllPrerequisitesMet"/> 判为"全部满足"，
    /// Listing 屏顶部那行就会翻成「没有前置要求，准备开始…」，而程序实际上正在退出、
    /// 从没进过 Cracking。改成按 IP 解析后，断连时 <c>Target</c> 为 <c>null</c>，
    /// 前置判断如实为"未满足"。
    /// </para>
    ///
    /// <para>
    /// <c>?? os.thisComputer</c> 那个回退只在**开机首帧 <c>_targetIp</c> 还没捕获**时
    /// 还有意义（那时玩家必然刚连着目标机），所以保留它兜底。
    /// </para>
    /// </summary>
    private Computer Target
    {
        get
        {
            if (_targetIp != null)
            {
                try
                {
                    Computer byIp = Pathfinder.Util.ComputerLookup.FindByIp(_targetIp, false);
                    if (byIp != null) return byIp;
                }
                catch { }
                // IP 已知但在网络里找不到 ⇒ 目标已不可达，**不**回退到自己的机器
                return null;
            }
            return os.connectedComp ?? os.thisComputer;
        }
    }

    /// <summary>目标机当前的连接是否仍是本程序启动时那台（断连即停，对齐原版语义）。</summary>
    private bool TargetStillReachable()
    {
        Computer cur = os.connectedComp ?? os.thisComputer;
        return cur != null && cur.ip == _targetIp;
    }

    private string _targetIp;

    /// <summary>某个前置开放端口是否已满足。</summary>
    private bool PortRequirementMet(string protocol)
    {
        try { return Target.isPortOpen(protocol); }
        catch { return false; }
    }

    /// <summary>
    /// 防火墙前置是否**需要显示**：配置要求开且目标机确实有防火墙。
    /// 目标机没有防火墙时该行不显示 —— 避免列出玩家无法满足、也无意义的条件。
    /// </summary>
    private bool FirewallRowVisible()
        => CurrentConfig != null && CurrentConfig.RequireSolveFirewall
           && Target != null && Target.firewall != null;

    /// <summary>代理前置是否需要显示：配置要求开且目标机确实有代理。</summary>
    private bool ProxyRowVisible()
        => CurrentConfig != null && CurrentConfig.RequireProxyOverload
           && Target != null && Target.hasProxy;

    private bool FirewallRequirementMet()
        => !FirewallRowVisible() || Target.firewall.solved;

    private bool ProxyRequirementMet()
        => !ProxyRowVisible() || !Target.proxyActive;

    /// <summary>
    /// 目标机上已破解的端口数（Pathfinder 下端口以 PortState 表的 Cracked 为准）。
    /// </summary>
    private int CrackedPortCount()
    {
        try { return Target.GetAllPortStates().Count(s => s.Cracked); }
        catch { return 0; }
    }

    /// <summary>
    /// "已破解端口数"前置的**要求值**：auto = 目标节点自身的要求
    /// （照搬原版 <c>max(portsNeededForCrack + 1, 0)</c>，即原版 PortHack 的判定）；
    /// 手写数字 = 直接用作者给的 N。
    /// 返回 &lt;= 0 表示这项不检查。
    /// </summary>
    private int RequiredCrackedPorts()
    {
        if (CurrentConfig.RequireCrackedPortsAutoMode)
        {
            try { return Math.Max(Target.portsNeededForCrack + 1, 0); }
            catch { return 0; }
        }
        return CurrentConfig.RequireCrackedPorts;
    }

    /// <summary>已破解端口数前置是否需要显示：要求 &gt; 0 且目标机确实有端口可破。</summary>
    private bool CrackedPortsRowVisible()
    {
        if (RequiredCrackedPorts() <= 0) return false;
        return CrackedPortCount() > 0 || Target.GetAllPortStates().Count > 0;
    }

    private bool CrackedPortsRequirementMet()
        => CrackedPortCount() >= RequiredCrackedPorts();

    /// <summary>
    /// 所有前置与协议前置端口是否都满足（Listing → Cracking 的判据）。
    /// 「能不能破解」**完全由这些运行时前置决定**，不由配置里的开关决定 ——
    /// 作者要表达"现在还破解不了"，就用 <c>Require*</c> 前置（它会每帧重查、满足即自动开跑），
    /// 而不是一个写死的禁用位（那种位会让程序永远停在 Listing 且玩家无从解除）。
    /// 需要"某段剧情期间才允许"时，应由扩展**挂/摘入口 flag** 来控制。
    /// </summary>
    private bool AllPrerequisitesMet()
    {
        if (CurrentConfig == null) return false;
        if (!FirewallRequirementMet()) return false;
        if (!ProxyRequirementMet()) return false;
        if (!CrackedPortsRequirementMet()) return false;
        foreach (string p in CurrentConfig.RequireOpenPortsBefore)
            if (!PortRequirementMet(p)) return false;
        return true;
    }

    // ======================= Cracking =======================

    private void EnterCracking()
    {
        CurrentState = PortHackV2State.Cracking;
        _crackTime = 0f;
        _runtimes.Clear();
        _successCount = 0;

        foreach (PortHackV2ProtocolConfig p in CurrentConfig.Protocols)
        {
            if (p == null || string.IsNullOrWhiteSpace(p.Name)) continue;
            _runtimes.Add(new ProtocolRuntime { Def = p });
        }

        // 保留一条汇总 Debug：协议何时启动、各自延迟多少，是排查时序问题的第一手信息。
        // （"某协议从不启动"这类问题只能靠它区分 —— 名字、StartDelay 解析值、Def 是否为 null。）
        foreach (ProtocolRuntime rt in _runtimes)
        {
            KELog.Debug($"[PortHackV2] '{CurrentConfig.SourceName}' runtime #{rt.Id}: name='{rt.Def?.Name}'"
                + $" startDelay={rt.Def?.StartDelay ?? -1f:0.###}"
                + $" timeIfSuccess={rt.Def?.TimeIfSuccess ?? -1f:0.###}"
                + $" timeIfFail={rt.Def?.TimeIfFail ?? -1f:0.###}");
        }

        string source = CurrentConfig.SourceName;
        SafeHook(() => CurrentConfig.OnStart?.Execute(os, source, "OnStart"), source, "OnStart");

        // 全局：破解一开始就算一次敌对行为（对齐原版 Shell 过载那种"动手了"的信号）
        MarkHostile("cracking started");

        KELog.Info($"[PortHackV2] '{source}': cracking started with {_runtimes.Count} protocol(s), "
            + $"root time {CurrentConfig.Time}s.");
    }

    /// <summary>
    /// 执行一个动作点，并**保证它的失败不会打断调用方**。
    ///
    /// <para>
    /// 为什么必须包：动作点由作者配置，可能引用不存在的文件或写错内容；而调用点位于
    /// <c>OS.Update</c> 驱动的流程里（例如"协议启动"）。一旦异常冒出去会被上层吞掉，
    /// 结果是**协议永远不启动、而且没有任何日志** —— 实机出现过这个症状，极难定位。
    /// 这里统一兜底并记 Error（附完整异常）。
    /// </para>
    /// </summary>
    /// <param name="run">要执行的动作点调用。</param>
    /// <param name="source">日志定位用。</param>
    /// <param name="point">动作点名（如 OnStart）。</param>
    private static void SafeHook(Action run, string source, string point)
    {
        try
        {
            run();
        }
        catch (Exception ex)
        {
            KELog.ErrorT($"[PortHackV2] '{source}': <{point}> threw but was contained: {ex}");
        }
    }

    /// <summary>
    /// 记一次对目标机的敌对行为。
    ///
    /// <para>
    /// <c>Computer.hostileActionTaken()</c> 是原版"你正在动这台机器"的信号：
    /// <c>traceTime &gt; 0</c> 时**启动追踪倒计时**，并每隔 0.35 秒在节点上闪一个红圈。
    /// 原版 <c>ShellExe</c> 在代理过载的每一帧都调它，所以"动手"是会立刻被察觉的。
    /// </para>
    ///
    /// <para>
    /// 本程序只在**两个关键时刻**各调一次：全局破解开始、以及各协议各自启动时。
    /// 不每帧调 —— 那样只会把 `timeLastPinged` 一直刷满、红圈闪个不停，
    /// 而追踪倒计时本来就是 <c>traceTracker.start()</c> 一次性的，重复调用没有额外含义。
    /// </para>
    ///
    /// <para>
    /// 该方法内部只对"当前所连的那台"生效（<c>os.connectedComp.ip == ip</c> 才做事），
    /// 所以目标机不是当前连接时调用是安全的空操作。
    /// </para>
    /// </summary>
    private void MarkHostile(string what)
    {
        try
        {
            Computer t = Target;
            if (t == null) return;
            t.hostileActionTaken();
            KELog.Debug($"[PortHackV2] hostile action taken on '{t.ip}' ({what}).");
        }
        catch (Exception ex)
        {
            KELog.Warn($"[PortHackV2] could not mark hostile action: {ex.Message}");
        }
    }

    /// <summary>
    /// 推进单个协议：StartDelay 到点 → **掷一次 RNG**（分支就此定下）→ 跑分支时长 →
    /// 到点立刻结算本协议（自己的 OnComplete / OnFail），不等其它协议。
    ///
    /// <para>
    /// ⚠️ **初始化必须用独立的标志位判定，不能挂在 <c>if (!rt.Started)</c> 上。**
    /// <c>Started</c> 的定义是 <c>DelayElapsed &gt;= StartDelay</c>，而 <c>StartDelay = 0</c> 时
    /// 它在**第一帧就是 true** —— 于是 <c>if (!rt.Started)</c> 整块被跳过，
    /// 掷 RNG、OnStart 钩子、MarkHostile、started 日志**一次都不执行**；
    /// 只有 <c>StartDelay &gt; 0</c> 的协议才会走到那块。实机症状是
    /// "delay=0 的协议从不执行 OnStart、也不记 started，却照常跑完流程"，
    /// 而且没有任何报错（因为根本没有异常）。
    /// </para>
    /// </summary>
    private void UpdateProtocol(ProtocolRuntime rt, float t)
    {
        if (rt.Finished) return;

        // ---- 1. 延迟计时 ----
        if (!rt.Started) rt.DelayElapsed += t;

        // ---- 2. 到点则**一次性初始化**（与是否已 Started 无关） ----
        if (!rt.Initialized && rt.Started)
        {
            rt.Initialized = true;

            // 启动瞬间掷一次：分支定下但不暴露（两条分支的演出完全一样，只有时长不同）
            rt.Succeeded = _rng.NextDouble() < rt.Def.SuccessChance;
            string protoSource = $"{CurrentConfig.SourceName}/{rt.Def.Name}";

            KELog.Debug($"[PortHackV2] '{CurrentConfig.SourceName}': protocol #{rt.Id} "
                + $"'{rt.Def.Name}' starting (delayElapsed={rt.DelayElapsed:0.###}).");

            // ⚠️ 钩子失败**绝不能**打断协议启动：调用方在 OS.Update 里，异常冒出去会被上层吞掉，
            // 于是本协议永远进不了 started、而且**连日志都没有**。
            SafeHook(() => rt.Def.OnStart?.Execute(os, protoSource, "OnStart"), protoSource, "OnStart");

            // 每个协议启动时也记一次敌对行为 —— 多协议是错开启动的，
            // 于是"动手"的信号会随着后续协议陆续到达而反复出现，而不是只在开头响一次。
            MarkHostile($"protocol '{rt.Def.Name}' started");

            KELog.Info($"[PortHackV2] '{protoSource}': started "
                + $"(success shape={rt.Succeeded}, duration={(rt.Succeeded ? rt.Def.TimeIfSuccess : rt.Def.TimeIfFail)}s).");
        }

        // 还没到启动点：本帧到此为止
        if (!rt.Started) return;

        rt.Elapsed += t;
        if (rt.IsDone) CompleteProtocol(rt, forced: false);
    }

    /// <summary>
    /// 结算一个协议：开通它自己的端口、执行它自己的动作点。
    /// <paramref name="forced"/> = true 表示是根级总时长到期被强制收尾（未跑完），记为失败并记 Warn。
    /// </summary>
    private void CompleteProtocol(ProtocolRuntime rt, bool forced)
    {
        if (rt.Finished) return;
        rt.Finished = true;
        if (forced) rt.Succeeded = false;

        string protoSource = $"{CurrentConfig.SourceName}/{rt.Def.Name}";

        // 结算来源与状态：排错时"某协议莫名完成"只能靠这条定位（是否被强制收尾、
        // 它此前有没有真正启动过、实例 id 是否与建成的那个一致）。
        KELog.Debug($"[PortHackV2] CompleteProtocol #{rt.Id} '{protoSource}'"
            + $" forced={forced} started={rt.Started}"
            + $" elapsed={rt.Elapsed:0.###} duration={rt.Duration:0.###}");

        if (rt.Succeeded)
        {
            _successCount++;
            try
            {
                if (Target.GetPortState(rt.Def.Name) != null)
                    Target.openPort(rt.Def.Name, os.thisComputer.ip);
            }
            catch (Exception ex)
            {
                KELog.WarnT($"[PortHackV2] '{protoSource}': could not open port: {ex.Message}");
            }
            SafeHook(() => rt.Def.OnComplete?.Execute(os, protoSource, "OnComplete"), protoSource, "OnComplete");
        }
        else
        {
            SafeHook(() => rt.Def.OnFail?.Execute(os, protoSource, "OnFail"), protoSource, "OnFail");
            if (forced)
                KELog.WarnT($"[PortHackV2] '{protoSource}': did not finish within the root <Time>; "
                    + "recorded as failed - check the timing in this configuration.");
        }

        KELog.Info($"[PortHackV2] '{protoSource}': finished (success={rt.Succeeded}).");
    }

    /// <summary>
    /// 根级结算：整体成功 = 根级总时长跑满（根级没有 RNG，故固定进成功演出）。
    /// 到期时仍未跑完的协议强制收尾（记为失败 + Warn）。
    /// 效果不在这里结算 —— 要做的事写在动作点里（见配置类的说明）。
    /// </summary>
    private void SettleRoot()
    {
        if (_rootHooksFired) return;
        _rootHooksFired = true;

        string source = CurrentConfig.SourceName;

        // 硬上限：总时长到期了还没结束的协议，收尾记为失败
        foreach (ProtocolRuntime rt in _runtimes)
            if (!rt.Finished) CompleteProtocol(rt, forced: true);

        // TakeAdmin 只看开关：根级没有整体失败，作者的开关语义就是"是否给管理员"；
        // 与协议成败无关（全灭也夺）——否则作者写 true 却因 RNG 全失败而静默不夺，难以排查。
        if (CurrentConfig.TakeAdmin)
        {
            try { os.takeAdmin(_targetIp); }
            catch (Exception ex) { KELog.WarnT($"[PortHackV2] '{source}': takeAdmin failed: {ex.Message}"); }
        }

        CurrentState = PortHackV2State.Complete;
        _completeElapsed = 0f;
        // 急刹从这一刻（根级 <Time> 跑满）才开始，而不是"匀速区间用完"时
        _sequence.CrackingFinished = true;
        _sequence.IdleSpin = true;      // 成功演出期间背景立体继续慢转
        SafeHook(() => CurrentConfig.OnComplete?.Execute(os, source, "OnComplete"), source, "OnComplete");
        KELog.Info($"[PortHackV2] '{source}': crack finished, {_successCount}/{_runtimes.Count} protocol(s) succeeded.");
    }

    // ======================= Update =======================

    public override void Update(float t)
    {
        base.Update(t);
        if (isExiting) return;

        // 目标机在程序启动那一刻就是"当前所连的机器"；此刻务必先捕获 IP，
        // 之后 Target 一律按这个 IP 解析（见 Target 的注释）。
        if (_targetIp == null) _targetIp = os.connectedComp?.ip ?? os.thisComputer?.ip;

        // 强调色逐帧刷新（动态色会随时间变化）；放在状态分支之前，所有状态都吃得到
        RefreshAccent();

        switch (CurrentState)
        {
            case PortHackV2State.Locked:
                _stateTimer -= t;
                if (_stateTimer <= 0f) isExiting = true;
                return;

            case PortHackV2State.Listing:
                // 前置条件可能从别处被满足（解防火墙 / 过载代理 / 开端口都是方法直改，没有事件源），
                // 所以每帧轮询；满足即自动进 Cracking。
                if (!TargetStillReachable()) { isExiting = true; return; }
                if (AllPrerequisitesMet()) EnterCracking();
                return;

            case PortHackV2State.Cracking:
                UpdateCracking(t);
                return;

            case PortHackV2State.Complete:
                _completeElapsed += t;
                // 总时长已跑满（_crackTime 不再增长），序列据此走急刹 → 慢转
                _sequence.Update(t, _crackTime, CurrentConfig.Time);
                if (_completeElapsed >= CurrentConfig.CompleteDelay) isExiting = true;
                return;

            case PortHackV2State.Failed:
                _stateTimer -= t;
                if (_stateTimer <= 0f) isExiting = true;
                return;
        }
    }

    private void UpdateCracking(float t)
    {
        // 断连即停（对齐原版）：这与 AutoExitOnLeave 是两回事
        if (!TargetStillReachable()) { isExiting = true; return; }

        // AutoExitOnLeave：玩家切走节点就关程序；关掉时走中断收尾（没跑完就中断）
        if (CurrentConfig.AutoExitOnLeave && os.connectedComp != null && os.connectedComp.ip != _targetIp)
        {
            KELog.Info($"[PortHackV2] '{CurrentConfig.SourceName}': left the target node; exiting.");
            EnterInterrupted();
            return;
        }

        _crackTime += t;
        foreach (ProtocolRuntime rt in _runtimes)
        {
            // 逐个包住：循环里任何一个协议抛异常都会**静默中断整个循环**，
            // 后面的协议这一帧乃至后续帧都不再被推进 —— 这种"某协议毫无动静"的症状
            // 极难从结果反推，所以这里把异常明确记下来。
            try
            {
                UpdateProtocol(rt, t);
            }
            catch (Exception ex)
            {
                KELog.Error($"[PortHackV2] '{CurrentConfig.SourceName}' protocol "
                    + $"'{rt.Def?.Name}': update threw (other protocols continue): {ex}");
            }
        }

        _sequence.Update(t, _crackTime, CurrentConfig.Time);

        // 总时长跑满 → 整体结算（根级没有 RNG，故固定走成功演出）
        if (_crackTime >= CurrentConfig.Time) SettleRoot();
    }

    /// <summary>
    /// 中途中断收尾（断连 / 玩家离开目标节点）。
    /// 注意这里**不跑任何根级动作点**：根级没有 RNG，也就没有"破解失败"这回事 ——
    /// 没破开的协议由它自己的 OnFail 表达，而且那个 OnFail 在它自己的时长跑完时就执行了。
    /// </summary>
    private void EnterInterrupted()
    {
        if (_rootHooksFired) return;
        _rootHooksFired = true;
        CurrentState = PortHackV2State.Failed;
        _stateTimer = FAILED_EXIT_SECONDS;
        KELog.Info($"[PortHackV2] '{CurrentConfig.SourceName}': interrupted before finishing.");
    }

    // ======================= Draw =======================

    /// <summary>
    /// 绘制分两段（对齐 CustomTrialExe 的结构）：
    /// <list type="number">
    ///   <item>背景层：铺满内容区（<c>BackgroundEffect</c>，可配 NONE）</item>
    ///   <item>状态层：各状态自己的内容</item>
    /// </list>
    /// 退出按钮单独画在最上层，且**只在 Locked / Listing 出现** ——
    /// Cracking 一旦开跑就不该让玩家按"退出"溜走（要中断就断开连接），
    /// Complete / 中断收尾是演出，本来也没有可退的东西。
    /// </summary>
    public override void Draw(float t)
    {
        base.Draw(t);
        drawOutline();
        // 原版窗口顶部那行（"IP: xxx" + "app:" + IdentifierName）。
        // drawTarget 读的是 ExeModule.targetIP **字段** —— 基类构造函数只把它设一次，
        // 那是原版的**特性**（shell 之类的程序要绑死在创建时所连的节点上，之后不跟随）。
        // 本程序语义不同：它针对的是**当前**目标机，所以要按当前目标机同步一次再画，
        // 否则切节点后 IP 行会停在启动那一刻。
        this.targetIP = _targetIp ?? Target?.ip ?? string.Empty;
        drawTarget();

        switch (CurrentState)
        {
            case PortHackV2State.Locked:
                DrawLocked();
                break;
            case PortHackV2State.Listing:
                DrawListing();
                break;
            case PortHackV2State.Cracking:
                DrawCracking();
                break;
            case PortHackV2State.Complete:
            case PortHackV2State.Failed:
                DrawOutro();
                break;
        }

        // 退出按钮只在 Locked / Listing 出现（Cracking 起不给退；演出中也没有可退的）。
        // ⚠️ 必须带 isExiting 守卫：否则按下之后按钮还会继续画半秒（淡出期间），
        // 而其余内容已经停画了 —— 与"退出瞬间整体停画"的约定不一致。
        if (!isExiting && CanShowExitButton()
            && Button.doButton(PID + ID_EXIT, bounds.X + bounds.Width - 62, bounds.Y + bounds.Height - 30,
                56, 24, KELoc.Loc("PORT_HACK_V2_EXIT", "Exit"), os.lockedColor))
        {
            isExiting = true;
        }
    }

    /// <summary>当前状态是否显示退出按钮（见 <see cref="Draw"/> 的说明）。</summary>
    private bool CanShowExitButton()
        => CurrentState == PortHackV2State.Locked || CurrentState == PortHackV2State.Listing;

    // ---- 背景层 ----

    /// <summary>
    /// 铺背景。用游戏本体现成的 Effects（都不需要自带资源）。
    /// <paramref name="applyFade"/> = true 时随面板淡出（Listing / Cracking / 演出）；
    /// false 时按原量绘制（锁定屏 —— 它整屏瞬时消失，见 <see cref="DrawLocked"/>）。
    /// </summary>
    private void DrawBackground(Rectangle area, bool applyFade = true)
    {
        if (CurrentConfig == null || area.Width <= 8 || area.Height <= 8) return;
        if (CurrentConfig.BackgroundEffect == PortHackV2BackgroundEffect.None) return;

        float a = applyFade ? fade : 1f;
        Color c = _bgColor;
        try
        {
            switch (CurrentConfig.BackgroundEffect)
            {
                case PortHackV2BackgroundEffect.ZoomingDotGrid:
                    Hacknet.Effects.ZoomingDotGridEffect.Render(area, spriteBatch, os.timer, c * (0.4f * a));
                    break;

                case PortHackV2BackgroundEffect.HexGrid:
                    _hexGrid ??= new Hacknet.Effects.HexGridBackground(os.content);
                    _hexGrid.Update(1f / 60f);
                    _hexGrid.Draw(area, spriteBatch, Color.Black * a, c * (0.2f * a),
                        Hacknet.Effects.HexGridBackground.ColoringAlgorithm.CorrectedSinWash, 0f);
                    break;

                case PortHackV2BackgroundEffect.Grid:
                    Hacknet.Effects.GridEffect.DrawGridBackground(area, spriteBatch, 12, c * (0.3f * a));
                    break;

                case PortHackV2BackgroundEffect.DepthDotGrid:
                    _depthGrid ??= new Hacknet.Effects.DepthDotGridEffect(os.content);
                    _depthGrid.DrawGrid(area, Vector2.Zero, spriteBatch, 1f, 3, c * (0.2f * a),
                        15f, 3f, 30f, os.timer * 0.2f, 0.3f);
                    break;
            }
        }
        catch (Exception ex)
        {
            // 背景画不出来不该影响破解流程；只报一次免得刷屏
            if (!_bgWarned)
            {
                _bgWarned = true;
                KELog.Warn($"[PortHackV2] background '{CurrentConfig.BackgroundEffect}' failed: {ex.Message}");
            }
        }
    }

    private Hacknet.Effects.HexGridBackground _hexGrid;
    private Hacknet.Effects.DepthDotGridEffect _depthGrid;
    private bool _bgWarned;

    // ---- Locked ----

    /// <summary>
    /// 锁定屏。
    ///
    /// <para>
    /// ⚠️ <b>只画「LOCKED」与倒计时，不画锁定原因。</b> 锁定的三种成因
    /// （无 flag / 多 flag / 配置加载失败）**全都是"要么还没到那段剧情、要么扩展作者写错了"** ——
    /// 前者玩家迟早会看到程序正常打开，后者玩家什么也做不了、只有作者该知道。
    /// 所以原因只进日志（各个锁定成因在触发处**各自**记了 <c>KELog</c>），
    /// 不往玩家窗口里塞 —— 那行字玩家看不懂也用不上，还抢了这屏唯一的视觉重心。
    /// </para>
    ///
    /// <para>
    /// ⚠️ **整屏内容都不参与淡出**：<c>isExiting</c> 一开始就整个停画
    /// —— 背景网格与文字在退出瞬间一起"啪"地消失（对齐 PhaseSwiftExe 的做法，
    /// 它整屏内容都不乘 <c>fade</c>），只留基类那层 shell 在淡出。
    /// 其余状态（Listing / Cracking / 演出）才走 <c>fade</c>。
    /// </para>
    /// </summary>
    private void DrawLocked()
    {
        if (isExiting) return;   // 退出瞬间：背景与文字一起停止绘制，不做淡出

        Rectangle area = ContentArea;
        DrawBackground(area, applyFade: false);

        string title = KELoc.Loc("PORT_HACK_V2_LOCKED_TITLE", "LOCKED");
        string closing = KELoc.Format("PORT_HACK_V2_CLOSING", "Closing in {0}s",
            Math.Max(1, (int)Math.Ceiling(_stateTimer)));

        // ⚠️ 位置必须**按内容区算**，不能写死偏移：本程序占用的内存（RamCost）由作者配置，
        // 而窗口高度是跟着 ramCost 变的 —— 写死偏移会让这屏在大窗口里靠上、小窗口里溢出。
        //
        // 两行作为一组在内容区**垂直居中**：字号不同（大字 LOCKED + 小字倒计时），
        // 所以要先把两行的高度算出来，再让整组居中，否则居中的是大字、整组看着偏下。
        Vector2 titleSize = GuiData.font.MeasureString(title);
        Vector2 closingSize = GuiData.detailfont.MeasureString(closing);
        const int gap = 8;                                  // 两行间距
        float blockH = titleSize.Y + gap + closingSize.Y;
        float blockTop = area.Y + (area.Height - blockH) * 0.5f;

        spriteBatch.DrawString(GuiData.font, Utils.FlipRandomChars(title, 0.05),
            new Vector2(area.X + (area.Width - titleSize.X) * 0.5f, blockTop),
            os.lockedColor, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);

        TextItem.doFontLabel(
            new Vector2(area.X + (area.Width - closingSize.X) * 0.5f, blockTop + titleSize.Y + gap),
            closing, GuiData.detailfont, Color.White * 0.5f);
    }

    /// <summary>内容区（背景层铺满它；对齐 CustomTrialExe 的 contentRect 算法）。</summary>
    private Rectangle ContentArea
        => new(bounds.X + 2, bounds.Y + PANEL_HEIGHT + 2, bounds.Width - 4, bounds.Height - PANEL_HEIGHT - 4);

    // ---- Listing ----

    /// <summary>一行前置/协议要求的显示数据。</summary>
    private sealed class ListingRow
    {
        public string Label;
        public bool Met;
        public string Detail;

        /// <summary>汇总行（「N satisfied」/「+M more unmet」）：不画状态点、用暗底。</summary>
        public bool Summary;
    }

    /// <summary>前置行固定行高（像素）。
    ///
    /// <para>
    /// 固定 = 不随行数压缩。此前用 <c>avail / rows.Count</c> 自适应，行多时会压到 16px，
    /// 文字挤成一团看不清；改成固定 30（条高 26，与正常观感一致），
    /// 行数超出容量时改为**折叠已满足项 + 溢出汇总行**，而不是压行高。
    /// </para>
    /// </summary>
    private const int LISTING_ROW_HEIGHT = 30;

    /// <summary>
    /// 组装 Listing 行：先前置（防火墙 / 代理），再各协议前置端口。
    /// 前置行仅在「配置开启 **且** 目标机存在对应物」时显示。
    /// </summary>
    private List<ListingRow> BuildListingRows()
    {
        List<ListingRow> rows = new();
        if (CurrentConfig == null) return rows;

        if (FirewallRowVisible())
            rows.Add(new ListingRow
            {
                Label = KELoc.Loc("PORT_HACK_V2_ROW_FIREWALL", "FIREWALL"),
                Met = Target.firewall.solved,
                Detail = "solve the target firewall"
            });

        if (ProxyRowVisible())
            rows.Add(new ListingRow
            {
                Label = KELoc.Loc("PORT_HACK_V2_ROW_PROXY", "PROXY OVERLOAD"),
                Met = !Target.proxyActive,
                Detail = ""            // 说明文本已统一上移到标题下方，行内不再重复
            });

        if (CrackedPortsRowVisible())
            rows.Add(new ListingRow
            {
                Label = KELoc.Format("PORT_HACK_V2_ROW_CRACKED_PORTS", "CRACKED PORTS {0}/{1}",
                    CrackedPortCount(), RequiredCrackedPorts()),
                Met = CrackedPortsRequirementMet(),
                Detail = ""
            });

        foreach (string p in CurrentConfig.RequireOpenPortsBefore)
        {
            // 标签分三种：
            //  · 正常显示名（协议已注册 + 目标机有）
            //  · `xxx (unknown protocol)` —— 协议压根没注册（拼错，或稍后由 <PFPorts>/PortAdd 注册）
            //  · `xxx (not on target)`   —— 协议存在但**这台机器**没有该端口（可能漏了注册步骤，或靠 Action 后加）
            bool registered = IsProtocolRegistered(p);
            bool present = TargetHasPort(p);
            string label = ProtocolDisplayName(p);
            if (!registered)
                label += " " + KELoc.Loc("PORT_HACK_V2_TAG_UNKNOWN_PROTOCOL", "(unknown protocol)");
            else if (!present)
                label += " " + KELoc.Loc("PORT_HACK_V2_TAG_NOT_ON_TARGET", "(not on target)");

            rows.Add(new ListingRow
            {
                Label = label,
                Met = PortRequirementMet(p),
                Detail = ""
            });
        }

        return rows;
    }

    /// <summary>协议名 → 显示名；未注册的协议原样返回（标注由调用方加，避免重复）。</summary>
    private static string ProtocolDisplayName(string protocol)
    {
        try
        {
            if (PortManager.IsPortRegistered(protocol))
                return PortManager.GetPortRecordFromProtocol(protocol).DefaultDisplayName;
        }
        catch { }
        return protocol;
    }

    /// <summary>协议是否已在 Pathfinder 全局注册表里（原版内置 / 模组注册 / 读档补注册都算）。</summary>
    private static bool IsProtocolRegistered(string protocol)
    {
        if (string.IsNullOrEmpty(protocol)) return false;
        try { return PortManager.IsPortRegistered(protocol); }
        catch { return false; }
    }

    /// <summary>目标机上是否存在该协议的端口（与「是否已注册」是两件事）。</summary>
    private bool TargetHasPort(string protocol)
    {
        try { return Target?.GetPortState(protocol) != null; }
        catch { return false; }
    }

    /// <summary>
    /// 配置加载后做一次「协议名是否存在」的体检：未注册的协议大概率是拼错，
    /// 但也可能是**计划稍后由 `&lt;PFPorts&gt;` / `PortAdd` 注册**（合法用法），
    /// 所以只 **Warn（进终端）**、不锁死 —— Locked 会误伤动态注册场景。
    /// 该配置会在 Listing 一直阻塞到协议被注册且端口打开为止。
    /// </summary>
    private static void WarnUnregisteredProtocols(PortHackV2Config cfg)
    {
        if (cfg == null) return;
        List<string> unknown = new();
        try
        {
            foreach (string p in cfg.RequireOpenPortsBefore)
                if (!IsProtocolRegistered(p)) unknown.Add(p);
        }
        catch { }
        if (unknown.Count == 0) return;

        KELog.WarnT($"[PortHackV2] '{cfg.SourceName}': <RequireOpenPortsBefore> contains unregistered protocol(s): "
            + string.Join(", ", unknown)
            + " — likely a typo; if intentional, register them first (<PFPorts> or PortAdd). "
            + "The crack stays blocked until they are registered and opened.");
    }

    /// <summary>
    /// 预计破解时长 = **根级 <c>&lt;Time&gt;</c>**。
    ///
    /// <para>
    /// ⚠️ 以前这里算的是协议级的 <c>max(StartDelay + TimeIfSuccess)</c>，与真实时长**口径不一致**：
    /// 整场 Cracking 的时长由根级 <c>&lt;Time&gt;</c> 决定，底部进度条（<c>_crackTime / Time</c>）
    /// 与倒计时（<c>Time - _crackTime</c>）用的都是它。于是示例配置
    /// （<c>&lt;Time&gt;30&lt;/Time&gt;</c> 配 <c>StartDelay=3 / TimeIfSuccess=12</c>）会显示
    /// "预计 15 秒"，而进度条实际要跑 30 秒 —— 玩家看到的两个数对不上。
    /// </para>
    ///
    /// <para>
    /// 现在这个显示与进度条、倒计时用的是**同一个数**，不会再互相矛盾。
    /// </para>
    /// </summary>
    private float EstimatedDuration() => Math.Max(0f, CurrentConfig.Time);

    private void DrawListing()
    {
        Rectangle area = ContentArea;
        DrawBackground(area);

        int x = InnerX;
        int w = InnerW;

        // 顶部固定文案：**不用 <Title>**（那个字段已删）。这里说明"为什么还停在这一屏"，
        // 字号比原来的标题小一号（GuiData.smallfont），不再抢前置行的视觉重心。
        //
        // ⚠️ 用白色而**不是强调色**：这行是**状态说明**（"为什么还没开始"），不是内容标题。
        // 跟随 AccentColor 会让它在某些配色下与前缀行的语义色糊在一起，也把作者选的
        // 强调色误用到了一句提示语上 —— 强调色该只影响立体/进度条/背景这些"主体"。
        // ⚠️ `isExiting` 也是"没在开始"：退出阶段这一屏还在淡出，
        // 但程序已经不会再进 Cracking 了。若不把它算进来，断连（或玩家按了 Exit）之后
        // 顶部仍会写着「没有前置要求，准备开始…」—— 而程序其实正在关闭，从没进过破解。
        // 目标不可达（Target == null，例如已断连）同理。
        bool blocked = isExiting || Target == null || !AllPrerequisitesMet();
        string headline = blocked
            ? KELoc.Loc("PORT_HACK_V2_PREREQ_UNMET", "Prerequisites not met")
            : KELoc.Loc("PORT_HACK_V2_NO_PREREQ", "No prerequisites - starting...");
        TextItem.doFontLabel(new Vector2(x, ContentTop), headline, GuiData.smallfont, Color.White * fade);

        List<ListingRow> rows = BuildListingRows();

        // ── 行布局：**固定行高**，行数超出容量时折叠/汇总，绝不压缩行高、也绝不静默丢弃 ──
        // 容量 = 可用高度 / 固定行高；底部 22px 留给「预计用时」那行。
        int top = ContentTop + 20;
        int avail = Math.Max(0, ContentBottom - 22 - top);
        int capacity = Math.Max(1, avail / LISTING_ROW_HEIGHT);

        List<ListingRow> drawRows = new();
        if (rows.Count <= capacity)
        {
            // 放得下：保持原顺序，全部显示（行位置稳定，不因满足状态跳来跳去）
            drawRows.AddRange(rows);
        }
        else
        {
            // 放不下：**未满足优先**（玩家只关心还差什么），满足项折叠成一行
            List<ListingRow> unmet = rows.Where(r => !r.Met).ToList();
            List<ListingRow> met = rows.Where(r => r.Met).ToList();
            // ⚠️ 溢出时**必须**给汇总行留 1 行 —— 之前的写法在 met==0 时把整容量都给了
            //    未满足行，汇总行被挤到边界外（实测：第 7 行压到「预计用时」上）。
            int roomForUnmet = Math.Max(0, capacity - 1);

            if (unmet.Count <= roomForUnmet)
            {
                drawRows.AddRange(unmet);
                if (met.Count > 0)
                    drawRows.Add(new ListingRow
                    {
                        Label = KELoc.Format("PORT_HACK_V2_ROW_SATISFIED", "{0} satisfied", met.Count),
                        Met = true,
                        Summary = true
                    });
            }
            else
            {
                drawRows.AddRange(unmet.Take(roomForUnmet));
                int hidden = unmet.Count - roomForUnmet;
                drawRows.Add(new ListingRow
                {
                    Label = met.Count > 0
                        ? KELoc.Format("PORT_HACK_V2_ROW_MORE_UNMET_OK", "+{0} more unmet \u00b7 {1} satisfied", hidden, met.Count)
                        : KELoc.Format("PORT_HACK_V2_ROW_MORE_UNMET", "+{0} more unmet", hidden),
                    Met = false,
                    Summary = true
                });
            }
        }

        int y = top;
        foreach (ListingRow row in drawRows)
        {
            // 硬边界：绝不让行画进底部「预计用时」区（宁可少画一行）
            if (y + LISTING_ROW_HEIGHT > ContentBottom - 22) break;

            int rowH = LISTING_ROW_HEIGHT;
            Rectangle bar = new Rectangle(x, y, w, rowH - 4);

            if (row.Summary)
            {
                // 汇总行：暗底 + 无状态点（它不是一条具体前置）
                RenderedRectangle.doRectangle(bar.X, bar.Y, bar.Width, bar.Height,
                    os.darkBackgroundColor * (0.85f * fade));
            }
            else
            {
                Color bg = row.Met ? os.unlockedColor : os.lockedColor;
                RenderedRectangle.doRectangle(bar.X, bar.Y, bar.Width, bar.Height, bg * (0.85f * fade));

                // 左侧状态点：**满足 = 白色**（不跟 AccentColor，避免与动态色混在一起），未满足 = 暗
                RenderedRectangle.doRectangle(bar.X + 4, bar.Y + 4, 4, Math.Max(2, bar.Height - 8),
                    row.Met ? Color.White * fade : os.darkBackgroundColor * fade);
            }

            // 一行只写名字（长名字由 doFontLabelToSize 等比缩小，不截断）
            TextItem.doFontLabelToSize(
                new Rectangle(bar.X + 16, bar.Y + 1, bar.Width - 22, Math.Max(9, bar.Height - 2)),
                row.Label, GuiData.smallfont,
                (row.Summary ? Color.White * 0.72f : Color.White * 0.95f) * fade);

            y += rowH;
        }

        // 左下角：预计破解时长（= 根级 <Time>，与进度条/倒计时同一个数）
        // <ShowEstimate>false</ShowEstimate> 可隐藏 —— 时长本身就是剧透信息。
        if (CurrentConfig.ShowEstimate)
        {
            string est = KELoc.Format("PORT_HACK_V2_ESTIMATE", "Estimated time: {0}s",
                (int)Math.Ceiling(EstimatedDuration()));
            TextItem.doFontLabel(new Vector2(x, ContentBottom - 18), est, GuiData.detailfont,
                Color.White * (0.6f * fade));
        }
    }

    // ---- Cracking ----

    /// <summary>
    /// 破解中：**只有立体动画 + 底部总进度条**。
    ///
    /// 刻意不画的东西（都是与"正在破解"这件事无关的噪声）：
    ///   · 标题 —— 玩家已经知道自己在跑什么程序
    ///   · 每个协议各自的进度条 —— 多协议并行时它们会一起动，看不过来看不出信息；
    ///     底部的总进度条已经表达了"还要多久"
    ///   · 百分比与剩余秒数 —— 进度条本身就是时间表达，再叠数字是重复（且窄面板时会撞在一起）
    /// </summary>
    private void DrawCracking()
    {
        Rectangle area = ContentArea;

        // 本状态**不铺背景**：立体动画自己会填满内容区，再垫一层背景只是浪费；
        // 背景只服务 Locked / Listing 这两个"没有自己的画面"的状态。

        // 动画区铺满内容区（下面只留底部进度条的位置）
        Rectangle anim = new(area.X, area.Y, area.Width, Math.Max(24, area.Height - PROGRESS_BAR_H - 4));
        DrawAnimationArea(anim, opacity: 0.6f);

        float total = CurrentConfig.Time <= 0f ? 1f : MathHelper.Clamp(_crackTime / CurrentConfig.Time, 0f, 1f);
        DrawProgressBar(total, showSeconds: false, showPercent: false);
    }

    // ---- 底部总进度条 ----

    /// <summary>底部总进度条的几何（贴着内容区底边）。</summary>
    private Rectangle ProgressBarRect
        => new(bounds.X + 2, bounds.Y + bounds.Height - PROGRESS_BAR_H - 2, bounds.Width - 4, PROGRESS_BAR_H);

    /// <summary>进度条高度（像素）。</summary>
    private const int PROGRESS_BAR_H = 16;

    /// <summary>
    /// 底部总进度条：衬底 + 进度 + **边框**（边框对齐 CustomTrialExe 的 DrawTimerBar，
    /// 否则深色背景上条体边界不清）。
    ///
    /// 颜色走 <c>ProgressColor</c>（留空 = 跟随强调色），与原版一致整条随 <c>fade</c> 淡出。
    /// 文字默认都不画 —— Cracking 只看条、Complete 已经满了，两者都没必要读数。
    /// </summary>
    private void DrawProgressBar(float progress, bool showSeconds, bool showPercent)
    {
        Rectangle bar = ProgressBarRect;
        if (bar.Width <= 4 || bar.Height <= 4) return;

        RenderedRectangle.doRectangle(bar.X, bar.Y, bar.Width, bar.Height, os.darkBackgroundColor * (0.9f * fade));
        RenderedRectangle.doRectangle(bar.X, bar.Y, (int)(bar.Width * progress), bar.Height, _progressColor * fade);
        RenderedRectangle.doRectangleOutline(bar.X, bar.Y, bar.Width, bar.Height, 1, Color.White * fade);

        if (bar.Width < 120) return;   // 太窄就别塞文字（否则百分比和秒数会撞在一起）

        if (showPercent)
        {
            string pct = $"{(int)(progress * 100f)}%";
            Vector2 ps = GuiData.smallfont.MeasureString(pct);
            spriteBatch.DrawString(GuiData.smallfont, pct,
                new Vector2(bar.X + bar.Width / 2f - ps.X / 2f, bar.Y + (bar.Height - ps.Y) / 2f),
                Color.White * fade, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);
        }

        if (showSeconds)
        {
            int remaining = Math.Max(0, (int)(CurrentConfig.Time - _crackTime));
            string timeText = KELoc.Format("PORT_HACK_V2_SECONDS_LEFT", "{0}s", remaining);
            Vector2 tsz = GuiData.smallfont.MeasureString(timeText);
            spriteBatch.DrawString(GuiData.smallfont, timeText,
                new Vector2(bar.X + bar.Width - tsz.X - 4, bar.Y + (bar.Height - tsz.Y) / 2f),
                Color.White * (0.85f * fade), 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);
        }
    }

    // ---- Complete / Failed 演出 ----

    /// <summary>
    /// 收尾演出：背景（立体慢转）+ 上下白线展开 + **单行**中央文案。
    ///
    /// 文案：成功 = <c>CompletionText</c>（默认 <c>COMPLETE</c>，不做本地化）；
    /// 中断 = CONNECTION / LOST（说明"连接断了/人走了"，不是破解失败）。
    /// 对齐原版：上下白线用 <c>QuadraticOutCurve</c> 展开，文字叠两层乱序字。
    /// </summary>
    private void DrawOutro()
    {
        Rectangle area = ContentArea;

        // 本状态**不铺背景**：立体（慢转/已停）就是画面主体
        // 背景：立体继续慢转（成功）/ 已停住（中断）
        DrawAnimationArea(area, opacity: CurrentState == PortHackV2State.Complete ? 0.2f : 0.35f);

        bool win = CurrentState == PortHackV2State.Complete;
        string text = win ? CurrentConfig.CompletionText : KELoc.Loc("PORT_HACK_V2_INTERRUPT_1", "CONNECTION");

        int cx = bounds.X + bounds.Width / 2;

        // 中央文案：**尽量占满内容区**，只留出上下分隔线与底部进度条的位置。
        // 原先 textRect 高度写死 28 —— doFontLabelToSize 缩放到 28 就封顶了，字号上不去。
        // 现在把可用高度算出来交给它，长文案仍会被宽度约束自动缩到合适。
        const int LINE_GAP = 18;       // 文字与外框白线之间留的空
        const int BAR_ROOM = 34;       // 底部给进度条留的高度（成功演出时会画）
        int textTop = area.Y + 4;
        int textBottom = area.Bottom - (win ? BAR_ROOM : 6);
        int textH = Math.Max(20, textBottom - textTop - LINE_GAP * 2);
        Rectangle textRect = new(area.X + 10, textTop + LINE_GAP, area.Width - 20, textH);

        // 上下白线贴在文字上下外侧，随文字高度一起外扩（不再写死 cy±20/26）
        int lineTop = textRect.Y - LINE_GAP / 2;
        int lineBottom = textRect.Bottom + LINE_GAP / 2;

        // 上下白线展开：0.4s 内从中心向两侧推开
        float expand = Utils.QuadraticOutCurve(MathHelper.Clamp(_completeElapsed / 0.4f, 0f, 1f));
        int lineW = (int)(expand * (bounds.Width - 30f));
        if (lineW > 2)
        {
            spriteBatch.Draw(Utils.white, new Rectangle(cx - lineW / 2, lineTop, lineW, 2),
                Utils.AddativeWhite * fade);
            spriteBatch.Draw(Utils.white, new Rectangle(cx - lineW / 2, lineBottom, lineW, 2),
                Utils.AddativeWhite * fade);
        }

        // 单行中央文案：宽高各自自适应，过长自动缩字号（作者可能配很长的 CompletionText）
        Color baseColor = win ? Color.White : os.lockedColor;
        DrawFlipTextFit(textRect, text, baseColor);

        if (win)
            DrawProgressBar(1f, showSeconds: false, showPercent: false);
    }

    /// <summary>
    /// 在给定矩形里居中画一行乱序字（叠一层低概率抖动），**按矩形宽度自动缩字号**。
    /// 用 <see cref="TextItem.doFontLabelToSize"/> 做贴合，避免作者配长文案时溢出面板。
    /// </summary>
    private void DrawFlipTextFit(Rectangle rect, string text, Color color)
    {
        // 抖动层先画（暗、低概率），清晰层后画压在上面
        TextItem.doFontLabelToSize(rect, Utils.FlipRandomChars(text, 0.11),
            GuiData.font, Utils.AddativeWhite * (0.2f * fade * fade));
        TextItem.doFontLabelToSize(rect, Utils.FlipRandomChars(text, 0.012),
            GuiData.font, color * fade);
    }


    // ---- 3D 动画画布 ----

    /// <summary>
    /// 把 3D 动画渲染到离屏 RenderTarget 再贴回面板。
    ///
    /// 为什么要离屏：<c>OS.Draw</c> 在所有 exe 的 Draw 之外已经 Begin 了主 spriteBatch
    /// 且中途不 End，所以在面板里直接 Begin/End 会抛
    /// "Begin cannot be called again until End has been successfully called"。
    /// 离屏 + 专用 SpriteBatch 是本项目已验证的做法（见心碎 daemon 的渲染）。
    /// </summary>
    private void DrawAnimationArea(Rectangle area, float opacity)
    {
        if (area.Width <= 8 || area.Height <= 8) return;

        try
        {
            if (_rt == null || _rt.Width != area.Width || _rt.Height != area.Height)
            {
                _rtBatch ??= new SpriteBatch(spriteBatch.GraphicsDevice);
                _rt?.Dispose();
                _rt = new RenderTarget2D(spriteBatch.GraphicsDevice, area.Width, area.Height);
            }

            RenderTarget2D previous = Utils.GetCurrentRenderTarget();
            spriteBatch.GraphicsDevice.SetRenderTarget(_rt);
            spriteBatch.GraphicsDevice.Clear(Color.Transparent);
            _rtBatch.Begin();
            _sequence.RenderFrame(spriteBatch.GraphicsDevice, _rtBatch,
                new Rectangle(0, 0, area.Width, area.Height), _accent);
            _rtBatch.End();
            spriteBatch.GraphicsDevice.SetRenderTarget(previous);

            _sequence.DrawOverlay(spriteBatch, area, opacity * fade);
        }
        catch (Exception ex)
        {
            // 渲染失败不该打断破解流程：退回一个纯色底，并只记一次 Warn 避免刷屏
            if (!_animWarned)
            {
                _animWarned = true;
                KELog.Warn($"[PortHackV2] animation draw failed: {ex.Message}");
            }
            RenderedRectangle.doRectangle(area.X, area.Y, area.Width, area.Height, Color.Black * 0.35f);
        }
    }

    private bool _animWarned;

    /// <summary>
    /// 强调色（标题 / 底部总进度条 / 动画立体线框）。
    /// 动态色（CC 预设、RGB、Gradient）必须**逐帧重算**，所以在 Update 里刷新而不是 Draw 里现算 ——
    /// Draw 一帧要画十几处，每处都走一遍解析既浪费又可能在同一帧里拿到不同相位。
    /// </summary>
    private Color _accent = Color.White;
    private string _accentStr;

    /// <summary>进度条颜色（`ProgressColor`，留空 = 跟随强调色）。</summary>
    private Color _progressColor = Color.White;
    private string _progressStr;

    /// <summary>背景色（`BackgroundColor`，留空 = 跟随强调色）。</summary>
    private Color _bgColor = Color.White;
    private string _bgStr;

    /// <summary>
    /// 刷新三处颜色（强调色 / 进度条 / 背景）。
    /// 动态色（CC 预设、RGB、Gradient）必须**逐帧重算**，所以在 Update 里刷新而不是 Draw 里现算 ——
    /// Draw 一帧要画十几处，每处都走一遍解析既浪费又可能在同一帧里拿到不同相位。
    /// </summary>
    private void RefreshAccent()
    {
        _accent = CustomColorManager.GetDynamicColor(_accentStr, PortHackV2Config.DefaultAccentColor);
        // 留空 = 跟随强调色（配置类里也是这么解析的，这里保持一致）
        _progressColor = ConfigValue.IsNone(_progressStr)
            ? _accent : CustomColorManager.GetDynamicColor(_progressStr, _accent);
        _bgColor = ConfigValue.IsNone(_bgStr)
            ? _accent : CustomColorManager.GetDynamicColor(_bgStr, _accent);
    }

    /// <summary>
    /// 程序被移除时释放离屏资源（RenderTarget / 专用 SpriteBatch / 3D 缓冲）。
    /// GameExecutable 没有 Dispose 钩子，OnComplete 是它给的"即将从 os.exes 移除"回调。
    /// </summary>
    public override void OnComplete()
    {
        try { _sequence?.Dispose(); } catch { }
        try { _rt?.Dispose(); } catch { }
        try { _rtBatch?.Dispose(); } catch { }
        _rt = null;
        _rtBatch = null;
        base.OnComplete();
    }
}
