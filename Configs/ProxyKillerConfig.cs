using KernelExtensions.Managers;
using KernelExtensions.Utilities;
using Microsoft.Xna.Framework;
using System.Xml.Serialization;

namespace KernelExtensions.Configs;

/// <summary>
/// ProxyKiller 的配置：一个"加速目标代理瘫痪"的展示窗口。
///
/// 定位（2026-10-10 二次定位）：**它自己不瘫痪任何代理**，只**加速正在过载当前所连节点的 Shell**。
/// 原版把代理过载做成 Shell 的一个状态（<c>ShellExe</c> 的 <c>proxyOverloadTicks -= t</c>），
/// 而 Shell 需要目标机 admin 才能开 —— 那道门槛是设计护栏，本程序不去拆：
/// 没有 shell 在过载，它就什么都不做。它做的是把"力量倍增器"这件事显式化：
/// 速率 = 该节点上的 shell 数 × 当前倍速（原版多 shell 时本来就是简单叠加）。
///
/// 同时按配置把 RAM 从起始值爬到目标值，用背景动画表达这个过程（粒子由混沌趋于整齐），
/// 并提供一个退出按钮。
///
/// 数值项一律用 string 承接原值：<c>XmlSerializer</c> 遇到非数字会**直接抛异常**，
/// 那会让整份配置加载失败、程序永远锁在 Locked。走 <see cref="ConfigValue.ParseInRange"/>
/// 宽松解析后，畸形值只回退默认 + Warn，配置其余部分照常生效。
/// </summary>
public class ProxyKillerConfig
{
    // ======================= 默认值与范围（单一事实来源） =======================

    /// <summary>同时显示在终端与窗口顶部的程序名。</summary>
    public const string DefaultProgramName = "ProxyKiller";

    /// <summary>
    /// RAM 爬升的目标值（MB）。默认 <b>600</b>，与原版 SignalScramble
    /// （<c>DLCTraceSlower</c>，原版里唯一一个"自己把 RAM 吃掉"的程序）的目标一致。
    /// </summary>
    public const int DefaultBurstTarget = 600;

    /// <summary>本程序启动时的初始 RAM 占用（MB）—— 与原版 SignalScramble 的 50 一致。</summary>
    public const int DefaultRamCost = 50;

    /// <summary>RAM 爬升速率（MB/s）。默认 200，与原版一致（50→600 约 2.75 秒）。</summary>
    public const float DefaultRampRate = 200f;

    /// <summary>
    /// 代理过载倍速的目标值。默认 <b>1.0</b> = 原版过载速度（即"只展示、不加速"）。
    /// </summary>
    public const float DefaultProxyOverloadTarget = 1f;

    /// <summary>倍速范围：0 = 冻结该机器的过载进度，上限给足余量。</summary>
    public const float MinProxyOverloadTarget = 0f;
    public const float MaxProxyOverloadTarget = 1000f;

    /// <summary>数值项下限/上限。</summary>
    public const float MinRam = 0f;
    public const float MaxRam = 100000f;
    public const float MinRampRate = 0f;
    public const float MaxRampRate = 100000f;

    // ======================= XML 原始值（string 承接） =======================

    /// <summary>终端与窗口顶部显示的程序名。留空 = <see cref="DefaultProgramName"/>（注册名不受影响）。</summary>
    [XmlElement("ProgramName")] public string ProgramNameRaw { get; set; }

    /// <summary>
    /// RAM 爬升的目标值（MB）。留空 = <see cref="DefaultBurstTarget"/>（600）。
    /// 程序启动时按 <see cref="RamCostRaw"/> 占用，然后以 <see cref="RampRateRaw"/> 的速率爬到这里。
    /// </summary>
    [XmlElement("BurstTarget")] public string BurstTargetRaw { get; set; }

    /// <summary>启动时的 RAM 占用（MB）。留空 = <see cref="DefaultRamCost"/>（50）。</summary>
    [XmlElement("RamCost")] public string RamCostRaw { get; set; }

    /// <summary>RAM 爬升速率（MB/s）。留空 = <see cref="DefaultRampRate"/>（200）。</summary>
    [XmlElement("RampRate")] public string RampRateRaw { get; set; }

