using KernelExtensions.Managers;
using KernelExtensions.Utilities;
using Microsoft.Xna.Framework;
using System.Xml.Serialization;

namespace KernelExtensions.Configs;

/// <summary>
/// ProxyKiller 的配置：一个**加速代理瘫痪**的常驻程序。
///
/// <para>
/// 它做两件事，都不需要玩家操作：
/// <list type="number">
///   <item>把 RAM 从**固定的 50MB** 爬升到 <see cref="BurstTargetRaw"/>，之后一直占着 ——
///     这个过程同时驱动它自己的界面（<c>chaosPercent</c> 随爬升从 1 降到 0）</item>
///   <item>把**所有正在过载的 Shell** 的过载速率乘上一个倍速（<see cref="ShellOverloadTargetRaw"/>，
///     同样由 RAM 爬升从 1 线性驱动到目标值）</item>
/// </list>
/// </para>
///
/// <para>
/// ⚠️ **它自己不瘫痪任何代理** —— 原版把代理过载做成 Shell 的一个状态
/// （<c>ShellExe</c> 的 <c>proxyOverloadTicks -= t</c>），而 Shell 需要目标机 admin 才能开。
/// 那道门槛是设计护栏，本程序不去拆：没有 shell 在过载，它就只是占着内存跑动画。
/// N 个 shell 的总速率 = N × 倍速（原版多 shell 本来就是简单叠加）。
/// </para>
///
/// <para>
/// 视觉与 RAM 爬升逻辑照搬原版 <c>DLCTraceSlower</c>（<c>SignalScramble</c>）——
/// 那是原版唯一一个"自己把 RAM 吃掉"的程序。差别只有：目标 RAM 与倍速改成可配、
/// 背景参数可配、以及它原来是"减速 trace"而这里是"加速 shell 过载"。
/// </para>
///
/// <para>
/// 数值项一律用 string 承接原值：<c>XmlSerializer</c> 遇到非数字会**直接抛异常**，
/// 那会让整份配置加载失败、程序永远锁在 Locked。走 <see cref="ConfigValue.ParseInRange"/>
/// 宽松解析后，畸形值只回退默认 + Warn，配置其余部分照常生效。
/// </para>
/// </summary>
public class ProxyKillerConfig
{
    // ======================= 默认值与范围（单一事实来源） =======================

    /// <summary>同时显示在终端与窗口顶部的程序名。</summary>
    public const string DefaultProgramName = "ProxyKiller";

    /// <summary>
    /// **固定的**起始 RAM 占用（MB）。刻意不可配 —— 起始值是"程序刚起来"的状态，
    /// 让它可配只会制造"起始比目标还大"之类的无意义组合。取 50 与原版 SignalScramble 一致。
    /// </summary>
    public const int RamCost = 50;

    /// <summary>RAM 爬升速率（MB/s）的默认值，照搬原版 SignalScramble 的 200。</summary>
    public const float DefaultRampRate = 200f;

    /// <summary>RAM 爬升目标（MB）的默认值。</summary>
    public const int DefaultBurstTarget = 100;

    /// <summary>倍速目标默认值：1.0 = 原版过载速度（不加速）。</summary>
    public const float DefaultShellOverloadTarget = 1f;

    /// <summary>
    /// 数值项下限 / <see cref="BurstTarget"/> 的上限。
    ///
    /// <para>
    /// <see cref="MaxRam"/> 是**唯一保留的"数值上限"常量**，理由是它要转 <c>int</c>
    /// （超大的 <c>float</c> 转 <c>int</c> 会溢出成 <c>int.MinValue</c>）。
    /// 其余数值项一律**不限上限** —— 见 <see cref="MinShellOverloadTarget"/> 的说明。
    /// </para>
    /// </summary>
    public const float MinRam = 0f;
    public const float MaxRam = 100000f;

