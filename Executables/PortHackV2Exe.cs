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
        public PortHackV2ProtocolConfig Def;
        /// <summary>StartDelay 计时（Cracking 开始后累加），到点才启动本协议。</summary>
        public float DelayElapsed;
        /// <summary>本协议启动后经过的时间。</summary>
        public float Elapsed;
        /// <summary>本协议是否已成功（决定根级结算时开不开端口、给不给 admin）。</summary>
        public bool Succeeded;

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
    private string _lockReason;         // Locked 屏显示的原因
    private float _completeElapsed;     // Complete 演出已进行时长（也驱动白线展开）
    private int _successCount;          // 已成功协议数
    private bool _rootHooksFired;       // 根级结算只做一次
    private static readonly Random _rng = new();    // 各协议启动时的成功率掷点

    // ---------- 动画 ----------
    private readonly PortHackV2Sequence _sequence = new();
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
                _lockReason = "No configuration is armed.";
                KELog.Info("[PortHackV2] no config flag present - locked.");
            }
            else
            {
                _lockReason = "Multiple configurations are armed (authoring error).";
                KELog.Error($"[PortHackV2] {flags.Count} config flags present, expected exactly one: "
                    + string.Join(", ", flags));
                os.write("PortHackV2: multiple configurations are armed (authoring error).");
            }
            EnterLocked();
            return;
        }

        CurrentConfig = PortHackV2Config.LoadByFlag(flags[0]);
        if (CurrentConfig == null)
        {
            // 加载失败的原因上一句已经记进日志了；这里给玩家一个可读的锁定屏
            _lockReason = $"Configuration '{flags[0].Substring(PortHackV2Config.FlagPrefix.Length)}' failed to load.";
            os.write("PortHackV2: configuration could not be loaded.");
            EnterLocked();
            return;
        }

        ramCost = CurrentConfig.RamCost;

        // 动画参数交给序列；每帧还会按总时长重算一次时间轴，所以中途改配置也能立刻生效
        _sequence.Shape = CurrentConfig.PolyhedronShape;
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
        try
        {
            if (_progressionFlagsField?.GetValue(pf) is List<string> all)
            {
                enumerated = true;
                foreach (string f in all)
                    if (f != null && f.StartsWith(PortHackV2Config.FlagPrefix, StringComparison.Ordinal))
                        found.Add(f);
            }
        }
        catch (Exception ex)
        {
            KELog.Warn($"[PortHackV2] could not enumerate flags ({ex.Message}); falling back to first match.");
        }

        if (!enumerated)
        {
            // 反射不可用（字段改名 / 取不到）：退化为"取第一个匹配"，至少单 flag 能跑
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

    private static readonly FieldInfo _progressionFlagsField = typeof(ProgressionFlags)
        .GetField("Flags", BindingFlags.Instance | BindingFlags.NonPublic);

    private void EnterLocked()
    {
        CurrentState = PortHackV2State.Locked;
        _stateTimer = LOCKED_EXIT_SECONDS;
    }

    // ======================= 前置条件 =======================

    private Computer Target => os.connectedComp ?? os.thisComputer;

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

        string source = CurrentConfig.SourceName;
        PortHackV2Hooks.Run(os, CurrentConfig.OnStart, source, "OnStart");
        KELog.Info($"[PortHackV2] '{source}': cracking started with {_runtimes.Count} protocol(s), "
            + $"root time {CurrentConfig.Time}s.");
    }

    /// <summary>
    /// 推进单个协议：StartDelay 到点 → **掷一次 RNG**（分支就此定下）→ 跑分支时长 →
    /// 到点立刻结算本协议（自己的 OnComplete / OnFail），不等其它协议。
    /// </summary>
    private void UpdateProtocol(ProtocolRuntime rt, float t)
    {
        if (rt.Finished) return;

        if (!rt.Started)
        {
            rt.DelayElapsed += t;
            if (!rt.Started) return;    // 本帧还没到启动点

            // 启动瞬间掷一次：分支定下但不暴露（两条分支的演出完全一样，只有时长不同）
            rt.Succeeded = _rng.NextDouble() < rt.Def.SuccessChance;
            string protoSource = $"{CurrentConfig.SourceName}/{rt.Def.Name}";
            PortHackV2Hooks.Run(os, rt.Def.OnStart, protoSource, "OnStart");
            KELog.Info($"[PortHackV2] '{protoSource}': started "
                + $"(success shape={rt.Succeeded}, duration={(rt.Succeeded ? rt.Def.TimeIfSuccess : rt.Def.TimeIfFail)}s).");
        }

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
                KELog.Warn($"[PortHackV2] '{protoSource}': could not open port: {ex.Message}");
            }
            PortHackV2Hooks.Run(os, rt.Def.OnComplete, protoSource, "OnComplete");
        }
        else
        {
            PortHackV2Hooks.Run(os, rt.Def.OnFail, protoSource, "OnFail");
            if (forced)
                KELog.Warn($"[PortHackV2] '{protoSource}': did not finish within the root <Time>; "
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

        // 至少一个协议成功才夺 admin：全灭时不该白送管理员权限
        if (_successCount > 0 && CurrentConfig.TakeAdmin)
        {
            try { os.takeAdmin(_targetIp); }
            catch (Exception ex) { KELog.Warn($"[PortHackV2] '{source}': takeAdmin failed: {ex.Message}"); }
        }

        CurrentState = PortHackV2State.Complete;
        _completeElapsed = 0f;
        // 急刹从这一刻（根级 <Time> 跑满）才开始，而不是"匀速区间用完"时
        _sequence.CrackingFinished = true;
        _sequence.IdleSpin = true;      // 成功演出期间背景立体继续慢转
        PortHackV2Hooks.Run(os, CurrentConfig.OnComplete, source, "OnComplete");
        KELog.Info($"[PortHackV2] '{source}': crack finished, {_successCount}/{_runtimes.Count} protocol(s) succeeded.");
    }

    // ======================= Update =======================

    public override void Update(float t)
    {
        base.Update(t);
        if (isExiting) return;

        if (_targetIp == null) _targetIp = Target?.ip;

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
        foreach (ProtocolRuntime rt in _runtimes) UpdateProtocol(rt, t);

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

        // 退出按钮：仅 Locked / Listing（Cracking 起不给退，演出中也没有可退的）
        if (CanShowExitButton()
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
    /// ⚠️ **整屏内容都不参与淡出**：<c>isExiting</c> 一开始就整个停画
    /// —— 背景网格与文字在退出瞬间一起"啪"地消失（对齐 PhaseSwiftExe 的做法，
    /// 它整屏内容都不乘 <c>fade</c>），只留基类那层 shell 在淡出。
    /// 其余状态（Listing / Cracking / 演出）才走 <c>fade</c>。
    /// </summary>
    private void DrawLocked()
    {
        if (isExiting) return;   // 退出瞬间：背景与文字一起停止绘制，不做淡出

        Rectangle area = ContentArea;
        DrawBackground(area, applyFade: false);

        int cx = bounds.X + bounds.Width / 2;
        string title = KELoc.Loc("PORT_HACK_V2_LOCKED_TITLE", "LOCKED");
        Vector2 ts = GuiData.font.MeasureString(title);
        spriteBatch.DrawString(GuiData.font, Utils.FlipRandomChars(title, 0.05),
            new Vector2(cx - ts.X / 2f, ContentTop + 24), os.lockedColor, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);

        // 原因文本宽度自适应（文件名由作者给，可能很长）
        if (!string.IsNullOrEmpty(_lockReason))
        {
            Rectangle reasonRect = new(area.X + 8, ContentTop + 52, area.Width - 16, 20);
            TextItem.doFontLabelToSize(reasonRect, _lockReason, GuiData.smallfont, Color.White * 0.85f);
        }

        string closing = KELoc.Format("PORT_HACK_V2_CLOSING", "Closing in {0}s",
            Math.Max(1, (int)Math.Ceiling(_stateTimer)));
        TextItem.doFontLabel(new Vector2(bounds.X + 4, ContentBottom - 18), closing,
            GuiData.detailfont, Color.White * 0.5f);
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
    }

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
                Detail = "overload the target proxy"
            });

        foreach (string p in CurrentConfig.RequireOpenPortsBefore)
            rows.Add(new ListingRow
            {
                Label = ProtocolDisplayName(p),
                Met = PortRequirementMet(p),
                Detail = "open this port first"
            });

        return rows;
    }

    /// <summary>协议名 → 显示名；未注册的协议保留原文并标注，便于排查拼写错误。</summary>
    private static string ProtocolDisplayName(string protocol)
    {
        try
        {
            if (PortManager.IsPortRegistered(protocol))
                return PortManager.GetPortRecordFromProtocol(protocol).DefaultDisplayName;
        }
        catch { }
        return $"{protocol} <unknown>";
    }

    /// <summary>
    /// 预计破解时长：只显示**成功分支**的时长，避免从时长上剧透失败分支。
    /// 多协议并行时取最长的那个（最后一个跑完的）。
    /// </summary>
    private float EstimatedDuration()
    {
        float max = 0f;
        foreach (PortHackV2ProtocolConfig p in CurrentConfig.Protocols)
        {
            if (p == null) continue;
            float total = p.StartDelay + p.TimeIfSuccess;
            if (total > max) max = total;
        }
        return max;
    }

    private void DrawListing()
    {
        Rectangle area = ContentArea;
        DrawBackground(area);

        int x = InnerX;
        int w = InnerW;

        string title = string.IsNullOrEmpty(CurrentConfig.Title)
            ? KELoc.Loc("PORT_HACK_V2_LISTING_TITLE", "PortHackV2")
            : CurrentConfig.Title;
        TextItem.doFontLabel(new Vector2(x, ContentTop), title, GuiData.font, _accent * fade);

        List<ListingRow> rows = BuildListingRows();

        // 行高按可用高度自适应（行多时压缩，行少时保持原高）——避免小面板下越界
        int avail = Math.Max(0, ContentBottom - 22 - (ContentTop + 26));
        int rowH = rows.Count > 0 ? Math.Min(30, Math.Max(16, avail / rows.Count)) : 30;
        int y = ContentTop + 26;

        if (rows.Count == 0)
        {
            TextItem.doFontLabel(new Vector2(x, y),
                KELoc.Loc("PORT_HACK_V2_NO_PREREQ", "No prerequisites - starting..."),
                GuiData.smallfont, Color.White * (0.7f * fade));
        }

        foreach (ListingRow row in rows)
        {
            if (y + rowH > ContentBottom - 22) break;    // 行数超出可视区就不画了（不裁切半行）

            Rectangle bar = new Rectangle(x, y, w, rowH - 4);
            Color bg = row.Met ? os.unlockedColor : os.lockedColor;
            RenderedRectangle.doRectangle(bar.X, bar.Y, bar.Width, bar.Height, bg * (0.85f * fade));

            // 左侧状态点：满足 = 高亮，未满足 = 暗
            RenderedRectangle.doRectangle(bar.X + 4, bar.Y + 4, 4, Math.Max(2, bar.Height - 8),
                row.Met ? _accent * fade : os.darkBackgroundColor * fade);

            // 标签与说明各占一条带内的一半：说明放**上面**那条（原先把说明压在行底，
            // 行一压缩就贴到下一行去了；放上面与标题连读更顺，也不会被下一行盖住）
            int labelH = Math.Max(9, bar.Height / 2);
            TextItem.doFontLabelToSize(new Rectangle(bar.X + 16, bar.Y + 1, bar.Width - 22, labelH),
                row.Label, GuiData.smallfont, Color.White * (0.95f * fade));
            TextItem.doFontLabelToSize(
                new Rectangle(bar.X + 16, bar.Y + labelH, bar.Width - 22, Math.Max(8, bar.Height - labelH)),
                row.Detail, GuiData.detailfont, Color.White * (0.6f * fade));

            y += rowH;
        }

        // 左下角：预计破解时长（成功分支）
        string est = KELoc.Format("PORT_HACK_V2_ESTIMATE", "Estimated time: {0}s", (int)Math.Ceiling(EstimatedDuration()));
        TextItem.doFontLabel(new Vector2(x, ContentBottom - 18), est, GuiData.detailfont, Color.White * (0.6f * fade));
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
        DrawBackground(area);

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
        DrawBackground(area);

        // 背景：立体继续慢转（成功）/ 已停住（中断）
        DrawAnimationArea(area, opacity: CurrentState == PortHackV2State.Complete ? 0.2f : 0.35f);

        bool win = CurrentState == PortHackV2State.Complete;
        string text = win ? CurrentConfig.CompletionText : KELoc.Loc("PORT_HACK_V2_INTERRUPT_1", "CONNECTION");

        int cx = bounds.X + bounds.Width / 2;
        int cy = bounds.Y + bounds.Height / 2;

        // 上下白线展开：0.4s 内从中心向两侧推开
        float expand = Utils.QuadraticOutCurve(MathHelper.Clamp(_completeElapsed / 0.4f, 0f, 1f));
        int lineW = (int)(expand * (bounds.Width - 30f));
        if (lineW > 2)
        {
            spriteBatch.Draw(Utils.white, new Rectangle(cx - lineW / 2, cy - 20, lineW, 2),
                Utils.AddativeWhite * fade);
            spriteBatch.Draw(Utils.white, new Rectangle(cx - lineW / 2, cy + 26, lineW, 2),
                Utils.AddativeWhite * fade);
        }

        // 单行中央文案：宽度自适应，过长自动缩字号（作者可能配很长的 CompletionText）
        Rectangle textRect = new(area.X + 10, cy - 14, area.Width - 20, 28);
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
    /// 动态色（CC 预设、Rainbow、Gradient）必须**逐帧重算**，所以在 Update 里刷新而不是 Draw 里现算 ——
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
    /// 动态色（CC 预设、Rainbow、Gradient）必须**逐帧重算**，所以在 Update 里刷新而不是 Draw 里现算 ——
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