    /// <summary>
    /// **shell 过载**的倍速目标值。留空 = <see cref="DefaultProxyOverloadTarget"/>（1.0）。
    ///
    /// ⚠️ 本程序**自己不瘫痪任何代理** —— 它只加速正在过载**当前所连节点**的那些 Shell。
    /// 原版把代理过载做成 Shell 的一个状态（<c>ShellExe</c> 的 <c>proxyOverloadTicks -= t</c>），
    /// 而 Shell 需要目标机 admin 才能开 —— 那道门槛是设计上的护栏，本程序不去拆它：
    /// 没有 shell 在过载，它就什么都不做。
    ///
    /// 倍速**不是一次性跳到目标**，而是由 RAM 爬升线性驱动：
    /// <c>倍速 = 1 → 目标值</c> 对应 <c>RamCost → BurstTarget</c>（两者共用同一个进度）。
    /// 于是"占的内存越多、加速越强"两个数字能互相印证 —— 这也正是原版
    /// <c>DLCTraceSlower</c> 把 <c>ramCost</c> 映射到 trace 减速系数的做法。
    ///
    /// <c>1.0</c> = 原版过载速度（等于不加速）；<c>2.0</c> = 两倍。
    /// N 个 shell 的总速率 = <c>N × 倍速</c>。
    /// </summary>
    [XmlElement("ProxyOverloadTarget")] public string ProxyOverloadTargetRaw { get; set; }

    /// <summary>
    /// 强调色：背景粒子 + 退出按钮的颜色。留空 / <c>NONE</c> = <b>白色</b>。
    /// 支持 CustomColor 预设名与动态色（<c>LDTchara</c> / <c>Rainbow</c> / <c>Gradient:…</c>），
    /// 以及 <c>#RGB</c> / <c>#RRGGBB</c> / <c>#AARRGGBB</c> / <c>R,G,B[,A]</c> / XNA 命名色。
    /// </summary>
    [XmlElement("AccentColor")] public string AccentColorRaw { get; set; }

    /// <summary>强调色默认值：白色。</summary>
    [XmlIgnore] public static Color DefaultAccentColor => Color.White;

    /// <summary>
    /// 背景效果。<c>NONE</c> = 不画背景。取值见
    /// <see cref="ProxyKillerBackgroundEffect"/>；留空 = <c>DepthDotGrid</c>
    /// （原版 SignalScramble 用的就是它）。
    /// </summary>
    [XmlElement("BackgroundEffect")] public string BackgroundEffectRaw { get; set; }

    /// <summary>
    /// 玩家离开本程序所在节点时是否自动关闭。留空 = <b>true</b>
    /// （对齐原版 SignalScramble：它跟着所连节点走，断连即退）。
    /// </summary>
    public bool ExitOnDisconnect { get; set; } = true;

    // ======================= 解析结果 =======================

    /// <summary>终端与窗口顶部显示的程序名。</summary>
    [XmlIgnore] public string ProgramName { get; private set; } = DefaultProgramName;

    /// <summary>RAM 爬升目标（MB）。</summary>
    [XmlIgnore] public int BurstTarget { get; private set; } = DefaultBurstTarget;

    /// <summary>启动时的 RAM 占用（MB）。</summary>
    [XmlIgnore] public int RamCost { get; private set; } = DefaultRamCost;

    /// <summary>RAM 爬升速率（MB/s）。</summary>
    [XmlIgnore] public float RampRate { get; private set; } = DefaultRampRate;

    /// <summary>代理过载倍速的目标值（倍速由此线性爬到它）。</summary>
    [XmlIgnore] public float ProxyOverloadTarget { get; private set; } = DefaultProxyOverloadTarget;

    /// <summary>解析后的强调色（静态部分；动态色由消费端每帧取）。</summary>
    [XmlIgnore] public Color AccentColor { get; private set; } = Color.White;