    /// <summary>
    /// 加速已有 Shell 的倍速下限（0 = 不加速）。
    ///
    /// <para>
    /// ⚠️ <b>刻意没有上限。</b> 这里曾经有 <c>MaxShellOverloadTarget = 1000f</c>、
    /// 而"自己过载"的速率上限曾经是 <c>1f</c> —— 两个都是**顺手拍的数**。
    /// 后果是作者写 <c>&lt;SelfOverloadRate&gt;10</c> 被
    /// <c>ConfigValue.ParseInRange</c> **静默回退成默认 1**，表现为"配置写了但行为不符"。
    /// </para>
    ///
    /// <para>
    /// 现约定：解析层只挡"不是一个能用的数"（非数字 / <c>NaN</c> / <c>Infinity</c>）。
    /// 数值平衡是**扩展作者的事**；数值本身也有原版兜底
    /// （如 <c>proxyOverloadTicks</c> 减到 0 以下就贴 0）。
    /// </para>
    /// </summary>
    public const float MinShellOverloadTarget = 0f;

    /// <summary>自己过载代理的默认开关：<c>false</c> = 不主动过载（保持既有行为）。</summary>
    public const bool DefaultSelfOverload = false;

    /// <summary>
    /// 自己过载速率的默认值：<c>1.0</c> = 与原版**一个** shell 的过载速度相同。
    ///
    /// <para>
    /// 开关打开而没写速率时用这个 —— 不是 0。"打开但速率为 0"只会是个什么都
    /// 不做的哑配置，不该是默认。
    /// </para>
    /// </summary>
    public const float DefaultSelfOverloadRate = 1f;

    /// <summary>跟随连接的默认值：<c>false</c> = 常驻（既有行为，零破坏）。</summary>
    public const bool DefaultFollowConnection = false;

    /// <summary>过载完成自动关闭的默认值：<c>false</c> = 常驻到玩家自己按 Exit。</summary>
    public const bool DefaultAutoCloseOnOverload = false;

    // ---- 背景（上升数据流）默认参数 ----

    /// <summary>
    /// <c>&lt;BackgroundColor&gt;</c> 的 <c>auto</c> 写法（大小写不敏感）——
    /// 取**主题高亮色**（<c>os.highlightColor</c>），即随主题变化而不是固定白。
    /// 留空 / <c>NONE</c> 同样按 auto 处理。
    /// </summary>
    public const string BackgroundColorAutoValue = "auto";

    /// <summary>基准窗口下想要的粒子数；与 <see cref="DefaultDensityExponent"/> 一起换算成幂律系数。</summary>
    public const int DefaultCountAtReference = 230;
    /// <summary>密度的基准窗口尺寸（像素，取绘制区高度）。</summary>
    public const float DefaultReferenceHeight = 200f;
    /// <summary>密度幂律指数（约 1/2，即数量随高度按平方根增长）。</summary>
    public const float DefaultDensityExponent = 0.5052f;
    /// <summary>基准高度下的速度（每秒像素）；与 <see cref="DefaultSpeedExponent"/> 一起换算成速度幂律。</summary>
    public const float DefaultSpeedAtReference = 300f;
    /// <summary>速度幂律指数。</summary>
    public const float DefaultSpeedExponent = 0.3135f;
    /// <summary>粒子（含拖尾）的线宽像素数。</summary>
    public const float DefaultParticlePixels = 2f;
    /// <summary>拖尾基础长度（像素）。</summary>
    public const float DefaultTrailPixels = 5f;
    /// <summary>满负荷时拖尾的倍数。</summary>
    public const float DefaultLoadTrailGain = 2f;
    /// <summary>满负荷时速度的倍数。</summary>
    public const float DefaultLoadSpeedGain = 2.6f;

    // ======================= XML 原始值（string 承接） =======================

    /// <summary>终端与窗口顶部显示的程序名。留空 = <see cref="DefaultProgramName"/>（注册名不受影响）。</summary>
    [XmlElement("ProgramName")] public string ProgramNameRaw { get; set; }

    /// <summary>RAM 爬升目标（MB）。留空 = <see cref="DefaultBurstTarget"/>（100）。</summary>
    [XmlElement("BurstTarget")] public string BurstTargetRaw { get; set; }

