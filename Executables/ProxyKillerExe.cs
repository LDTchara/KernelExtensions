using Hacknet;
using Hacknet.Gui;
using HarmonyLib;
using KernelExtensions.Configs;
using KernelExtensions.Managers;
using KernelExtensions.Utilities;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Pathfinder.Executable;

namespace KernelExtensions.Executables;

/// <summary>
/// ProxyKiller —— **加速代理瘫痪**的常驻程序。
///
/// <para>
/// 它做两件事，都不需要玩家操作：
/// <list type="number">
///   <item>RAM 从**固定的 50MB** 爬到配置的目标值（默认 100），之后一直占着不释放 ——
///     这个过程同时驱动界面：<c>chaosPercent</c> 随爬升从 1 降到 0（粒子由混沌趋于整齐）</item>
///   <item>把**所有正在过载的 Shell** 的过载速率乘上一个倍速，倍速同样由 RAM 爬升
///     从 1 线性驱动到配置的目标值</item>
/// </list>
/// </para>
///
/// <para>
/// ⚠️ **它自己不瘫痪任何代理**。原版把代理过载做成 Shell 的一个状态
/// （<c>ShellExe</c> 的 <c>proxyOverloadTicks -= t</c>），而 Shell 需要目标机 admin 才能开
/// （终端 <c>shell</c> 命令里 <c>hasConnectionPermission(admin: true)</c> 那道判定）——
/// 那是设计上的护栏，本程序不去拆：没有 shell 在过载，它就只是占着内存跑动画。
/// 于是"提速"与"门槛"分离：谁去过载、能不能过载，仍由玩家与原版规则决定；
/// 本程序只让**已经在做的事**更快。N 个 shell 的总速率 = N × 倍速。
/// </para>
///
/// <para>
/// 视觉与 RAM 爬升逻辑**照搬原版 `DLCTraceSlower`（`SignalScramble`）** ——
/// 那是原版唯一一个"自己把 RAM 吃掉"的程序：
/// RAM 50 → 600、速率 200/s、底部一个 Exit 按钮、中下部一条状态文字带假进度读数。
/// 差别只有：目标 RAM 与倍速改成可配、背景参数可配、文字改成"加速"语义、
/// 以及它原来是减速 trace 而这里是加速 shell 过载。
/// </para>
///
/// <para>
/// 加速实现：不用 Harmony 去 patch <c>ShellExe.Update</c> 里那一行 —— 那要动一个 internal
/// 类型的方法体，会与将来任何改 <c>ShellExe</c> 的模组打架。这里只是在**已发生的过载**上
/// 补差额 <c>t * (倍速 - 1)</c>，不改变 shell 自己的状态机（"ticks ≤ 0 时置
/// proxyActive = false"的收尾仍由 shell 自己做）。
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
    private float _lockedTimer;

    /// <summary>当前实际 RAM 占用（浮点，爬升过程需要小数）。</summary>
    private float _ram;
    private float _ramStart;
    private float _target;
    private float _mulTarget;

    // ---- 自己过载 / 跟随连接 / 自动关闭（见 ProxyKillerConfig 的相应字段）----

    /// <summary>
    /// 自己过载的速率（<c>1</c> = 原版一个 shell 的过载速度）。
    /// 是否启用由 <see cref="ProxyKillerConfig.SelfOverload"/> 决定 —— 开关与速率是两个字段。
    /// </summary>
    private float _selfOverloadRate;

    /// <summary>启动时所在节点（跟随连接时用它判断"玩家走开了"）。</summary>
    private string _homeIp;

    /// <summary>
    /// 所盯节点：<see cref="ProxyKillerConfig.ResolvedFollowConnection"/> 为 true 时是**当前所连节点**，
    /// 否则是启动时所在节点。它的 <c>proxyActive</c> 由 true 变 false = 过载完成 → 可自动关闭。
    /// </summary>
    private Computer _watched;

    /// <summary>是否已经看到过所盯节点的代理处于生效状态（避免刚开就误判为"已完成"）。</summary>
    private bool _sawProxyActive;

    /// <summary>自己过载的命中计数（仅用于日志降噪）。</summary>
    private bool _selfOverloading;

    // ---- 背景：上升数据流（自下而上的粒子洪流，随 RAM 爬升加速）----
    private Color _bgColor = Color.White;
    /// <summary>true = 背景色是配置里写死的静态色（不再每帧取主题高亮色）。</summary>
    private bool _bgColorFixed;
    private readonly FlowFieldEffect _flow = new();
    private bool _bgWarned;

    /// <summary>上一帧绘制区高度 —— 背景的密度/速度幂律都要按高度算（见 FlowFieldEffect）。</summary>
    private int _lastAreaHeight;

    private const int ID_EXIT = 910;

    public ProxyKillerExe() : base()
    {
        this.name = ProxyKillerConfig.DefaultProgramName;
        // ⚠️ 窗口顶部标题画的是 IdentifierName（原版 ExeModule.drawTarget："app:" + IdentifierName），
        // 不是 name —— 不设它就显示 app:UNKNOWN。
        this.IdentifierName = ProxyKillerConfig.DefaultProgramName;
        this.ramCost = ProxyKillerConfig.RamCost;
    }

    // ---------- 实例登记与无痕互斥（对齐 CustomTrialExe / PhaseSwiftExe）----------

    private static readonly List<ProxyKillerExe> activeInstances = new();

    /// <summary>是否存在正在运行的实例（事件层无痕互斥检查用）。</summary>
    public static bool HasRunningInstance() => activeInstances.Any(inst => !inst.isExiting);

    /// <summary>当前正在运行的实例（事件层无痕互斥提示用，与 OnInitialize 兜底查找一致）。</summary>
    public static ProxyKillerExe RunningInstance => activeInstances.FirstOrDefault(inst => !inst.isExiting);

    public override void OnInitialize()
    {
        base.OnInitialize();

        activeInstances.Add(this);

        // 互斥检查（兜底防御，主防线在事件层 KernelExtensions.OnExecutableExecute_Mutex）：
        // 本程序是**常驻**的，而副作用是**全局**的 —— 每帧扫所有过载中的 shell 逐帧削减
        // proxyOverloadTicks。开两个 = 同一个 shell 被削两次 = 加速与 RAM 都凭空翻倍，
        // 原版没有对应物，纯属自己叠加自己。
        var other = activeInstances.FirstOrDefault(inst => inst != this && !inst.isExiting);
        if (other != null)
        {
            os.write(KELoc.Format("EXECUTABLE_ALREADY_RUNNING", "{0} already running!", other.IdentifierName));
            isExiting = true;
            activeInstances.Remove(this);
            try
            {
                // ExeModule internal 无法直接访问，反射处理（同 CustomTrialExe）
                var exes = AccessTools.Field(typeof(OS), "exes")?.GetValue(os) as System.Collections.IList;
                exes?.Remove(this);
            }
            catch { }
            return;
        }

        List<string> flags = FindConfigFlags();
        if (flags.Count != 1)
        {
            if (flags.Count == 0)
            {
                // ⚠️ tellPlayer: false —— 没 flag 是最正常的情况（剧本还没发工具，
                //    或前半段发了不给用）。锁定本身就是剧情的一部分，终端里不该剧透。
                //    作者要提示玩家，用自己的剧情文案（TerminalWrite 等）。
                Lock(KELoc.Loc("PROXY_KILLER_LOCKED", "ProxyKiller: no configuration is armed."),
                    tellPlayer: false);
                KELog.Info($"{LogTag} no config flag present - locked.");
            }
            else
            {
                // 多 flag = 作者配置错误，玩家无从下手 —— 但让作者在终端里看见
                Lock(KELoc.Loc("PROXY_KILLER_MULTI_FLAG",
                    "ProxyKiller: multiple configurations are armed (authoring error)."),
                    tellPlayer: true);
                KELog.Error($"{LogTag} {flags.Count} config flags present, expected exactly one: "
                    + string.Join(", ", flags));
            }
            return;
        }

        _cfg = ProxyKillerConfig.LoadByFlag(flags[0]);
        if (_cfg == null)
        {
            // 同样属作者错误（文件缺失 / 解析失败），让作者在终端里看见
            Lock(KELoc.Loc("PROXY_KILLER_LOAD_FAILED",
                "ProxyKiller: configuration could not be loaded."), tellPlayer: true);
            return;
        }

        // 程序名与注册名一致（作者可改显示名，注册名 #PROXYKILLER# 不受影响）
        if (!string.IsNullOrWhiteSpace(_cfg.ProgramName))
        {
            name = _cfg.ProgramName;
            IdentifierName = _cfg.ProgramName;
        }

        _bgColor = _cfg.BackgroundColor;
        // auto（或留空 / NONE）时颜色取主题高亮色，但**每帧取** —— 配置解析时拿不到 os，
        // 而且这样主题中途变化也能跟上。所以这里只把非 auto 的静态值定下来。
        if (!_cfg.BackgroundColorAuto) _bgColorFixed = true;

        // 起始值固定 50（不可配）；目标取配置值，且不小于起始值，避免"反向下滑"
        _ramStart = ProxyKillerConfig.RamCost;
        _target = Math.Max(_ramStart, _cfg.BurstTarget);
        _mulTarget = _cfg.ShellOverloadTarget;
        _selfOverloadRate = _cfg.SelfOverloadRate;
        _ram = _ramStart;
        ramCost = (int)_ram;

        // 跟随连接的判定基准：启动时所在节点（不跟随时就盯它）
        _homeIp = os?.connectedComp?.ip;
        _watched = _cfg.ResolvedFollowConnection ? os?.connectedComp : FindComputerByIp(_homeIp);

        // 背景：上升数据流。参数全部来自配置，运行时不变（它们是形态而不是动画进度）。
        _flow.DensityCoefficient = _cfg.BackgroundDensityCoefficient;
        _flow.DensityExponent = _cfg.BackgroundDensityExponent;
        _flow.SpeedCoefficient = _cfg.BackgroundSpeedCoefficient;
        _flow.SpeedExponent = _cfg.BackgroundSpeedExponent;
        _flow.ParticlePixels = _cfg.ParticlePixels;
        _flow.TrailPixels = _cfg.TrailPixels;
        _flow.LoadTrailGain = _cfg.LoadTrailGain;
        _flow.LoadSpeedGain = _cfg.LoadSpeedGain;
        _flow.Reset();

        KELog.Info($"{LogTag} '{_cfg.SourceName}' started: ram {_ramStart} -> {_target}MB "
            + $"at {_cfg.RampRate.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}MB/s, "
            + $"shell overload x1 -> x{_mulTarget.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}"
            + (_cfg.SelfOverload
                ? $", self-overload x{_selfOverloadRate.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)} (follows connection)"
                : ", no self-overload")
            + $", follow={_cfg.ResolvedFollowConnection}, autoCloseOnOverload={_cfg.AutoCloseOnOverload}.");

        // 反射可用性一次报清 —— 失败时加速会静默失效，必须让它可见
        if (ReflectionReady)
        {
            KELog.Debug($"{LogTag} shell hooks resolved (OS.exes, ShellExe.destComp).");
        }
        else
        {
            KELog.Error($"{LogTag} shell hooks could NOT be resolved - shell acceleration will do nothing. "
                + $"(OS.exes found: {AccessTools.Field(typeof(OS), "exes") != null}, "
                + $"ShellExe.destComp found: {destCompField != null})");
        }
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

    /// <summary>
    /// 进入锁定态。
    ///
    /// <para>
    /// <paramref name="tellPlayer"/> 决定要不要往**玩家终端**写一句。
    /// ⚠️ 「没有 flag」这一种**不写**：那是最正常的情况 —— 剧本还没发这个工具、
    /// 或者前半段发了但不给用，**锁定本身就是剧情的一部分**，不该在终端里剧透一句
    /// "没有配置"。作者要提示玩家，应该用自己的剧情文案（`TerminalWrite` 之类）。
    /// </para>
    ///
    /// <para>
    /// 另外两种（多 flag / 配置加载失败）都是**作者配置错误**，玩家什么也做不了，
    /// 但写一句至少让作者在终端里看见 —— 那正是"配置写了却没生效"最容易沉底的地方。
    /// </para>
    /// </summary>
    private void Lock(string reason, bool tellPlayer)
    {
        _locked = true;
        _lockedTimer = LOCKED_EXIT_SECONDS;
        if (tellPlayer) os.write(reason);
    }

    /// <summary>锁定态的"负荷"：固定给一个中间值，让粒子流匀速流动、不随 RAM 爬升变化。</summary>
    private static float LockedLoad() => 0.5f;

    // ======================= Update =======================

    public override void Update(float t)
    {
        base.Update(t);

        if (_locked)
        {
            // 锁定屏要么整屏在、要么整屏不在（见 Draw），这里的计时只负责到点关闭
            if (!isExiting)
            {
                _lockedTimer -= t;
                if (_lockedTimer <= 0f) isExiting = true;
            }
            // ⚠️ 锁定态也要推进背景：那一屏只有一个词 + 一片粒子流，
            //    背景停住就真的是一块死画面了（而它要停留 3 秒）。
            //    `_cfg` 为 null 时用 FlowFieldEffect 自带的默认参数（见 Draw）。
            _flow.Update(t, LockedLoad(), _lastAreaHeight);
            return;
        }
        if (_cfg == null) { isExiting = true; return; }

        // ⚠️ 退出时**不能**整体 return —— 背景必须继续跑。
        // 原版的退场是"窗口高度收缩到 0"，如果这里一停，收缩期间画面就是**凝固**的，
        // 而状态文字已经写着 "Spinning down..."，两者完全脱节（用户实测就是这个现象）。
        // 所以：退出时只跳过 RAM 爬升与 shell 加速（那两个没必要再算），背景照旧推进。

        // 跟随连接 / 过载完成自动关闭：两项都是"该不该退"的判定，放在最前
        if (!isExiting)
        {
            CheckFollowConnection();
            CheckAutoCloseOnOverload();
        }

        if (!isExiting)
        {
            // RAM 线性爬升到目标，之后一直占着（原版 SignalScramble 同款机制，速率可配）
            if (_ram < _target)
            {
                if (_cfg.RampRate > 0f)
                {
                    _ram = Math.Min(_target, _ram + _cfg.RampRate * t);
                }
                else
                {
                    // RampRate = 0 的语义是**不爬、直接到位**，不是"停在起步值"。
                    // ⚠️ 这里曾经是"跳过整段"，于是 _ram 恒为起始 50MB：
                    //    能力进度（AbilityProgress）会因 RampRate<=0 直接满值，
                    //    而界面进度看的是 _ram，就一直是 0% + "Spinning up..." ——
                    //    表现为"能力是满的、画面说在起步"，两边对不上。
                    _ram = _target;
                }
                ramCost = (int)_ram;
            }

            // 倍速由 RAM 爬升线性驱动：倍速 1 → 目标值 对应 50MB → BurstTarget
            AccelerateShells(t);

            // 自己过载（不依赖 shell / 管理员权限）；与加速共用同一个 RAM 进度
            bool self = SelfOverloadProxy(t);
            if (self != _selfOverloading)
            {
                _selfOverloading = self;
                if (self) KELog.Debug($"{LogTag} self-overloading the watched node's proxy.");
            }
        }

        // 背景始终推进（速度与拖尾在 Draw 里按进度给）—— 退出期间它就是"降速"的视觉
        _flow.Update(t, BackgroundLoad(), _lastAreaHeight);
    }

    /// <summary>
    /// 背景的"负荷"值：既驱动速度也驱动拖尾长度。
    ///
    /// <para>
    /// 正常运行时就是 RAM 爬升进度；**退出时按窗口剩余高度等比回落**，
    /// 这样状态文字写 "Spinning down..." 的时候粒子是**真的在降速**，
    /// 而不是维持满速、只靠窗口缩小收场。
    /// </para>
    ///
    /// <para>
    /// 为什么用 <c>fade</c> 而不是自己计时：原版退场就是
    /// <c>bounds.Height = baseRamCost × fade</c>、<c>fade</c> 按 <see cref="ExeModule.FADEOUT_RATE"/>
    /// 递减 —— 所以 <c>fade</c> 本身就是"窗口还剩几成"，跟着它走两者天然同步。
    /// </para>
    /// </summary>
    private float BackgroundLoad()
        => isExiting ? RampProgress() * MathHelper.Clamp(fade, 0f, 1f) : RampProgress();

    /// <summary>
    /// RAM 爬升进度 0~1（= 倍速爬升进度）。起始值 == 目标值时视为已满。
    ///
    /// <para>
    /// <b>视觉进度</b>：只看内存爬了多少，<c>RampRate = 0</c> 时恒为 0（画面停在起步状态）。
    /// 用于背景的密度/流速——那里"没爬"就该是起步的观感。
    /// </para>
    /// </summary>
    private float RampProgress()
        => _target > _ramStart ? MathHelper.Clamp((_ram - _ramStart) / (_target - _ramStart), 0f, 1f) : 1f;

    /// <summary>
    /// <b>能力进度</b>（倍速 / 自己过载速率用它，**不要**用 <see cref="RampProgress"/>）。
    ///
    /// <para>
    /// 与视觉进度的差别只在 <c>RampRate = 0</c> 这个边界：那时 RAM 根本不爬（见 <c>Update</c> 里的
    /// 爬升守卫），<see cref="RampProgress"/> 恒为 0 —— 若能力也照它算，倍速会恒为 1、
    /// <b>自己过载的速率会恒为 0（整个功能静默失效）</b>。
    /// </para>
    ///
    /// <para>
    /// 但 <c>RampRate = 0</c> 的语义是"<b>不爬，直接到位</b>"，不是"关掉功能"。
    /// 所以这里返回 1 = 能力立刻满值。
    /// </para>
    /// </summary>
    private float AbilityProgress()
        => _cfg != null && _cfg.RampRate <= 0f ? 1f : RampProgress();

    // ======================= 核心：加速所有正在过载的 shell =======================

    /// <summary>
    /// 加速**所有正在过载的 Shell**（不区分它们各自对着哪个节点）。
    ///
    /// <para>
    /// 不按 IP 过滤的原因：shell 分布运行在多个节点上（每个 shell 的 <c>targetIP</c> 是它创建时
    /// 所连的那台，之后不变），而原版的多 shell 叠加本来就不挑目标 ——
    /// 每个 shell 各自 <c>ticks -= t</c>，谁指着哪台无所谓。所以这里也不按目标筛选。
    /// </para>
    ///
    /// <para>
    /// 每帧重新遍历 <c>os.exes</c>，所以**先运行本程序、之后才开的 shell 一样会被加速**
    /// （不缓存 shell 列表、不看创建时机）。
    /// </para>
    ///
    /// <para>
    /// 但必须**排除空闲的 shell**：<c>proxyOverloadTicks</c> 是 shell 自身持久的字段，
    /// 过载完之后停在 0；若不加判断，那些没在过载的 shell 的 ticks 会被减成负数，
    /// 下次它真的去过载时就从负数起跑 —— 等于把那台机器的代理直接归零，这不是加速。
    /// 判据是"该 shell 正在过载"，用两个条件确认：
    /// <c>0 &lt; ticks ≤ 目标机 startingOverloadTicks</c>（过载中被逐帧减小的区间）
    /// 且**目标机的代理仍存活**。
    /// </para>
    ///
    /// <para>
    /// 语义：倍速 <c>mul</c> = <c>Lerp(1, ShellOverloadTarget, 爬升进度)</c>，
    /// 每个 shell 原本每帧减 <c>t</c>（原版速度 = 倍速 1），所以补差额 <c>t * (mul - 1)</c>。
    /// 倍速峰值恰好等于配置的目标值（RAM 爬满那刻），爬升期间平均为 <c>(1 + 目标值) / 2</c>。
    /// </para>
    /// </summary>
    private void AccelerateShells(float t)
    {
        float mul = MathHelper.Lerp(1f, _mulTarget, AbilityProgress());
        float extraPerShell = t * (mul - 1f);
        if (extraPerShell <= 0f) return;   // 倍速 ≤ 1：不加速（也不该去"冻结"别人的 shell）

        try
        {
            if (AccessTools.Field(typeof(OS), "exes")?.GetValue(os) is not System.Collections.IList exes)
                return;

            int hit = 0;
            for (int i = 0; i < exes.Count; i++)
            {
                object shell = exes[i];
                if (shell == null) continue;

                // 只看 ShellExe（internal，用类型名判定）；其余程序一律不碰
                if (shell.GetType().FullName != ShellExeTypeName) continue;

                // ⚠️ 过载进度在**目标机**上，不在 shell 上。
                // 原版 ShellExe 的字段只有 destComp（目标机对象），它执行的是
                // destComp.proxyOverloadTicks -= t —— 所以这里也必须读 shell 的 destComp。
                // （ShellExe 上并没有 proxyOverloadTicks 字段，早期版本误读了它，
                //  反射返回 null 后每帧静默跳过所有 shell，表现为"加速完全没用"。）
                if (destCompField?.GetValue(shell) is not Computer dest) continue;

                float ticks = dest.proxyOverloadTicks;
                if (ticks <= 0f) continue;

                // 代理已不在 / 已瘫痪：没有可加速的东西
                if (!dest.hasProxy || !dest.proxyActive) continue;

                // "正在过载"的区间：ticks 在 (0, startingOverloadTicks] 内。
                // 超出上限说明该 shell 当前不是在过载它（destComp 是持久的、会指向上一次的目标），
                // 此时不碰 —— 否则会把一台只是"被这个 shell 指着"的机器的代理直接归零。
                if (dest.startingOverloadTicks > 0f && ticks > dest.startingOverloadTicks) continue;

                float next = ticks - extraPerShell;
                dest.proxyOverloadTicks = next > 0f ? next : 0f;
                hit++;
            }

            if (hit > 0 && !_accelerating)
            {
                _accelerating = true;
                KELog.Debug($"{LogTag} accelerating {hit} shell(s) overload.");
            }
            else if (hit == 0)
            {
                _accelerating = false;
            }
        }
        catch (Exception ex)
        {
            if (!_effectWarned)
            {
                _effectWarned = true;
                KELog.Warn($"{LogTag} could not accelerate shells: {ex.Message}");
            }
        }
    }

    /// <summary>原版 ShellExe 的类型全名（internal 类型，只能按名字找）。</summary>
    private const string ShellExeTypeName = "Hacknet.ShellExe";

    /// <summary>
    /// 自己过载代理 —— 不需要 shell，因此**不依赖目标机管理员权限**。
    ///
    /// <para>
    /// 原版里"过载代理"本身没有权限门槛（<c>ShellExe.StartOverload</c> 里没有任何检查），
    /// 门槛只在"运行 shell"上。所以本项不是绕过任何规则，而是补上一个原版缺失的手段。
    /// </para>
    ///
    /// <para>
    /// 速率由 RAM 爬升线性驱动：<c>0 → SelfOverload</c> 对应 <c>50MB → BurstTarget</c>，
    /// 与 shell 倍速共用同一个进度 —— 玩家调的是"给多少内存"，两个能力一起涨。
    /// </para>
    ///
    /// <para>
    /// ⚠️ 每帧调一次目标机的 <c>hostileActionTaken()</c>（对齐原版 shell 的做法）：
    /// 它内部自带 <b>0.35 秒</b>节流，且**只在玩家正连着该节点时才真的起 trace** ——
    /// 所以"跟随连接"的强制要求在这里自然成立（不跟随就会跑到哪打到哪）。
    /// </para>
    /// </summary>
    /// <returns>本帧是否真的推进了某个节点的过载。</returns>
    private bool SelfOverloadProxy(float t)
    {
        if (!_cfg.SelfOverload) return false;

        Computer dest = WatchedComputer();
        if (dest == null) return false;
        if (!dest.hasProxy || !dest.proxyActive) return false;

        float ticks = dest.proxyOverloadTicks;
        if (ticks <= 0f) return false;

        // 速率：0 → 目标值，对应 50MB → BurstTarget（与 shell 倍速同一进度）。
        // ⚠️ 用 AbilityProgress 而非 RampProgress：RampRate = 0 时后者恒为 0，
        //    会让这个功能**完全静默失效**（速率恒 0 = 一帧都不推进）。
        float rate = _selfOverloadRate * AbilityProgress();
        if (rate <= 0f) return false;

        float next = ticks - t * rate;
        dest.proxyOverloadTicks = next > 0f ? next : 0f;

        if (dest.proxyOverloadTicks <= 0f)
        {
            dest.proxyActive = false;
            KELog.Info($"{LogTag} self-overload finished on '{dest.ip}'.");
        }
        else
        {
            // 敌对行为：原版每个过载帧都调（见 ShellExe.Update 的 case 1）。
            //
            // hostileActionTaken() 是原版通用惯例（13 处调用：每个端口破解器、PortHack、
            // FTP/HTTP/SSH/SQL 等各一次）。它的实际效果分两半，都由原版自己判定：
            //   · 只有**玩家正连着那台机**才生效（内部先判 os.connectedComp.ip）
            //   · 其中「起 trace」还要求该机 traceTime > 0 —— 即**安全等级 ≥ 4** 的机器
            //     （Computer.cs: 仅 security >= 4 才赋正值）。低安机器只会收到视觉提示
            //     （SFX 圆圈）、不会起 trace。
            //   · trace 之外还有 0.35 秒节流的视觉/音效提示（内部 timeLastPinged）
            //
            // 所以「跟随连接」对本能力是**双重必要**：既是 trace 生效的前提，
            // 也是"别跑到哪打到哪"的约束（见 ProxyKillerConfig.ResolvedFollowConnection）。
            TryHostileAction(dest);
        }

        return true;
    }

    /// <summary>
    /// 当前"所盯节点"：跟随连接时取**当前所连节点**（连接变了就换），否则取启动时所在节点。
    /// </summary>
    private Computer WatchedComputer()
    {
        if (_cfg != null && _cfg.ResolvedFollowConnection)
        {
            Computer now = os?.connectedComp;
            if (now != null && !ReferenceEquals(now, _watched))
            {
                // 换节点 = 换了目标，重新起算"是否见过代理生效"
                _watched = now;
                _sawProxyActive = false;
            }
        }
        return _watched;
    }

    /// <summary>
    /// 过载完成自动关闭：所盯节点的代理由"生效"变为"已瘫痪"。
    ///
    /// <para>
    /// 必须**先见到过它生效**（<see cref="_sawProxyActive"/>），否则刚开程序时
    /// 一个本来就已瘫痪（或压根没有）代理的节点会被立刻判为"完成"而秒关。
    /// </para>
    /// </summary>
    private void CheckAutoCloseOnOverload()
    {
        if (_cfg == null || !_cfg.AutoCloseOnOverload) return;

        Computer w = WatchedComputer();
        if (w == null) return;

        if (w.hasProxy && w.proxyActive) { _sawProxyActive = true; return; }

        if (_sawProxyActive)
        {
            KELog.Info($"{LogTag} proxy on '{w.ip}' is down; closing (AutoCloseOnOverload).");
            isExiting = true;
        }
    }

    /// <summary>
    /// 跟随连接：玩家离开启动时所在的节点就关（<c>FollowConnection</c>）。
    ///
    /// <para>
    /// ⚠️ 自己过载时本项恒真（<see cref="ProxyKillerConfig.ResolvedFollowConnection"/> 已强制），
    /// 因为过载会持续触发敌对行为，不跟随会"跑到哪打到哪"。
    /// </para>
    /// </summary>
    private void CheckFollowConnection()
    {
        if (_cfg == null || !_cfg.ResolvedFollowConnection) return;
        if (string.IsNullOrEmpty(_homeIp)) return;

        string now = os?.connectedComp?.ip;
        if (now != _homeIp)
        {
            KELog.Info($"{LogTag} left node '{_homeIp}'"
                + (_cfg.SelfOverload ? " (self-overload forces follow)" : "")
                + "; exiting.");
            isExiting = true;
        }
    }

    /// <summary>按 ip 找一台机器（找不到返回 null）。</summary>
    private Computer FindComputerByIp(string ip)
    {
        if (string.IsNullOrEmpty(ip) || os?.netMap?.nodes == null) return null;
        for (int i = 0; i < os.netMap.nodes.Count; i++)
        {
            Computer c = os.netMap.nodes[i];
            if (c != null && c.ip == ip) return c;
        }
        return null;
    }

    /// <summary>
    /// 调目标机的 <c>hostileActionTaken()</c>（internal 类型上的 public 方法，走反射）。
    /// 失败只警告一次 —— 它只影响 trace / 音效，不影响过载本身。
    /// </summary>
    private void TryHostileAction(Computer dest)
    {
        try
        {
            hostileActionMethod?.Invoke(dest, null);
        }
        catch (Exception ex)
        {
            if (!_hostileWarned)
            {
                _hostileWarned = true;
                KELog.Warn($"{LogTag} could not fire hostile action: {ex.Message}");
            }
        }
    }

    /// <summary><c>Computer.hostileActionTaken()</c> 的反射句柄（找不到时为 null）。</summary>
    private static readonly System.Reflection.MethodInfo hostileActionMethod =
        typeof(Computer).GetMethod("hostileActionTaken",
            System.Reflection.BindingFlags.Instance
            | System.Reflection.BindingFlags.Public
            | System.Reflection.BindingFlags.NonPublic);

    private bool _hostileWarned;

    /// <summary>
    /// <c>ShellExe.destComp</c> —— 这个 shell 正在过载的那台机器。
    /// 过载进度（<c>proxyOverloadTicks</c>）在**它**身上，不在 shell 身上。
    /// </summary>
    private static readonly System.Reflection.FieldInfo destCompField =
        AccessTools.TypeByName(ShellExeTypeName)?.GetField("destComp",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public
            | System.Reflection.BindingFlags.NonPublic);

    /// <summary>
    /// 反射资源是否可用。**启动时查一次并记日志** —— 这类反射失败的默认行为是"静默跳过"，
    /// 不主动报出来就只能靠猜（曾经就因为误读 ShellExe.proxyOverloadTicks 这个不存在的字段，
    /// 表现成"加速完全没用"而日志里一点提示都没有）。
    /// </summary>
    private static bool ReflectionReady
        => AccessTools.Field(typeof(OS), "exes") != null && destCompField != null;

    private bool _accelerating;
    private bool _effectWarned;

    // ======================= Draw（照搬 DLCTraceSlower） =======================

    public override void Draw(float t)
    {
        base.Draw(t);
        drawOutline();
        drawTarget();

        // ⚠️ 这里**不能**因为 isExiting 就整体停画。
        //
        // 原版的退出机制是 ExeModule.Update 里 "ramCost = baseRamCost * fade; bounds.Height = ramCost"
        // —— 靠**窗口高度收缩到 0** 来退场（2 秒，FADEOUT_RATE = 0.5），不是靠绘制端淡出。
        // 原版 DLCTraceSlower 因此退出时不带任何 isExiting 判断，照常画，内容随窗口一起缩小消失。
        // 早退会让窗口框（drawOutline 画的那层）继续存在 2 秒、里面却全空 —— 看起来像"窗口还在但全没了"。
        //
        // 锁定屏是唯一例外：它要么整屏在，要么整屏不在（见下面的 isExiting 判断）。

        // 内容区：原版 SignalScramble 就是 Utils.InsetRectangle(bounds 去掉面板高, 2)
        Rectangle content = Utils.InsetRectangle(
            new Rectangle(bounds.X, bounds.Y + Module.PANEL_HEIGHT, bounds.Width, bounds.Height - Module.PANEL_HEIGHT), 2);

        // 窗口被 fade 收缩到内容装不下时，连背景也不用画了（原版在同一位置也有这道闸）
        if (content.Width <= 8 || content.Height <= 8) return;
        // Update 在 Draw 之前跑，背景的密度/速度幂律要用上一帧的高度
        _lastAreaHeight = content.Height;

        if (_locked)
        {
            // 锁定屏是"要么全在、要么全无"：退出瞬间连背景一起停画（不参与淡出）
            if (isExiting) return;

            // ⚠️ 锁定态**没有** `_cfg`，所以用不了作者配的背景参数
            //    （那 8 个值全在 `_cfg` 上）—— 直接用 FlowFieldEffect 自带的默认参数，
            //    颜色跟随 `os.lockedColor`（主题的"锁定"语义色）。
            //    这一屏只停留 3 秒，背景的作用是"别是一块死画面"，不需要配置化。
            try
            {
                _flow.Opacity = 0.55f;
                _flow.Draw(spriteBatch, content, os.lockedColor, LockedLoad());
            }
            catch { /* 背景是装饰：画不出来也不该影响"锁定"这件事被看见 */ }

            // 只有一个词，居中（水平 + 垂直）。锁定原因不显示 ——
            // 「没 flag」是剧情的一部分，「多 flag / 加载失败」是作者的事（进日志与终端）。
            string locked = KELoc.Loc("PROXY_KILLER_LOCKED_TITLE", "LOCKED");
            Vector2 lockedSize = GuiData.font.MeasureString(locked);
            spriteBatch.DrawString(GuiData.font, locked,
                new Vector2(content.X + (content.Width - lockedSize.X) * 0.5f,
                            content.Y + (content.Height - lockedSize.Y) * 0.5f),
                Color.White, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);
            return;
        }
        if (_cfg == null) return;

        DrawFlowBackground(content);

        // ── 底部一条通栏：左边是 Exit 按钮，右边是状态文字，两者**叠在同一条上** ──
        //
        // 原版 SignalScramble 是"状态文字带"与"按钮条"两条分开的（文字带在按钮上方）。
        // 这里合成一条：黑框只从按钮右侧开始，状态文字直接落在按钮条上。
        // ⚠️ 绘制顺序必须是 通栏底 → 按钮 → 文字。按钮后画会盖住文字，反过来才能让
        // 文字叠在按钮条上（这正是要的效果）。
        const int bandH = 25;
        Rectangle bar = new(content.X, content.Y + content.Height - bandH, content.Width, bandH);

        // 通栏底色。**不加 fade** —— 原版退场靠窗口高度收缩到 0 把内容裁掉，
        // 位置重排之后贴着按钮的那条本来就跟着窗口一起缩短，再加淡出是多余的。
        spriteBatch.Draw(Utils.white, bar, Color.Black * 0.95f);

        // Exit 按钮占左边一小块；其余留给状态文字（黑框"起始位置在右边" = 只压住右半边）
        int btnW = Math.Min(bar.Width, 60);
        Rectangle btn = new(bar.X, bar.Y, btnW, bar.Height);
        if (Button.doButton(PID + ID_EXIT, btn.X + 1, btn.Y, btn.Width - 2, btn.Height,
                KELoc.Loc("PROXY_KILLER_BTN_EXIT", "Exit"), os.lockedColor))
        {
            KELog.Info($"{LogTag} exit requested by player.");
            isExiting = true;
        }

        // 状态文字区：按钮右侧，留 4px 间隙
        Rectangle textArea = new(btn.X + btn.Width + 4, bar.Y + 2,
            Math.Max(10, bar.Right - (btn.X + btn.Width + 4) - 2), bar.Height - 4);

        // 状态文字：语义对齐原版 DLCTraceSlower，但把"抑制追踪"换成"加速 shell 过载"。
        //
        // 原版是三个状态词（"[ Spinning up... ]" / "[ Suppression Active ]" /
        // "[ Spinning down... ]"），括号包起来、读起来像机器在报自己的状态。
        //   爬升中   → "Spinning up..." + 进度读数（机械起转，原版同款词）
        //   已爬满   → "Overdrive"（**超负荷运转** —— 对应原版的 "Suppression Active"）
        //   退出中   → "Spinning down..."（卸荷，原版同款）
        //
        // 三级连起来是一条完整的负载曲线：升速 → 被逼到超负荷 → 降速。
        // 刻意不用 "Accelerating" 这类词：那是描述**动作**，而这里要报的是**负载状态**。
        float progress = RampProgress();
        bool rampDone = _ram >= _target;
        string label = isExiting
            ? KELoc.Loc("PROXY_KILLER_SPINDOWN", "Spinning down...")
            : rampDone
                ? KELoc.Loc("PROXY_KILLER_ACTIVE", "Overdrive")
                : KELoc.Loc("PROXY_KILLER_SPINUP", "Spinning up...");
        string text = "[ " + label + " ]";
        string readout = (!isExiting && !rampDone)
            ? (progress * 100f).ToString("00.00", System.Globalization.CultureInfo.InvariantCulture) + "%"
            : null;

        Vector2 size = GuiData.font.MeasureString(text);
        float readoutScale = readout != null ? 0.7f : 1f;
        float scale = Math.Min(1f, Math.Min(textArea.Width * readoutScale / size.X,
            textArea.Height / size.Y));
        Vector2 offset = new(textArea.Width - size.X * scale,
            textArea.Height / 2f - size.Y * scale / 2f);
        Color textColor = Color.Lerp(Color.White, os.highlightColor, 0.4f + Utils.randm(0.1f));
        spriteBatch.DrawString(GuiData.font, text, new Vector2(textArea.X, textArea.Y) + offset,
            textColor, 0f, Vector2.Zero, new Vector2(scale), SpriteEffects.None, 0.5f);

        // 读数逐字符铺在文字左侧（原版同款版式）
        if (readout != null)
        {
            Vector2 pos = new(textArea.X + 2f, textArea.Y + offset.Y);
            for (int i = 0; i < readout.Length; i++)
            {
                spriteBatch.DrawString(GuiData.font, readout[i].ToString(),
                    pos + (readout[i] == '.' ? new Vector2(3f, 0f) : Vector2.Zero),
                    textColor, 0f, Vector2.Zero, new Vector2(scale), SpriteEffects.None, 0.5f);
                pos.X += size.X / text.Length * scale + 2f;
            }
        }
    }

    /// <summary>
    /// 背景：**上升数据流**（自下而上的粒子洪流）。
    ///
    /// <para>
    /// 三个量都挂在 RAM 爬升进度上 —— 这就是"过载"在背景上的表达：
    /// <list type="bullet">
    ///   <item><b>速度</b>随进度升到 <c>LoadSpeedGain</c> 倍（数据冲得更快）</item>
    ///   <item><b>拖尾</b>随进度变长（越快尾巴越长，这是"高速"最直接的视觉线索）</item>
    ///   <item>整体<b>不透明度</b>随进度淡入，退出时乘 <c>fade</c>（与窗口一起退场）</item>
    /// </list>
    /// </para>
    ///
    /// <para>
    /// 不透明度这样给：启动时 <c>0.35 → 1</c> 淡入（让程序出现得不那么突兀）。
    /// **退出时不再乘 <c>fade</c>** —— 原版的退场靠窗口高度收缩到 0，内容本来就该在
    /// 收缩期间保持原样跟着一起被裁掉；再乘一层 fade 会让它过早变暗、看着像"没在动"。
    /// </para>
    /// </summary>
    private void DrawFlowBackground(Rectangle area)
    {
        if (_cfg == null) return;

        try
        {
            // auto 时每帧取主题高亮色（主题中途变化也能跟上）
            Color tint = _bgColorFixed ? _bgColor : os.highlightColor;

            // 用同一个负荷值决定速度/拖尾/淡入，三者天然同步
            float load = BackgroundLoad();
            _flow.Opacity = MathHelper.Lerp(0.35f, 1f, load);
            _flow.Draw(spriteBatch, area, tint, load);
        }
        catch (Exception ex)
        {
            if (!_bgWarned)
            {
                _bgWarned = true;
                KELog.Warn($"{LogTag} background draw failed: {ex.Message}");
            }
        }
    }

    /// <summary>程序被移除时的收尾（背景效果不需要释放，它不持有非托管资源）。</summary>
    public override void OnComplete()
    {
        _flow.Reset();
        base.OnComplete();
    }
}