    /// <summary>背景效果的解析结果。</summary>
    [XmlIgnore] public ProxyKillerBackgroundEffect BackgroundEffect { get; private set; }
        = ProxyKillerBackgroundEffect.DepthDotGrid;

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
            var serializer = new XmlSerializer(typeof(ProxyKillerConfig));
            using var fs = new FileStream(path, FileMode.Open);
            var cfg = (ProxyKillerConfig)serializer.Deserialize(fs);
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
    /// 反序列化后解析并校验数值字段、着色字段与背景效果。
    /// 与 <see cref="LoadByFlag"/> 分开，便于从其它入口加载时复用。
    /// </summary>
    private void ResolveValues()
    {
        ProgramName = string.IsNullOrWhiteSpace(ProgramNameRaw) ? DefaultProgramName : ProgramNameRaw.Trim();

        BurstTarget = ToInt(ConfigValue.ParseInRange(BurstTargetRaw, DefaultBurstTarget,
            MinRam, MaxRam, out bool btOk));
        if (!btOk)
            KELog.Warn($"[ProxyKiller] '{SourceName}': invalid <BurstTarget> '{BurstTargetRaw}', "
                + $"expected {MinRam}~{MaxRam}; using {DefaultBurstTarget}.");

        RamCost = ToInt(ConfigValue.ParseInRange(RamCostRaw, DefaultRamCost,
            MinRam, MaxRam, out bool rcOk));
        if (!rcOk)
            KELog.Warn($"[ProxyKiller] '{SourceName}': invalid <RamCost> '{RamCostRaw}', "
                + $"expected {MinRam}~{MaxRam}; using {DefaultRamCost}.");

        RampRate = ConfigValue.ParseInRange(RampRateRaw, DefaultRampRate,
            MinRampRate, MaxRampRate, out bool rrOk);
        if (!rrOk)
            KELog.Warn($"[ProxyKiller] '{SourceName}': invalid <RampRate> '{RampRateRaw}', "
                + $"expected {MinRampRate}~{MaxRampRate}; using {DefaultRampRate}.");

        ProxyOverloadTarget = ConfigValue.ParseInRange(ProxyOverloadTargetRaw, DefaultProxyOverloadTarget,
            MinProxyOverloadTarget, MaxProxyOverloadTarget, out bool potOk);
        if (!potOk)
            KELog.Warn($"[ProxyKiller] '{SourceName}': invalid <ProxyOverloadTarget> '{ProxyOverloadTargetRaw}', "
                + $"expected {MinProxyOverloadTarget}~{MaxProxyOverloadTarget}; using {DefaultProxyOverloadTarget}.");

        // 颜色走统一入口：这样才吃得到 CustomColor 预设名与动态色
        AccentColor = CustomColorManager.GetDynamicColor(AccentColorRaw, DefaultAccentColor);

        BackgroundEffect = ParseBackgroundEffect(BackgroundEffectRaw);
    }

    /// <summary>float → int 的安全取整（先夹到 int 范围内，避免大值溢出成负数）。</summary>
    private static int ToInt(float v)
    {
        if (v <= 0f) return 0;
        return v >= int.MaxValue ? int.MaxValue : (int)v;
    }

    /// <summary>
    /// 解析背景效果名（大小写不敏感）；留空 = 默认 <c>DepthDotGrid</c>（不记日志，那是正常省略）；
    /// 写了但无法识别 = 回退默认 + Warn（帮作者揪出拼写错误）。
    /// </summary>
    private ProxyKillerBackgroundEffect ParseBackgroundEffect(string raw)
    {
        if (ConfigValue.IsNone(raw)) return ProxyKillerBackgroundEffect.DepthDotGrid;

        if (Enum.TryParse(raw.Trim(), ignoreCase: true, out ProxyKillerBackgroundEffect effect))
            return effect;

        KELog.Warn($"[ProxyKiller] '{SourceName}': unknown <BackgroundEffect> '{raw}'; "
            + "expected NONE / DepthDotGrid / ZoomingDotGrid / HexGrid / Grid. Using DepthDotGrid.");
        return ProxyKillerBackgroundEffect.DepthDotGrid;
    }
}

/// <summary>
/// ProxyKiller 可选的面板背景效果（都取自游戏本体现成的 Effects，无需自带资源）。
/// </summary>
public enum ProxyKillerBackgroundEffect
{
    /// <summary>不画背景。</summary>
    None,
    /// <summary>多层递归点阵，景深感 —— 原版 SignalScramble（DLCTraceSlower）用的就是它。</summary>
    DepthDotGrid,
    /// <summary>从中心向外扩散旋转的点阵（PhaseSwift 用的）。</summary>
    ZoomingDotGrid,
    /// <summary>六边形蜂窝网格（原版 KaguyaTrial 用的）。</summary>
    HexGrid,
    /// <summary>带十字标记的交叉网格。</summary>
    Grid,
}