    /// <summary>
    /// RAM 爬升速率（MB/s）。留空 = <see cref="DefaultRampRate"/>（200，与原版一致）。
    ///
    /// 它同时决定两件事的快慢：**界面**上粒子由混沌趋于整齐的时长，
    /// 以及**两个能力**（shell 倍速、自己过载速率）从起始值爬到目标值的时长
    /// （三者共用同一个进度）。
    /// 默认 200 时 50 → 100 只要 0.25 秒，形同瞬间；想看清爬升过程就调小它
    /// （例如 8 ≈ 6.25 秒），不必去动 <c>BurstTarget</c>。
    ///
    /// <para>
    /// 写 <c>0</c> = **不爬，直接到位**：界面停在起始状态（粒子最混沌、读数 0%），
    /// 而两个能力**立刻是满值**。⚠️ 别把 0 当成"关掉加速" —— 它只是没有爬升过程；
    /// 要关掉某个能力请写它自己的开关 / 目标（<c>ShellOverloadTarget=1</c> / <c>SelfOverload=false</c>）。
    /// </para>
    /// </summary>
    [XmlElement("RampRate")] public string RampRateRaw { get; set; }

    /// <summary>
    /// **shell 过载**的倍速目标值。留空 = <see cref="DefaultShellOverloadTarget"/>（1.0 = 不加速）。
    ///
    /// 倍速由 RAM 爬升线性驱动：<c>1 → 目标值</c> 对应 <c>50MB → BurstTarget</c>
    /// （两者共用同一个进度，所以"占的内存越多、加速越强"能互相印证）。
    /// <c>2.0</c> = 两倍。N 个 shell 的总速率 = <c>N × 倍速</c>。
    /// </summary>
    [XmlElement("ShellOverloadTarget")] public string ShellOverloadTargetRaw { get; set; }

    /// <summary>
    /// **自己过载代理**的开关。留空 = <see cref="DefaultSelfOverload"/>（<c>false</c> = 不主动过载）。
    ///
    /// <para>
    /// 打开后本程序 <b>不用 shell 也能推进所盯节点的代理过载</b> —— 因此**不依赖目标机的
    /// 管理员权限**（原版里过载代理本来就不设权限门槛，门槛只在"运行 shell"上）。
    /// 速率见 <see cref="SelfOverloadRateRaw"/>。
    /// </para>
    ///
    /// <para>
    /// ⚠️ 打开后会**强制跟随连接**（见 <see cref="ResolvedFollowConnection"/>）：
    /// 过载会持续触发目标机的敌对行为（起 trace），
    /// 不跟随的话就是"跑到哪打到哪还爆 trace"，故此项优先于 <see cref="FollowConnectionRaw"/>。
    /// </para>
    /// </summary>
    [XmlElement("SelfOverload")] public string SelfOverloadRaw { get; set; }

    /// <summary>
    /// **自己过载**的速率倍数 —— <c>1.0</c> = **原版一个 shell 的过载速度**。
    /// 留空 = <see cref="DefaultSelfOverloadRate"/>（<c>1.0</c>）。**无上限**（有限性即可，见下）。
    ///
    /// <para>
    /// ⚠️ 曾经上限是 <c>1</c>，结果写 <c>10</c> 会被静默回退成默认 <c>1</c>。
    /// 现在只要求"是一个有限的非负数"；多快是扩展作者的设计自由。
    /// </para>
    ///
    /// <para>
    /// 仅在 <see cref="SelfOverloadRaw"/> 打开时起作用；写 <c>0</c> = 打开但速率恒为 0
    /// （等于没开，属作者笔误）。
    /// </para>
    ///
    /// <para>
    /// 与 <see cref="ShellOverloadTargetRaw"/> 的区别：那项是"加速**别人**正在做的过载"
    /// （<b>倍速</b>，1 = 不加速）；本项是"自己推进过载"的**绝对速率**（1 = 与一个原版 shell 等速）。
    /// </para>
    ///
    /// <para>
    /// 速率由 RAM 爬升线性驱动：<c>0 → 本值</c> 对应 <c>50MB → BurstTarget</c>，
    /// 即**开局几乎不推进、随内存爬升逐渐加速到本值**，之后保持。
    /// 与 shell 倍速共用同一个进度 —— 玩家调的是"给多少内存"，两个能力一起涨。
    /// </para>
    /// </summary>
    [XmlElement("SelfOverloadRate")] public string SelfOverloadRateRaw { get; set; }

    /// <summary>
    /// 是否**跟随连接**：<c>true</c> 时玩家离开启动时所在的节点就自动关闭。
    /// 留空 = <see cref="DefaultFollowConnection"/>（<c>false</c> = 常驻）。
    ///
    /// <para>
    /// <c>false</c>（常驻）时程序会一直加速**后来才开的** shell —— 每帧重新遍历，不看创建时机。
    /// ⚠️ 如果 <see cref="SelfOverloadRaw"/> 是有效值，本项**被忽略并强制为 true**（见该类说明）。
    /// </para>
    /// </summary>
    [XmlElement("FollowConnection")] public string FollowConnectionRaw { get; set; }

