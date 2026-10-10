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
    private string _lockedReason;
    private float _lockedTimer;

    /// <summary>当前实际 RAM 占用（浮点，爬升过程需要小数）。</summary>
    private float _ram;
    private float _ramStart;
    private float _target;
    private float _mulTarget;

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

        _bgColor = _cfg.BackgroundColor;
        // auto（或留空 / NONE）时颜色取主题高亮色，但**每帧取** —— 配置解析时拿不到 os，
        // 而且这样主题中途变化也能跟上。所以这里只把非 auto 的静态值定下来。
        if (!_cfg.BackgroundColorAuto) _bgColorFixed = true;

        // 起始值固定 50（不可配）；目标取配置值，且不小于起始值，避免"反向下滑"
        _ramStart = ProxyKillerConfig.RamCost;
        _target = Math.Max(_ramStart, _cfg.BurstTarget);
        _mulTarget = _cfg.ShellOverloadTarget;
        _ram = _ramStart;
        ramCost = (int)_ram;

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
            + $"shell overload x1 -> x{_mulTarget.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}.");

        // 反射可用性一次报清 —— 失败时加速会静默失效，必须让它可见
        if (ReflectionReady)
        {
            KELog.Info($"{LogTag} shell hooks resolved (OS.exes, ShellExe.destComp).");
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

    private void Lock(string reason)
    {
        _locked = true;
        _lockedReason = reason;
        _lockedTimer = LOCKED_EXIT_SECONDS;
        os.write(reason);
    }

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
            return;
        }
        if (_cfg == null) { isExiting = true; return; }

        // ⚠️ 退出时**不能**整体 return —— 背景必须继续跑。
        // 原版的退场是"窗口高度收缩到 0"，如果这里一停，收缩期间画面就是**凝固**的，
        // 而状态文字已经写着 "Spinning down..."，两者完全脱节（用户实测就是这个现象）。
        // 所以：退出时只跳过 RAM 爬升与 shell 加速（那两个没必要再算），背景照旧推进。

        // 本程序**常驻**：不跟随连接变化关闭（无 ExitOnDisconnect），只由玩家按 Exit 结束。
        // 这也意味着它可以一直加速**后来才开的** shell —— 每帧重新遍历，不看创建时机。
        if (!isExiting)
        {
            // RAM 线性爬升到目标，之后一直占着（原版 SignalScramble 同款机制，速率可配）
            if (_ram < _target && _cfg.RampRate > 0f)
            {
                _ram = Math.Min(_target, _ram + _cfg.RampRate * t);
                ramCost = (int)_ram;
            }

            // 倍速由 RAM 爬升线性驱动：倍速 1 → 目标值 对应 50MB → BurstTarget
            AccelerateShells(t);
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

    /// <summary>RAM 爬升进度 0~1（= 倍速爬升进度）。起始值 == 目标值时视为已满。</summary>
    private float RampProgress()
        => _target > _ramStart ? MathHelper.Clamp((_ram - _ramStart) / (_target - _ramStart), 0f, 1f) : 1f;

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
        float mul = MathHelper.Lerp(1f, _mulTarget, RampProgress());
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
                KELog.Info($"{LogTag} accelerating {hit} shell(s) overload.");
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
            if (!string.IsNullOrEmpty(_lockedReason))
                TextItem.doFontLabelToSize(
                    new Rectangle(content.X + 6, content.Y + content.Height / 2 - 10, content.Width - 12, 20),
                    _lockedReason, GuiData.smallfont, Color.White * 0.9f);
            return;
        }
        if (_cfg == null) return;

        DrawFlowBackground(content);

        // 状态文字带（原版 SignalScramble 在"图形区底部往上 2 行"的位置放它）
        const int bandH = 25;
        Rectangle band = new(content.X, content.Y + content.Height - bandH - 10 - bandH,
            (int)(content.Width * 0.92f), bandH + 2);
        band.Width += 8;
        spriteBatch.Draw(Utils.white, band, Color.Black * 0.9f);
        band.Width -= 8;
        band.Y += 4;
        band.Height -= 8;

        // 状态文字：语义对齐原版 DLCTraceSlower，但把"抑制追踪"换成"加速 shell 过载"。
        //
        // 原版是三个状态词（"[ Spinning up... ]" / "[ Suppression Active ]" /
        // "[ Spinning down... ]"），长句、括号包起来、读起来像机器在报自己的状态。
        // 这里照这个语气来：
        //   爬升中   → "Spinning up..." + 进度读数（机械起转，原版同款词）
        //   已爬满   → "Overdrive"（**超负荷运转** —— 对应原版的 "Suppression Active"）
        //   退出中   → "Spinning down..."（卸荷，原版同款）
        //
        // 三级连起来是一条完整的负载曲线：升速 → 被逼到超负荷 → 降速。
        // 刻意不用 "Accelerating" 这类词：那是描述**动作**，而这里要的是报**负载状态**。
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
        float scale = Math.Min(1f, Math.Min(band.Width * readoutScale / size.X, band.Height / size.Y));
        Vector2 offset = new(band.Width - size.X * scale, band.Height / 2f - size.Y * scale / 2f);
        Color textColor = Color.Lerp(Color.White, os.highlightColor, 0.4f + Utils.randm(0.1f));
        spriteBatch.DrawString(GuiData.font, text, new Vector2(band.X, band.Y) + offset,
            textColor, 0f, Vector2.Zero, new Vector2(scale), SpriteEffects.None, 0.5f);

        // 读数逐字符铺在文字左侧（原版同款版式）
        if (readout != null)
        {
            Vector2 pos = new(band.X + 2f, band.Y + offset.Y);
            for (int i = 0; i < readout.Length; i++)
            {
                spriteBatch.DrawString(GuiData.font, readout[i].ToString(),
                    pos + (readout[i] == '.' ? new Vector2(3f, 0f) : Vector2.Zero),
                    textColor, 0f, Vector2.Zero, new Vector2(scale), SpriteEffects.None, 0.5f);
                pos.X += size.X / text.Length * scale + 2f;
            }
        }

        // 底部 Exit 按钮（原版 SignalScramble 就是这条满宽黑条 + 按钮）
        // 按钮条画在内容区**内部底部**（压住一点背景没关系；放到内容区外面会盖到别的 exe 窗口）
        Rectangle btn = new(content.X, content.Y + content.Height - bandH, content.Width, bandH);
        spriteBatch.Draw(Utils.white, btn, Color.Black);
        if (Button.doButton(PID + ID_EXIT, btn.X + 1, btn.Y, btn.Width - 2, btn.Height,
                KELoc.Loc("PROXY_KILLER_BTN_EXIT", "Exit"), os.lockedColor))
        {
            KELog.Info($"{LogTag} exit requested by player.");
            isExiting = true;
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
