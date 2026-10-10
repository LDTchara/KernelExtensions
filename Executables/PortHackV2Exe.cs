using Hacknet;
using Hacknet.Gui;
using KernelExtensions.Configs;
using KernelExtensions.Managers;
using KernelExtensions.Utilities;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Pathfinder.Executable;
using Pathfinder.Port;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace KernelExtensions.Executables;

/// <summary>
/// PortHackV2 —— 原版 PortHack 的可配置重制版。
///
/// <para>
/// 入口是**存档 flag**（`CustomPortHackV2_&lt;配置文件名&gt;`），不是命令行参数：
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
        this.ramCost = 30;
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

        CurrentState = PortHackV2State.Listing;
    }

    /// <summary>
    /// 找出存档里所有属于本程序的 flag（`CustomPortHackV2_*`）。
    /// 正常情况下应恰好一个（flag = 入口牌）；0 个 → 锁定；多个 → 作者配置错误。
    /// </summary>
    private static List<string> FindConfigFlags()
    {
        List<string> found = new();
        ProgressionFlags pf = OS.currentInstance?.Flags;
        if (pf == null) return found;

        // ProgressionFlags 只暴露 HasFlag / GetFlagStartingWith（后者仅返回第一个），
        // 没有"枚举全部"的公开 API；要检出多个 flag 只能反射读内部 List。
        // 反射失败时退化为"取第一个匹配"——仍能工作，只是检不出多 flag 的情形。
        try
        {
            if (_progressionFlagsField?.GetValue(pf) is List<string> all)
            {
                foreach (string f in all)
                    if (f != null && f.StartsWith(PortHackV2Config.FlagPrefix, StringComparison.Ordinal))
                        found.Add(f);
            }
        }
        catch (Exception ex)
        {
            KELog.Warn($"[PortHackV2] could not enumerate flags ({ex.Message}); falling back to first match.");
            string one = pf.GetFlagStartingWith(PortHackV2Config.FlagPrefix);
            if (!string.IsNullOrEmpty(one)) found.Add(one);
        }
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

    /// <summary>所有前置与协议前置端口是否都满足（Listing → Cracking 的判据）。</summary>
    private bool AllPrerequisitesMet()
    {
        if (CurrentConfig == null) return false;
        if (!CurrentConfig.Usable) return false;
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

        // Exit 按钮在除演出外的所有状态都可用
        if (CurrentState != PortHackV2State.Complete && CurrentState != PortHackV2State.Failed
            && Button.doButton(PID + ID_EXIT, bounds.X + bounds.Width - 62, bounds.Y + bounds.Height - 30,
                56, 24, KELoc.Loc("PORT_HACK_V2_EXIT", "Exit"), os.lockedColor))
        {
            isExiting = true;
        }
    }

    // ---- Locked ----

    private void DrawLocked()
    {
        int cx = bounds.X + bounds.Width / 2;
        string title = KELoc.Loc("PORT_HACK_V2_LOCKED_TITLE", "LOCKED");
        Vector2 ts = GuiData.font.MeasureString(title);
        spriteBatch.DrawString(GuiData.font, Utils.FlipRandomChars(title, 0.05),
            new Vector2(cx - ts.X / 2f, ContentTop + 24), os.lockedColor, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);

        if (!string.IsNullOrEmpty(_lockReason))
        {
            Vector2 rs = GuiData.smallfont.MeasureString(_lockReason);
            spriteBatch.DrawString(GuiData.smallfont, _lockReason,
                new Vector2(cx - rs.X / 2f, ContentTop + 54), Color.White * 0.75f,
                0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);
        }

        string closing = KELoc.Format("PORT_HACK_V2_CLOSING", "Closing in {0}s",
            Math.Max(1, (int)Math.Ceiling(_stateTimer)));
        TextItem.doFontLabel(new Vector2(bounds.X + 4, ContentBottom - 18), closing,
            GuiData.detailfont, Color.White * 0.5f);
    }

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
        int x = InnerX;
        int w = InnerW;

        string title = string.IsNullOrEmpty(CurrentConfig.Title)
            ? KELoc.Loc("PORT_HACK_V2_LISTING_TITLE", "PortHackV2")
            : CurrentConfig.Title;
        TextItem.doFontLabel(new Vector2(x, ContentTop), title, GuiData.font, _accent);

        List<ListingRow> rows = BuildListingRows();
        int y = ContentTop + 26;
        const int rowH = 30;

        if (rows.Count == 0)
        {
            TextItem.doFontLabel(new Vector2(x, y),
                KELoc.Loc("PORT_HACK_V2_NO_PREREQ", "No prerequisites - starting..."),
                GuiData.smallfont, Color.White * 0.7f);
        }

        foreach (ListingRow row in rows)
        {
            if (y + rowH > ContentBottom - 24) break;    // 行数超出可视区就不画了（不裁切半行）

            Rectangle bar = new Rectangle(x, y, w, rowH - 4);
            Color bg = row.Met ? os.unlockedColor : os.lockedColor;
            RenderedRectangle.doRectangle(bar.X, bar.Y, bar.Width, bar.Height, bg * 0.85f);

            // 左侧状态点：满足 = 高亮，未满足 = 暗
            RenderedRectangle.doRectangle(bar.X + 4, bar.Y + 4, 4, bar.Height - 8,
                row.Met ? _accent : os.darkBackgroundColor);

            spriteBatch.DrawString(GuiData.smallfont, row.Label,
                new Vector2(bar.X + 16, bar.Y + 4), Color.White * 0.95f);
            spriteBatch.DrawString(GuiData.detailfont, row.Detail,
                new Vector2(bar.X + 16, bar.Y + bar.Height - 14), Color.White * 0.55f);

            y += rowH;
        }

        // 左下角：预计破解时长（成功分支）
        string est = KELoc.Format("PORT_HACK_V2_ESTIMATE", "Estimated time: {0}s", (int)Math.Ceiling(EstimatedDuration()));
        TextItem.doFontLabel(new Vector2(x, ContentBottom - 18), est, GuiData.detailfont, Color.White * 0.6f);

        // 程序被配置为不可用时，明确告知（否则玩家只看到一直停着）
        if (!CurrentConfig.Usable)
            TextItem.doFontLabel(new Vector2(x + w - 180, ContentTop + 2),
                KELoc.Loc("PORT_HACK_V2_NOT_USABLE", "NOT USABLE"), GuiData.smallfont, os.lockedColor);
    }

    // ---- Cracking ----

    private void DrawCracking()
    {
        int x = InnerX;
        int w = InnerW;

        string title = string.IsNullOrEmpty(CurrentConfig.Title)
            ? KELoc.Loc("PORT_HACK_V2_CRACKING_TITLE", "Cracking...")
            : CurrentConfig.Title;
        TextItem.doFontLabel(new Vector2(x, ContentTop), title, GuiData.font, _accent);

        // 动画区：批 3 由 PortHackV2Sequence 接管（此处先出 3D 画布）
        Rectangle anim = new Rectangle(x, ContentTop + 24, w, Math.Max(40, ContentBottom - ContentTop - 24 - 16 - 22 - 8));
        DrawAnimationArea(anim, opacity: 0.6f);

        // 每个协议一条独立进度条（各跑自己的成功/失败分支时长）
        int y = anim.Bottom + 4;
        foreach (ProtocolRuntime rt in _runtimes)
        {
            if (y + 12 > ContentBottom - 22) break;
            DrawProtocolBar(new Rectangle(x, y, w, 10), rt);
            y += 14;
        }

        // 底部：根级总进度条（走 <Time>）
        float total = CurrentConfig.Time <= 0f ? 1f : MathHelper.Clamp(_crackTime / CurrentConfig.Time, 0f, 1f);
        DrawProgressBar(new Rectangle(bounds.X + 2, bounds.Y + bounds.Height - 18, w, 16), total, showSeconds: true);
    }

    /// <summary>单个协议的进度条：左侧协议名，右侧百分比。</summary>
    private void DrawProtocolBar(Rectangle bar, ProtocolRuntime rt)
    {
        string label = ProtocolDisplayName(rt.Def.Name);
        bool running = rt.Started;

        spriteBatch.DrawString(GuiData.detailfont, label, new Vector2(bar.X, bar.Y - 2),
            running ? Color.White * 0.8f : Color.White * 0.35f);

        int barX = bar.X + 110;
        int barW = bar.Width - 110 - 40;
        if (barW <= 10) return;

        RenderedRectangle.doRectangle(barX, bar.Y, barW, bar.Height, Color.Black * 0.7f);
        if (running)
        {
            float p = rt.Progress;
            RenderedRectangle.doRectangle(barX, bar.Y, (int)(barW * p), bar.Height,
                (rt.Succeeded ? _accent : os.lockedColor) * 0.9f);

            string pct = $"{(int)(p * 100f)}%";
            spriteBatch.DrawString(GuiData.detailfont, pct,
                new Vector2(bar.X + bar.Width - 36, bar.Y - 2), Color.White * 0.7f);
        }
    }

    /// <summary>主题色进度条（衬底 + 进度条 + 百分比 + 剩余秒数）。</summary>
    private void DrawProgressBar(Rectangle bar, float progress, bool showSeconds)
    {
        RenderedRectangle.doRectangle(bar.X, bar.Y, bar.Width, bar.Height, os.darkBackgroundColor * 0.9f);
        RenderedRectangle.doRectangle(bar.X, bar.Y, (int)(bar.Width * progress), bar.Height, _accent);

        string pct = $"{(int)(progress * 100f)}%";
        Vector2 ps = GuiData.smallfont.MeasureString(pct);
        spriteBatch.DrawString(GuiData.smallfont, pct,
            new Vector2(bar.X + bar.Width / 2f - ps.X / 2f, bar.Y + (bar.Height - ps.Y) / 2f),
            Color.White, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);

        if (showSeconds)
        {
            int remaining = Math.Max(0, (int)(CurrentConfig.Time - _crackTime));
            string timeText = KELoc.Format("PORT_HACK_V2_SECONDS_LEFT", "{0}s", remaining);
            Vector2 tsz = GuiData.smallfont.MeasureString(timeText);
            spriteBatch.DrawString(GuiData.smallfont, timeText,
                new Vector2(bar.X + bar.Width - tsz.X - 4, bar.Y + (bar.Height - tsz.Y) / 2f),
                Color.White * 0.85f, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);
        }
    }

    // ---- Complete / Failed 演出 ----

    private void DrawOutro()
    {
        int x = InnerX;
        int w = InnerW;

        // 背景：立体继续慢转（成功）/ 停止（失败）
        Rectangle anim = new Rectangle(x, ContentTop + 20, w, ContentBottom - ContentTop - 20);
        DrawAnimationArea(anim, opacity: CurrentState == PortHackV2State.Complete ? 0.2f : 0.35f);

        bool win = CurrentState == PortHackV2State.Complete;
        // 中断收尾不是"破解失败"：根级没有 RNG，单个协议没破开由该协议自己的 OnFail 表达。
        // 这里的文案只说明"连接断了/人走了"。
        string line1 = win
            ? KELoc.Loc("PORT_HACK_V2_COMPLETE_1", "PASSWORD")
            : KELoc.Loc("PORT_HACK_V2_INTERRUPT_1", "CONNECTION");
        string line2 = win
            ? KELoc.Loc("PORT_HACK_V2_COMPLETE_2", "FOUND")
            : KELoc.Loc("PORT_HACK_V2_INTERRUPT_2", "LOST");

        int cx = bounds.X + bounds.Width / 2;
        int cy = bounds.Y + bounds.Height / 2;

        // 上下白线展开：0.4s 内从中心向两侧推开（对齐原版的 QuadraticOut 展开）
        float expand = Utils.QuadraticOutCurve(MathHelper.Clamp(_completeElapsed / 0.4f, 0f, 1f));
        int lineW = (int)(expand * (bounds.Width - 30f));
        if (lineW > 2)
        {
            int lineY1 = cy - 18;
            int lineY2 = cy + 46;
            spriteBatch.Draw(Utils.white, new Rectangle(cx - lineW / 2, lineY1, lineW, 2),
                Utils.AddativeWhite * fade);
            spriteBatch.Draw(Utils.white, new Rectangle(cx - lineW / 2, lineY2, lineW, 2),
                Utils.AddativeWhite * fade);
        }

        // 中央文案：两层乱序字（清晰层 + 低概率抖动层）叠出原版的"电子抖动"观感
        Color baseColor = win ? Color.White : os.lockedColor;
        DrawFlipText(line1, new Vector2(cx, cy - 12), baseColor);
        DrawFlipText(line2, new Vector2(cx, cy + 24), baseColor);

        if (win)
            DrawProgressBar(new Rectangle(bounds.X + 2, bounds.Y + bounds.Height - 18, w, 16), 1f, showSeconds: false);
    }

    /// <summary>居中绘制一行乱序字，并叠一层低概率抖动。</summary>
    private void DrawFlipText(string text, Vector2 center, Color color)
    {
        Vector2 size = GuiData.font.MeasureString(text);
        Vector2 pos = new(center.X - size.X / 2f, center.Y - size.Y / 2f);
        spriteBatch.DrawString(GuiData.font, Utils.FlipRandomChars(text, 0.012), pos, color * fade);
        spriteBatch.DrawString(GuiData.font, Utils.FlipRandomChars(text, 0.11), pos,
            Utils.AddativeWhite * 0.2f * fade * fade);
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

            _sequence.DrawOverlay(spriteBatch, area, opacity);
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

    /// <summary>刷新强调色：留空 / NONE 时返回白色（GetDynamicColor 内部已处理）。</summary>
    private void RefreshAccent()
        => _accent = CustomColorManager.GetDynamicColor(_accentStr, PortHackV2Config.DefaultAccentColor);

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
