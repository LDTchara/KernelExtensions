using KernelExtensions.Utilities;
using Microsoft.Xna.Framework;
using System.Xml.Serialization;

namespace KernelExtensions.Configs;

/// <summary>
/// ProxyTuner（游戏内实时调节代理破解倍数，供扩展作者调参）的配置契约。
///
/// 入口与 PortHackV2 同构：存档 flag <c>CustomProxyTuner_&lt;文件名&gt;</c> 就是入口牌，
/// 反查出扩展根目录下 <c>ProxyTuner/&lt;文件名&gt;.xml</c>。无 flag → 程序锁定；
/// 出现多个 flag → 作者配置错误（两者都由主程序给出提示后自动退出）。
///
/// 数值项**统一用 string 承接原值**：<see cref="XmlSerializer"/> 遇到非数字
/// （例如 <c>&lt;Depth&gt;4 层&lt;/Depth&gt;</c>）会直接抛异常，导致**整份配置加载失败**；
/// 这里统一走 <see cref="ConfigValue.ParseInRange"/> 宽松解析 —— 畸形值
/// （非数字 / NaN / Infinity / 负数 / 超范围）只回退默认 + Warn，配置其余部分照常生效。
/// </summary>
public class ProxyTunerConfig
{
    // ======================= 默认值与范围（单一事实来源：解析与运行时调节共用） =======================

    /// <summary>过载倍数的默认值（1.0 = 原版速度）。</summary>
    public const float DefaultSpeedMultiplier = 1f;

    /// <summary>过载倍数下限（0 = 冻结，便于观察起始状态）。</summary>
    public const float MinSpeedMultiplier = 0f;

    /// <summary>过载倍数上限。</summary>
    public const float MaxSpeedMultiplier = 100f;

    /// <summary>本程序 RAM 占用的默认值（MB）。</summary>
    public const int DefaultRamCost = 100;

    /// <summary>RAM 占用下限（MB）。</summary>
    public const float MinRamCost = 0f;

    /// <summary>RAM 占用上限（MB）。</summary>
    public const float MaxRamCost = 100000f;

    /// <summary>动画推进速率的默认值（1.0 = 原始推进速度）。</summary>
    public const float DefaultAnimationSpeed = 1f;

    /// <summary>动画推进速率下限。</summary>
    public const float MinAnimationSpeed = 0f;

    /// <summary>动画推进速率上限。</summary>
    public const float MaxAnimationSpeed = 100f;

    /// <summary>递归步数默认值。</summary>
    public const int DefaultDepth = 4;

    /// <summary>递归步数下限。</summary>
    public const float MinDepth = 0f;

    /// <summary>递归步数上限（原版效果每层点大小乘 0.6，实际层数会自行提前收敛）。</summary>
    public const float MaxDepth = 64f;

    /// <summary>点大小默认值（像素）。</summary>
    public const float DefaultDotSize = 3f;

    /// <summary>点大小下限。</summary>
    public const float MinDotSize = 0f;

    /// <summary>点大小上限（像素）。</summary>
    public const float MaxDotSize = 200f;

    /// <summary>点间距默认值（像素）。</summary>
    public const float DefaultDotSeparation = 14f;

    /// <summary>点间距上限（像素）。</summary>
    public const float MaxDotSeparation = 500f;

    /// <summary>
    /// 点间距的**渲染安全下限**（像素）。
    /// 原版效果的绘制循环按点间距递增坐标，间距过小时单帧绘制量会暴涨
    /// （间距 0 更是死循环），所以解析与运行时调节都不允许低于此值。
    /// </summary>
    public const float MinDotSeparation = 2f;

    /// <summary>混沌度默认值（0 = 完全规整）。</summary>
    public const float DefaultChaos = 0.35f;

    /// <summary>混沌度下限。</summary>
    public const float MinChaos = 0f;

    /// <summary>混沌度上限。</summary>
    public const float MaxChaos = 1f;

    /// <summary>结束后退出延时的默认值（秒）。</summary>
    public const float DefaultCompleteDelay = 5f;

    /// <summary>结束后退出延时下限。</summary>
    public const float MinCompleteDelay = 0f;

