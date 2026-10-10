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

    /// <summary>
    /// RAM 爬升速率（MB/s）。**固定不可配**，照搬原版 SignalScramble 的 200。
    /// 50 → 100 用时 0.25 秒；要它慢下来就把 <c>BurstTarget</c> 调高。
    /// </summary>
    public const float RampRate = 200f;

    /// <summary>RAM 爬升目标（MB）的默认值。</summary>
    public const int DefaultBurstTarget = 100;

    /// <summary>倍速目标默认值：1.0 = 原版过载速度（不加速）。</summary>
    public const float DefaultShellOverloadTarget = 1f;

    /// <summary>数值项范围。</summary>
    public const float MinRam = 0f;
    public const float MaxRam = 100000f;
    public const float MinShellOverloadTarget = 0f;
    public const float MaxShellOverloadTarget = 1000f;

    // ---- 背景（DepthDotGrid）默认参数：取自原版 SignalScramble 的那次调用 ----

    /// <summary>递归时每层内缩的像素数（<c>pixelsInOnRecurse</c>）。</summary>
    public const float DefaultPixelsInOnRecurse = 30f;
    /// <summary>递归层数（<c>recursionSteps</c>）。</summary>
    public const int DefaultRecursionSteps = 10;
    /// <summary>点间距（<c>dotSeperation</c>）。</summary>
    public const float DefaultDotSeparation = 60f;
    /// <summary>点大小（<c>dotSize</c>）。</summary>
    public const float DefaultDotSize = 10f;
    /// <summary>最大景深偏移距离（<c>MaxDepthEffectDistance</c>）。</summary>
    public const float DefaultMaxDepthDistance = 0f;

    // ======================= XML 原始值（string 承接） =======================

    /// <summary>终端与窗口顶部显示的程序名。留空 = <see cref="DefaultProgramName"/>（注册名不受影响）。</summary>
    [XmlElement("ProgramName")] public string ProgramNameRaw { get; set; }

    /// <summary>RAM 爬升目标（MB）。留空 = <see cref="DefaultBurstTarget"/>（100）。</summary>
    [XmlElement("BurstTarget")] public string BurstTargetRaw { get; set; }

    /// <summary>
    /// **shell 过载**的倍速目标值。留空 = <see cref="DefaultShellOverloadTarget"/>（1.0 = 不加速）。
    ///
    /// 倍速由 RAM 爬升线性驱动：<c>1 → 目标值</c> 对应 <c>50MB → BurstTarget</c>
    /// （两者共用同一个进度，所以"占的内存越多、加速越强"能互相印证）。
    /// <c>2.0</c> = 两倍。N 个 shell 的总速率 = <c>N × 倍速</c>。
    /// </summary>
    [XmlElement("ShellOverloadTarget")] public string ShellOverloadTargetRaw { get; set; }

    // ---- 背景参数（只在加载时解析一次，运行时不变）----

    /// <summary>背景粒子颜色。留空 / <c>NONE</c> = <b>白色</b>。支持 CustomColor 预设名与动态色。</summary>
    [XmlElement("BackgroundColor")] public string BackgroundColorRaw { get; set; }

    /// <summary>递归每层内缩像素（<c>pixelsInOnRecurse</c>）。留空 = 30。</summary>
    [XmlElement("PixelsInOnRecurse")] public string PixelsInOnRecurseRaw { get; set; }

    /// <summary>递归层数（<c>recursionSteps</c>）。留空 = 10。</summary>
    [XmlElement("RecursionSteps")] public string RecursionStepsRaw { get; set; }

    /// <summary>点间距（<c>dotSeperation</c>）。留空 = 60。</summary>
    [XmlElement("DotSeparation")] public string DotSeparationRaw { get; set; }

    /// <summary>点大小（<c>dotSize</c>）。留空 = 10。</summary>
    [XmlElement("DotSize")] public string DotSizeRaw { get; set; }

    /// <summary>最大景深偏移距离（<c>MaxDepthEffectDistance</c>）。留空 = 0。</summary>
    [XmlElement("MaxDepthDistance")] public string MaxDepthDistanceRaw { get; set; }

    // ======================= 解析结果 =======================

    /// <summary>终端与窗口顶部显示的程序名。</summary>
    [XmlIgnore] public string ProgramName { get; private set; } = DefaultProgramName;

    /// <summary>RAM 爬升目标（MB）。</summary>
    [XmlIgnore] public int BurstTarget { get; private set; } = DefaultBurstTarget;

    /// <summary>shell 过载倍速的目标值。</summary>
    [XmlIgnore] public float ShellOverloadTarget { get; private set; } = DefaultShellOverloadTarget;

    /// <summary>背景粒子颜色。</summary>
    [XmlIgnore] public Color BackgroundColor { get; private set; } = Color.White;

    /// <summary>递归每层内缩像素。</summary>
    [XmlIgnore] public float PixelsInOnRecurse { get; private set; } = DefaultPixelsInOnRecurse;
    /// <summary>递归层数。</summary>
    [XmlIgnore] public int RecursionSteps { get; private set; } = DefaultRecursionSteps;
    /// <summary>点间距。</summary>
    [XmlIgnore] public float DotSeparation { get; private set; } = DefaultDotSeparation;
    /// <summary>点大小。</summary>
    [XmlIgnore] public float DotSize { get; private set; } = DefaultDotSize;
    /// <summary>最大景深偏移距离。</summary>
    [XmlIgnore] public float MaxDepthDistance { get; private set; } = DefaultMaxDepthDistance;

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

        ShellOverloadTarget = ConfigValue.ParseInRange(ShellOverloadTargetRaw, DefaultShellOverloadTarget,
            MinShellOverloadTarget, MaxShellOverloadTarget, out bool soOk);
        if (!soOk)
            KELog.Warn($"[ProxyKiller] '{SourceName}': invalid <ShellOverloadTarget> '{ShellOverloadTargetRaw}', "
                + $"expected {MinShellOverloadTarget}~{MaxShellOverloadTarget}; using {DefaultShellOverloadTarget}.");

        // 颜色走统一入口：这样才吃得到 CustomColor 预设名与动态色
        BackgroundColor = CustomColorManager.GetDynamicColor(BackgroundColorRaw, Color.White);

        PixelsInOnRecurse = ParseNum(PixelsInOnRecurseRaw, DefaultPixelsInOnRecurse, 0f, 1000f, "PixelsInOnRecurse");
        RecursionSteps = ToInt(ParseNum(RecursionStepsRaw, DefaultRecursionSteps, 0f, 64f, "RecursionSteps"));
        DotSeparation = ParseNum(DotSeparationRaw, DefaultDotSeparation, 1f, 1000f, "DotSeparation");
        DotSize = ParseNum(DotSizeRaw, DefaultDotSize, 0f, 500f, "DotSize");
        MaxDepthDistance = ParseNum(MaxDepthDistanceRaw, DefaultMaxDepthDistance, 0f, 10000f, "MaxDepthDistance");
    }

    /// <summary>解析一个数值背景参数：留空 = 默认（正常省略，不记日志）；畸形 / 超范围 = 回退 + Warn。</summary>
    private float ParseNum(string raw, float fallback, float min, float max, string name)
    {
        float v = ConfigValue.ParseInRange(raw, fallback, min, max, out bool ok);
        if (!ok)
            KELog.Warn($"[ProxyKiller] '{SourceName}': invalid <{name}> '{raw}', expected {min}~{max}; using {fallback}.");
        return v;
    }

    /// <summary>float → int 的安全取整（先夹到 int 范围内，避免大值溢出成负数）。</summary>
    private static int ToInt(float v)
    {
        if (v <= 0f) return 0;
        return v >= int.MaxValue ? int.MaxValue : (int)v;
    }
}