    /// <summary>
    /// **过载完成自动关闭**：所盯节点的代理从"生效"变为"已瘫痪"后自动关闭。
    /// 留空 = <see cref="DefaultAutoCloseOnOverload"/>（<c>false</c>）。
    ///
    /// <para>
    /// 所盯节点 = 跟随连接时的**当前所连节点**、否则是启动时所在节点（见
    /// <see cref="ResolvedFollowConnection"/>）。这就是"任务做完了别赖着"的那一项。
    /// </para>
    /// </summary>
    [XmlElement("AutoCloseOnOverload")] public string AutoCloseOnOverloadRaw { get; set; }

    // ---- 背景（上升数据流）参数 ----
    //
    // 背景效果的"密度"与"速度"都是幂律公式：n = C × 高度^k、v = C × 高度^k
    // （那个效果的内部实现，见 Utilities/FlowFieldEffect.cs）。
    // 这里**不直接暴露 C/k**，而是让作者填"基准尺寸下的目标值"——
    // 那样更直观（"这个窗口下我要多少粒子、多快"），由 ResolveValues 反解出系数。

    /// <summary>背景粒子颜色。留空 / <c>NONE</c> = <b>白色</b>。支持 CustomColor 预设名与动态色。</summary>
    [XmlElement("BackgroundColor")] public string BackgroundColorRaw { get; set; }

    /// <summary>基准尺寸下想要的粒子数。留空 = 230。</summary>
    [XmlElement("BackgroundCount")] public string BackgroundCountRaw { get; set; }

    /// <summary>基准尺寸（像素，指绘制区高度）。留空 = 200。</summary>
    [XmlElement("BackgroundReferenceHeight")] public string BackgroundReferenceHeightRaw { get; set; }

    /// <summary>基准尺寸下想要的速度（每秒像素）。留空 = 300。</summary>
    [XmlElement("BackgroundSpeed")] public string BackgroundSpeedRaw { get; set; }

    /// <summary>粒子与拖尾的线宽（像素）。留空 = 5。</summary>
    [XmlElement("ParticlePixels")] public string ParticlePixelsRaw { get; set; }

    /// <summary>拖尾基础长度（像素）。留空 = 27。</summary>
    [XmlElement("TrailPixels")] public string TrailPixelsRaw { get; set; }

    /// <summary>满负荷时拖尾的倍数。留空 = 2.0。</summary>
    [XmlElement("LoadTrailGain")] public string LoadTrailGainRaw { get; set; }

    /// <summary>满负荷时速度的倍数。留空 = 2.6 —— 这就是"加速"在背景上的体现。</summary>
    [XmlElement("LoadSpeedGain")] public string LoadSpeedGainRaw { get; set; }

    // ======================= 解析结果 =======================

    /// <summary>终端与窗口顶部显示的程序名。</summary>
    [XmlIgnore] public string ProgramName { get; private set; } = DefaultProgramName;

    /// <summary>RAM 爬升目标（MB）。</summary>
    [XmlIgnore] public int BurstTarget { get; private set; } = DefaultBurstTarget;

    /// <summary>RAM 爬升速率（MB/s）。</summary>
    [XmlIgnore] public float RampRate { get; private set; } = DefaultRampRate;

    /// <summary>shell 过载倍速的目标值。</summary>
    [XmlIgnore] public float ShellOverloadTarget { get; private set; } = DefaultShellOverloadTarget;

    /// <summary>是否**自己过载代理**（不依赖 shell / 管理员权限）。</summary>
    [XmlIgnore] public bool SelfOverload { get; private set; } = DefaultSelfOverload;

    /// <summary>
    /// 自己过载的速率倍数（<c>1</c> = 原版一个 shell 的过载速度）。
    /// 速率由 RAM 爬升线性驱动，与 <see cref="ShellOverloadTarget"/> 共用同一个进度。
    /// 仅在 <see cref="SelfOverload"/> 为 true 时有意义。
    /// </summary>
    [XmlIgnore] public float SelfOverloadRate { get; private set; } = DefaultSelfOverloadRate;