    /// <summary>结束后退出延时上限（秒）。</summary>
    public const float MaxCompleteDelay = 600f;

    /// <summary>唯一已实现的动画形态：原版 <c>Hacknet.Effects.DepthDotGridEffect</c>。</summary>
    public const string StyleDepthDotGrid = "DepthDotGrid";

    // ======================= XML 原始值（string 承接）与解析结果 =======================

    /// <summary>终端与窗口标题里显示的程序名。留空 = <c>ProxyTuner</c>（注册名 #PROXYTUNER# 不受影响）。</summary>
    [XmlElement("ProgramName")] public string ProgramName { get; set; } = "ProxyTuner";

    /// <summary>代理过载倍数原始值。留空 = 1.0；1.0 = 原版速度，0 = 冻结（不推进过载）。</summary>
    [XmlElement("SpeedMultiplier")] public string SpeedMultiplierRaw { get; set; }

    /// <summary>本程序 RAM 占用原始值（MB）。留空 = <see cref="DefaultRamCost"/>（100）。</summary>
    [XmlElement("RamCost")] public string RamCostRaw { get; set; }

    /// <summary>
    /// 强调色（界面文字 / 深度点阵粒子的颜色）。留空 / <c>NONE</c> = <see cref="DefaultAccentColor"/>（白色）。
    /// 支持全部写法：CustomColor 预设名、动态色（<c>LDTchara</c> / <c>Rainbow</c> / <c>Gradient:…</c>）、
    /// <c>#RGB</c> / <c>#RRGGBB</c> / <c>#AARRGGBB</c>、<c>R,G,B[,A]</c>、XNA 命名色；
    /// 由 CustomColorManager 统一解析，动态色逐帧刷新。
    /// </summary>
    [XmlElement("AccentColor")] public string AccentColorRaw { get; set; }

    /// <summary>强调色默认值：白色（程序自带配色，不跟随主题高亮色）。</summary>
    [XmlIgnore] public static Color DefaultAccentColor => Color.White;

    /// <summary>动画形态原始值。留空 = <see cref="StyleDepthDotGrid"/>；未知值 → Warn + 回退默认。</summary>
    [XmlElement("AnimationStyle")] public string AnimationStyleRaw { get; set; }

    /// <summary>动画推进速率原始值。留空 = 1.0。</summary>
    [XmlElement("AnimationSpeed")] public string AnimationSpeedRaw { get; set; }

    /// <summary>递归步数原始值。留空 = 4。</summary>
    [XmlElement("Depth")] public string DepthRaw { get; set; }

    /// <summary>点大小原始值（像素）。留空 = 3。</summary>
    [XmlElement("DotSize")] public string DotSizeRaw { get; set; }

    /// <summary>点间距原始值（像素）。留空 = 14；低于 <see cref="MinDotSeparation"/> 会抬到该下限并 Warn。</summary>
    [XmlElement("DotSeparation")] public string DotSeparationRaw { get; set; }

    /// <summary>混沌度原始值（0~1）。留空 = 0.35。</summary>
    [XmlElement("Chaos")] public string ChaosRaw { get; set; }

    /// <summary>结束后退出延时原始值（秒）。留空 = 5。</summary>
    [XmlElement("CompleteDelay")] public string CompleteDelayRaw { get; set; }

    // ---- 以下为解析结果（由 ResolveValues 在反序列化后填入）----

    /// <summary>代理过载倍数（1.0 = 原版速度）。</summary>
    [XmlIgnore] public float SpeedMultiplier { get; private set; } = DefaultSpeedMultiplier;

    /// <summary>本程序 RAM 占用（MB）。</summary>
    [XmlIgnore] public int RamCost { get; private set; } = DefaultRamCost;

    /// <summary>动画形态（已知值之一）。</summary>
    [XmlIgnore] public string AnimationStyle { get; private set; } = StyleDepthDotGrid;

    /// <summary>动画推进速率（1.0 = 原始推进速度）。</summary>
    [XmlIgnore] public float AnimationSpeed { get; private set; } = DefaultAnimationSpeed;

    /// <summary>递归步数。</summary>
    [XmlIgnore] public int Depth { get; private set; } = DefaultDepth;

    /// <summary>点大小（像素）。</summary>
    [XmlIgnore] public float DotSize { get; private set; } = DefaultDotSize;

    /// <summary>点间距（像素），已抬到渲染安全下限之上。</summary>
    [XmlIgnore] public float DotSeparation { get; private set; } = DefaultDotSeparation;

    /// <summary>混沌度（0~1）。</summary>
    [XmlIgnore] public float Chaos { get; private set; } = DefaultChaos;

    /// <summary>结束后退出延时（秒）。</summary>
    [XmlIgnore] public float CompleteDelay { get; private set; } = DefaultCompleteDelay;

    /// <summary>本配置对应的 flag 名（不含前缀）；由加载器填入，供日志与终端提示定位。</summary>
    [XmlIgnore] public string SourceName { get; set; }

    /// <summary>反序列化用无参构造。</summary>
    public ProxyTunerConfig() { }

    // ======================= 加载（flag 入口） =======================

    /// <summary>
    /// 存档 flag 前缀。**flag 是本程序的入口牌**：
    /// 形如 <c>CustomProxyTuner_MyTuning</c> → 对应 <c>&lt;扩展根&gt;/ProxyTuner/MyTuning.xml</c>。
    /// 无 flag → 程序锁定；多 flag → 作者配置错误。
    /// </summary>
    public const string FlagPrefix = "CustomProxyTuner_";

    /// <summary>配置目录（扩展根下）。</summary>
    public const string ConfigDir = "ProxyTuner";