    /// <summary>
    /// 有效解析后的"是否跟随连接"。
    ///
    /// <para>
    /// ⚠️ <b><see cref="SelfOverload"/> 为 true 时强制为 <c>true</c></b>，
    /// 无视作者写的 <c>FollowConnection</c>：主动过载会持续触发目标机敌对行为（起 trace），
    /// 不跟随就是"跑到哪打到哪还爆 trace"。此项优先。
    /// </para>
    /// </summary>
    [XmlIgnore] public bool ResolvedFollowConnection { get; private set; } = DefaultFollowConnection;

    /// <summary>过载完成（所盯节点代理失效）后是否自动关闭。</summary>
    [XmlIgnore] public bool AutoCloseOnOverload { get; private set; } = DefaultAutoCloseOnOverload;

    /// <summary>背景粒子颜色的静态值（<see cref="BackgroundColorAuto"/> 为 true 时不用它）。</summary>
    [XmlIgnore] public Color BackgroundColor { get; private set; } = Color.White;

    /// <summary>
    /// true = <c>&lt;BackgroundColor&gt;</c> 写了 <c>auto</c>（或留空 / <c>NONE</c>）——
    /// 此时颜色取**主题高亮色**。
    ///
    /// <para>
    /// 为什么不在配置里直接取好：配置解析时拿不到 <c>os</c>（主题高亮色在它上面），
    /// 所以只能记住"要自动"，由 Exe 绘制时每帧取。顺带的好处是主题中途变化也能跟上。
    /// </para>
    /// </summary>
    [XmlIgnore] public bool BackgroundColorAuto { get; private set; }

    /// <summary>反解出的密度幂律系数（<c>n = Coefficient × 高度^DensityExponent</c>）。</summary>
    [XmlIgnore] public float BackgroundDensityCoefficient { get; private set; }
    /// <summary>反解出的速度幂律系数（<c>v = Coefficient × 高度^SpeedExponent</c>，单位每秒像素）。</summary>
    [XmlIgnore] public float BackgroundSpeedCoefficient { get; private set; }

    /// <summary>密度幂律指数（固定值，约 1/2）。</summary>
    [XmlIgnore] public float BackgroundDensityExponent => DefaultDensityExponent;
    /// <summary>速度幂律指数（固定值）。</summary>
    [XmlIgnore] public float BackgroundSpeedExponent => DefaultSpeedExponent;

    /// <summary>粒子与拖尾的线宽（像素）。</summary>
    [XmlIgnore] public float ParticlePixels { get; private set; } = DefaultParticlePixels;
    /// <summary>拖尾基础长度（像素）。</summary>
    [XmlIgnore] public float TrailPixels { get; private set; } = DefaultTrailPixels;
    /// <summary>满负荷时拖尾的倍数。</summary>
    [XmlIgnore] public float LoadTrailGain { get; private set; } = DefaultLoadTrailGain;
    /// <summary>满负荷时速度的倍数。</summary>
    [XmlIgnore] public float LoadSpeedGain { get; private set; } = DefaultLoadSpeedGain;

    /// <summary>本配置对应的 flag 名（不含前缀）；由加载器填入，供反查与日志使用。</summary>
    [XmlIgnore] public string SourceName { get; set; }

    // ======================= 加载（flag 入口） =======================

    /// <summary>
    /// 存档 flag 前缀。**flag 是本程序的入口牌**：
    /// 形如 <c>ProxyKiller_MyBurst</c> → 对应 <c>&lt;扩展根&gt;/ProxyKiller/MyBurst.xml</c>。
    /// 无 flag → 程序锁定；多 flag → 作者配置错误。
    /// </summary>
    public const string FlagPrefix = "ProxyKiller_";

    /// <summary>配置目录（扩展根下）。</summary>
    public const string ConfigDir = "ProxyKiller";

    /// <summary>
    /// 从存档 flag 反查并加载配置。
    /// 失败（越界 / 文件缺失 / 解析异常）返回 null，并记 Error（不静默）。
    /// </summary>
    public static ProxyKillerConfig LoadByFlag(string flag)
    {
        if (string.IsNullOrEmpty(flag)) return null;
        if (!flag.StartsWith(FlagPrefix, StringComparison.Ordinal))
        {
            KELog.Error($"[ProxyKiller] flag '{flag}' does not start with '{FlagPrefix}'.");
            return null;
        }

        string name = flag.Substring(FlagPrefix.Length);
        if (string.IsNullOrWhiteSpace(name))
        {
            KELog.Error($"[ProxyKiller] flag '{flag}' has an empty config name.");
            return null;
        }

        // 相对扩展根解析；越界由 KEPath 拦下（返回 null），不会读到扩展目录外
        string rel = $"{ConfigDir}/{name}.xml";
        string path = KEPath.ResolveInsideExtension(rel);
        if (path == null || !File.Exists(path))
        {
            KELog.Error($"[ProxyKiller] config not found for flag '{flag}': {rel}");
            return null;
        }

        try
        {
            var cfg = XmlConfigLoader.Load<ProxyKillerConfig>(path, rel);
            if (cfg == null)
            {
                KELog.Error($"[ProxyKiller] config '{rel}' deserialized to null.");
                return null;
            }
            cfg.SourceName = name;
            cfg.ResolveValues();
            return cfg;
        }
        catch (Exception ex)
        {
            KELog.Error($"[ProxyKiller] failed to load {rel}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 反序列化后解析并校验各字段。背景参数在这里**一次性**解析完 ——
    /// 运行时不再改（它们是形态而不是动画进度，中途变会让画面跳变）。
    /// </summary>
    private void ResolveValues()
    {
        ProgramName = string.IsNullOrWhiteSpace(ProgramNameRaw) ? DefaultProgramName : ProgramNameRaw.Trim();

        BurstTarget = ToInt(ConfigValue.ParseInRange(BurstTargetRaw, DefaultBurstTarget,
            MinRam, MaxRam, out bool btOk));
        if (!btOk)
            KELog.Warn($"[ProxyKiller] '{SourceName}': invalid <BurstTarget> '{BurstTargetRaw}', "
                + $"expected {MinRam}~{MaxRam}; using {DefaultBurstTarget}.");

        RampRate = ParseNum(RampRateRaw, DefaultRampRate, 0f, "RampRate");

        ShellOverloadTarget = ConfigValue.ParseInRange(ShellOverloadTargetRaw, DefaultShellOverloadTarget,
            MinShellOverloadTarget, float.MaxValue, out bool soOk);
        if (!soOk)
            KELog.Warn($"[ProxyKiller] '{SourceName}': invalid <ShellOverloadTarget> '{ShellOverloadTargetRaw}', "
                + $"expected a number >= {MinShellOverloadTarget}; using {DefaultShellOverloadTarget}.");

        // 自己过载：**开关**与**速率**分开两个字段 —— 一个字段同时表达"开不开"与"多快"
        // 会让 0 这个值承担两重含义（关掉 / 速率为零），作者无法表达"打开但速率待定"。
        SelfOverload = ParseBool(SelfOverloadRaw, DefaultSelfOverload);
        // 无上限：数值平衡是扩展作者的事。这里挡的只有"不是一个能用的数"
        // （非数字 / NaN / Infinity / 负数），不是"值看着太大"。
        SelfOverloadRate = ConfigValue.ParseInRange(SelfOverloadRateRaw, DefaultSelfOverloadRate,
            0f, float.MaxValue, out bool selfRateOk);
        if (!selfRateOk)
        {
            KELog.Warn($"[ProxyKiller] '{SourceName}': invalid <SelfOverloadRate> '{SelfOverloadRateRaw}', "
                + $"expected a finite number >= 0; using {DefaultSelfOverloadRate}.");
        }
        if (SelfOverload && SelfOverloadRate <= 0f)
        {
            // 开了但速率为 0 = 什么都不做的哑配置，多半是笔误
            KELog.Warn($"[ProxyKiller] '{SourceName}': <SelfOverload> is on but <SelfOverloadRate> is 0, "
                + "so self-overload will never advance (did you mean to turn <SelfOverload> off?).");
        }

        bool wantFollow = ParseBool(FollowConnectionRaw, DefaultFollowConnection);
        ResolvedFollowConnection = SelfOverload || wantFollow;
        if (SelfOverload && !wantFollow && !string.IsNullOrWhiteSpace(FollowConnectionRaw))
        {
            // 作者显式写了 false，但主动过载要求跟随 —— 说清楚为什么被覆盖
            KELog.Warn($"[ProxyKiller] '{SourceName}': <FollowConnection>false</FollowConnection> is ignored "
                + "because <SelfOverload> is active (self-overload always follows the connection).");
        }

        AutoCloseOnOverload = ParseBool(AutoCloseOnOverloadRaw, DefaultAutoCloseOnOverload);

        // 背景颜色：auto（含留空 / NONE）走主题高亮色，其余走统一颜色入口
        // （这样才吃得到 CustomColor 预设名与动态色）
        string bgRaw = BackgroundColorRaw?.Trim();
        BackgroundColorAuto = string.IsNullOrEmpty(bgRaw)
            || ConfigValue.IsNone(bgRaw)
            || bgRaw.Equals(BackgroundColorAutoValue, StringComparison.OrdinalIgnoreCase);
        BackgroundColor = BackgroundColorAuto
            ? Color.White                                  // 占位；实际由 Exe 每帧取主题高亮色
            : CustomColorManager.GetDynamicColor(bgRaw, Color.White);

        // ---- 背景：作者填"基准尺寸下的目标值"，这里反解出幂律系数 ----
        // 反解公式：n = C·h^k 且已知 (h0, n0) → C = n0 / h0^k
        float refH = ParseNum(BackgroundReferenceHeightRaw, DefaultReferenceHeight, 10f,
            "BackgroundReferenceHeight");
        float wantCount = ParseNum(BackgroundCountRaw, DefaultCountAtReference, 1f, "BackgroundCount");
        float wantSpeed = ParseNum(BackgroundSpeedRaw, DefaultSpeedAtReference, 1f, "BackgroundSpeed");

        BackgroundDensityCoefficient = wantCount / (float)Math.Pow(refH, DefaultDensityExponent);
        BackgroundSpeedCoefficient = wantSpeed / (float)Math.Pow(refH, DefaultSpeedExponent);

        ParticlePixels = ParseNum(ParticlePixelsRaw, DefaultParticlePixels, 1f, "ParticlePixels");
        TrailPixels = ParseNum(TrailPixelsRaw, DefaultTrailPixels, 2f, "TrailPixels");
        LoadTrailGain = ParseNum(LoadTrailGainRaw, DefaultLoadTrailGain, 1f, "LoadTrailGain");
        LoadSpeedGain = ParseNum(LoadSpeedGainRaw, DefaultLoadSpeedGain, 1f, "LoadSpeedGain");
    }

    /// <summary>
    /// 解析一个数值参数：留空 = 默认（正常省略，不记日志）；畸形 / 无效 = 回退 + Warn。
    ///
    /// <paramref name="max"/> 默认不限 —— 只有"语义有界"或"后续要转 int"才该给上限
    /// （见 <see cref="ConfigValue.ParseInRange"/> 的说明）。Warn 文案按是否给了上限分别措辞，
    /// 免得谎报一个并不存在的区间。
    /// </summary>
    private float ParseNum(string raw, float fallback, float min, string name, float max = float.MaxValue)
    {
        float v = ConfigValue.ParseInRange(raw, fallback, min, max, out bool ok);
        if (!ok)
        {
            string range = max >= float.MaxValue ? $">= {min}" : $"{min}~{max}";
            KELog.Warn($"[ProxyKiller] '{SourceName}': invalid <{name}> '{raw}', expected {range}; using {fallback}.");
        }
        return v;
    }

    /// <summary>
    /// 解析一个布尔项：留空 = 默认（正常省略，不记日志）；无法识别 = 回退 + Warn。
    /// （<c>ConfigValue</c> 只有数值与 NONE 的工具，布尔在这里就地处理。）
    /// </summary>
    private bool ParseBool(string raw, bool fallback)
    {
        if (string.IsNullOrWhiteSpace(raw)) return fallback;
        string v = raw.Trim();
        if (bool.TryParse(v, out bool b)) return b;
        KELog.Warn($"[ProxyKiller] '{SourceName}': invalid boolean '{raw}', expected true/false; using {fallback}.");
        return fallback;
    }

    /// <summary>float → int 的安全取整（先夹到 int 范围内，避免大值溢出成负数）。</summary>
    private static int ToInt(float v)
    {
        if (v <= 0f) return 0;
        return v >= int.MaxValue ? int.MaxValue : (int)v;
    }
}