    /// <summary>
    /// 从存档 flag 反查并加载配置。
    /// 失败（前缀不符 / 越界 / 文件缺失 / 解析异常）返回 null，并在日志里记 Error；
    /// 数值项的畸形值不在此列 —— 那些只回退默认 + Warn，配置照常返回。
    /// </summary>
    public static ProxyTunerConfig LoadByFlag(string flag)
    {
        if (string.IsNullOrEmpty(flag)) return null;
        if (!flag.StartsWith(FlagPrefix, StringComparison.Ordinal))
        {
            KELog.Error($"[ProxyTuner] flag '{flag}' does not start with '{FlagPrefix}'.");
            return null;
        }

        string name = flag.Substring(FlagPrefix.Length);
        if (string.IsNullOrWhiteSpace(name))
        {
            KELog.Error($"[ProxyTuner] flag '{flag}' has an empty config name.");
            return null;
        }

        // 相对扩展根解析；越界由 KEPath 拦下（返回 null），不会读到扩展目录外
        string rel = $"{ConfigDir}/{name}.xml";
        string path = KEPath.ResolveInsideExtension(rel);
        if (path == null || !File.Exists(path))
        {
            KELog.Error($"[ProxyTuner] config not found for flag '{flag}': {rel}");
            return null;
        }

        try
        {
            var serializer = new XmlSerializer(typeof(ProxyTunerConfig));
            using var fs = new FileStream(path, FileMode.Open);
            var cfg = (ProxyTunerConfig)serializer.Deserialize(fs);
            if (cfg == null)
            {
                KELog.Error($"[ProxyTuner] config '{rel}' deserialized to nothing.");
                return null;
            }
            cfg.SourceName = name;
            cfg.ResolveValues();
            return cfg;
        }
        catch (Exception ex)
        {
            KELog.Error($"[ProxyTuner] failed to load {rel}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 反序列化后解析并校验数值 / 枚举型原始字段。
    /// 留空 = 默认（不算错误）；写了但无效 = 默认 + Warn。
    /// </summary>
    private void ResolveValues()
    {
        SpeedMultiplier = ConfigValue.ParseInRange(SpeedMultiplierRaw, DefaultSpeedMultiplier, MinSpeedMultiplier, MaxSpeedMultiplier, out bool smOk);
        if (!smOk)
            KELog.Warn($"[ProxyTuner] '{SourceName}': invalid <SpeedMultiplier> '{SpeedMultiplierRaw}', expected {MinSpeedMultiplier}~{MaxSpeedMultiplier}; using {DefaultSpeedMultiplier}.");

        RamCost = ToInt(ConfigValue.ParseInRange(RamCostRaw, DefaultRamCost, MinRamCost, MaxRamCost, out bool rcOk));
        if (!rcOk)
            KELog.Warn($"[ProxyTuner] '{SourceName}': invalid <RamCost> '{RamCostRaw}', expected {MinRamCost}~{MaxRamCost}; using {DefaultRamCost}.");

        AnimationSpeed = ConfigValue.ParseInRange(AnimationSpeedRaw, DefaultAnimationSpeed, MinAnimationSpeed, MaxAnimationSpeed, out bool asOk);
        if (!asOk)
            KELog.Warn($"[ProxyTuner] '{SourceName}': invalid <AnimationSpeed> '{AnimationSpeedRaw}', expected {MinAnimationSpeed}~{MaxAnimationSpeed}; using {DefaultAnimationSpeed}.");

        Depth = ToInt(ConfigValue.ParseInRange(DepthRaw, DefaultDepth, MinDepth, MaxDepth, out bool dOk));
        if (!dOk)
            KELog.Warn($"[ProxyTuner] '{SourceName}': invalid <Depth> '{DepthRaw}', expected {MinDepth}~{MaxDepth}; using {DefaultDepth}.");

        DotSize = ConfigValue.ParseInRange(DotSizeRaw, DefaultDotSize, MinDotSize, MaxDotSize, out bool dsOk);
        if (!dsOk)
            KELog.Warn($"[ProxyTuner] '{SourceName}': invalid <DotSize> '{DotSizeRaw}', expected {MinDotSize}~{MaxDotSize}; using {DefaultDotSize}.");

        DotSeparation = ConfigValue.ParseInRange(DotSeparationRaw, DefaultDotSeparation, 0f, MaxDotSeparation, out bool dpOk);
        if (!dpOk)
            KELog.Warn($"[ProxyTuner] '{SourceName}': invalid <DotSeparation> '{DotSeparationRaw}', expected 0~{MaxDotSeparation}; using {DefaultDotSeparation}.");
        if (DotSeparation < MinDotSeparation)
        {
            // 下限不是"配置错误"而是渲染安全线：间距过小时单帧绘制量暴涨（0 会死循环）
            KELog.Warn($"[ProxyTuner] '{SourceName}': <DotSeparation> {DotSeparation} is below the safe minimum; using {MinDotSeparation}.");
            DotSeparation = MinDotSeparation;
        }

        Chaos = ConfigValue.ParseInRange(ChaosRaw, DefaultChaos, MinChaos, MaxChaos, out bool cOk);
        if (!cOk)
            KELog.Warn($"[ProxyTuner] '{SourceName}': invalid <Chaos> '{ChaosRaw}', expected {MinChaos}~{MaxChaos}; using {DefaultChaos}.");

        CompleteDelay = ConfigValue.ParseInRange(CompleteDelayRaw, DefaultCompleteDelay, MinCompleteDelay, MaxCompleteDelay, out bool cdOk);
        if (!cdOk)
            KELog.Warn($"[ProxyTuner] '{SourceName}': invalid <CompleteDelay> '{CompleteDelayRaw}', expected {MinCompleteDelay}~{MaxCompleteDelay}; using {DefaultCompleteDelay}.");

        // 动画形态：目前只有 DepthDotGrid 一种；留空/NONE = 默认（不算错误），未知值 = 默认 + Warn
        string style = AnimationStyleRaw?.Trim();
        if (ConfigValue.IsNone(style) || style.Equals(StyleDepthDotGrid, StringComparison.OrdinalIgnoreCase))
            AnimationStyle = StyleDepthDotGrid;
        else
        {
            KELog.Warn($"[ProxyTuner] '{SourceName}': unknown <AnimationStyle> '{AnimationStyleRaw}', expected '{StyleDepthDotGrid}'; using '{StyleDepthDotGrid}'.");
            AnimationStyle = StyleDepthDotGrid;
        }

        if (string.IsNullOrWhiteSpace(ProgramName)) ProgramName = "ProxyTuner";
        else ProgramName = ProgramName.Trim();
    }

    /// <summary>把解析出的浮点值转成整数项（四舍五入；<c>Math.Round</c> 的银行家舍入在这里反直觉）。</summary>
    private static int ToInt(float v) => (int)Math.Floor(v + 0.5f);
}
