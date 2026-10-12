using HarmonyLib;
using KernelExtensions.Managers;
using KernelExtensions.Utilities;
using Microsoft.Xna.Framework;
using Pathfinder.Port;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace KernelExtensions.Configs;

/// <summary>
/// PortHackV2 可选的面板背景效果（都取自游戏本体现成的 Effects，无需自带资源）。
/// </summary>
public enum PortHackV2BackgroundEffect
{
    /// <summary>不画背景。</summary>
    None,
    /// <summary>从中心向外扩散旋转的点阵（PhaseSwift 用的就是它）。</summary>
    ZoomingDotGrid,
    /// <summary>六边形蜂窝网格（原版 KaguyaTrial 用的）。</summary>
    HexGrid,
    /// <summary>带十字标记的交叉网格。</summary>
    Grid,
    /// <summary>多层递归点阵（景深感）。</summary>
    DepthDotGrid,
}

/// <summary>
/// 单个待破解协议：协议名 + 成功率 + 两条分支各自时长。
///
/// 数值项与根配置同样用 string 承接原值（XmlSerializer 遇非数字会直接抛，详见根类注释）。
/// </summary>
public class PortHackV2ProtocolConfig
{
    /// <summary>协议名（必填，须已被注册，如 ssh / ftp）。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 本协议的破解成功概率，**百分比 0~100**。留空 = 100（必定成功）。
    /// 在**进入 Cracking 时**掷一次，决定走成功分支还是失败分支；演出上不提前暴露。
    /// </summary>
    [XmlElement("Rng")] public string RngRaw { get; set; }

    /// <summary>成功分支的破解时长（秒）。留空 = 10；&lt;= 0 = 瞬满。</summary>
    [XmlElement("TimeIfSuccess")] public string TimeIfSuccessRaw { get; set; }

    /// <summary>失败分支的破解时长（秒）。留空 = 3；&lt;= 0 = 瞬满。</summary>
    [XmlElement("TimeIfFail")] public string TimeIfFailRaw { get; set; }

    /// <summary>破解成功概率，归一化 0.0~1.0。判定用 <c>Random.NextDouble() &lt; SuccessChance</c>。</summary>
    [XmlIgnore] public float SuccessChance { get; private set; } = 1f;

    /// <summary>成功分支时长（秒）。</summary>
    [XmlIgnore] public float TimeIfSuccess { get; private set; } = 10f;

    /// <summary>失败分支时长（秒）。</summary>
    [XmlIgnore] public float TimeIfFail { get; private set; } = 3f;

    /// <summary>
    /// 本协议在 Cracking 开始后**等待多久才启动**（秒）。
    /// 多协议并行时用它错开各协议的进度条。留空 = 0（立即开始）。
    /// </summary>
    [XmlElement("StartDelay")] public string StartDelayRaw { get; set; }

    /// <summary>启动延迟的解析结果（秒）。</summary>
    [XmlIgnore] public float StartDelay { get; private set; }

    /// <summary>动作点：本协议开始破解时执行。支持内联动作与动作文件路径，见 <see cref="ActionHook"/>。</summary>
    public ActionHook OnStart { get; set; }

    /// <summary>动作点：本协议破解成功时执行。支持内联动作与动作文件路径，见 <see cref="ActionHook"/>。</summary>
    public ActionHook OnComplete { get; set; }

    /// <summary>动作点：本协议破解失败时执行。支持内联动作与动作文件路径，见 <see cref="ActionHook"/>。</summary>
    public ActionHook OnFail { get; set; }

    public PortHackV2ProtocolConfig() { }

    /// <summary>反序列化后解析并校验本协议的数值字段；<paramref name="sourceName"/> 仅用于日志定位。</summary>
    public void ResolveValues(string sourceName)
    {
        string tag = $"{sourceName}/{Name}";

        SuccessChance = ConfigValue.ParseInRange(RngRaw, 100f, 0f, 100f, out bool rngOk) / 100f;
        if (!rngOk)
            KELog.Warn($"[PortHackV2] '{tag}': invalid <Rng> '{RngRaw}', expected 0~100; using 100 (always succeeds).");

        TimeIfSuccess = ConfigValue.ParseInRange(TimeIfSuccessRaw, 10f, 0f, float.MaxValue, out bool tsOk);
        if (!tsOk)
            KELog.Warn($"[PortHackV2] '{tag}': invalid <TimeIfSuccess> '{TimeIfSuccessRaw}', expected 0~3600; using 10.");

        TimeIfFail = ConfigValue.ParseInRange(TimeIfFailRaw, 3f, 0f, float.MaxValue, out bool tfOk);
        if (!tfOk)
            KELog.Warn($"[PortHackV2] '{tag}': invalid <TimeIfFail> '{TimeIfFailRaw}', expected 0~3600; using 3.");

        StartDelay = ConfigValue.ParseInRange(StartDelayRaw, 0f, 0f, float.MaxValue, out bool sdOk);
        if (!sdOk)
            KELog.Warn($"[PortHackV2] '{tag}': invalid <StartDelay> '{StartDelayRaw}', expected 0~36000; using 0.");
    }
}

public class PortHackV2Config
{
    // 属性而非字段，便于后续添加验证或变更通知

    /// <summary>终端里显示的程序名（对齐 PhaseSwift 的 ProgramName）。注册名 #PORTHACKV2# 不受影响。</summary>
    [XmlElement("ProgramName")] public string ProgramName { get; set; } = "PortHackV2";

    /// <summary>
    /// 运行本程序占用的内存（MB）。
    /// 默认 <see cref="DefaultRamCost"/> = <b>246</b>，与原版 PortHack 一致
    /// （原版 <c>PortHackExe</c> 从不设置 ramCost，取的是 Hacknet <c>ExeModule.DEFAULT_RAM_COST</c>）。
    /// ⚠️ 这个默认值不能靠"属性初始值 0"来兜 —— 0 会让程序不占内存，等于白嫖。
    /// </summary>
    public int RamCost { get; set; } = DefaultRamCost;

    /// <summary>RAM 默认占用（MB）—— 对齐原版 PortHack 的 <c>ExeModule.DEFAULT_RAM_COST</c>。</summary>
    public const int DefaultRamCost = 246;

    /// <summary>
    /// 要破解的协议列表。每个 &lt;Protocol&gt; 自带协议名、成功率与两条分支时长。
    /// ⚠️ 多协议时的编排语义（并行 / 串行、根级 &lt;Time&gt; 如何参与）**待批 2 定**。
    /// </summary>
    [XmlArray("Protocols")]
    [XmlArrayItem("Protocol")]
    public List<PortHackV2ProtocolConfig> Protocols { get; set; } = new();

    /// <summary>协议名列表（剔除空项），供快速判断用；不参与序列化。</summary>
    [XmlIgnore]
    public List<string> ProtocolNames =>
        Protocols == null
            ? new List<string>()
            : Protocols.Where(p => p != null && !string.IsNullOrWhiteSpace(p.Name)).Select(p => p.Name).ToList();

    /// <summary>
    /// 根级时长（秒），**不参与 RNG 判定**。
    /// ⚠️ 具体语义（整体基准时长 / 未指定分支时的兜底）**待批 2 定**。
    /// </summary>
    [XmlElement("Time")] public string TimeRaw { get; set; }

    /// <summary>根级时长的解析结果（秒）。</summary>
    [XmlIgnore] public float Time { get; private set; } = 10f;

    // ---- 数值项统一用 string 承接原值 ----
    // 理由：XmlSerializer 遇到非数字（如 <TimeIfSuccess>10s</TimeIfSuccess>）会直接抛异常，
    // 导致整份配置加载失败 → 程序锁定。改动后统一走 ConfigValue.ParseInRange 宽松解析，
    // 畸形值（非数字 / NaN / Infinity / 负数 / 超范围）只回退默认 + Warn，配置其余部分照常生效。

    /// <summary>
    /// Cracking 阶段动画用的柏拉图立体。可写 <c>Tetrahedron</c> / <c>Cube</c> / <c>Octahedron</c> /
    /// <c>Dodecahedron</c> / <c>Icosahedron</c>（大小写不敏感）。留空或无法识别 = <c>Cube</c>。
    /// </summary>
    [XmlElement("Polyhedron")] public string PolyhedronRaw { get; set; }

    /// <summary>动画立体的解析结果。</summary>
    [XmlIgnore] public KPlatonicShape PolyhedronShape { get; private set; } = KPlatonicShape.Cube;

    /// <summary>
    /// 立体的**呈现方式**：<c>Wireframe</c>（默认）/ <c>Solid</c> / <c>SolidWire</c>（大小写不敏感）。
    ///
    /// <list type="bullet">
    ///   <item><c>Wireframe</c> —— 只画线框。**默认**，与原版 <c>PortHack</c> 观感一致，
    ///     也是加入面渲染之前的行为（老配置零变化）。</item>
    ///   <item><c>Solid</c> —— 只画实心面，带**逐面明暗**（平面着色）与背面剔除。</item>
    ///   <item><c>SolidWire</c> —— 实心面 + 线框叠加：低多边形观感，棱始终可见。</item>
    /// </list>
    ///
    /// <para>
    /// ⚠️ 面数少的立体（正四面体 4 / 立方体 6 / 正八面体 8）实心后体积感最好；
    /// 面数多的（正十二面体 12 / 正二十面体 20）实心后可能显得"糊成一团"，
    /// 那种情况下 <c>SolidWire</c> 通常比纯 <c>Solid</c> 好看。
    /// </para>
    /// </summary>
    [XmlElement("PolyhedronStyle")] public string PolyhedronStyleRaw { get; set; }

    /// <summary>立体呈现方式的解析结果。</summary>
    [XmlIgnore] public KPolyhedronStyle PolyhedronStyle { get; private set; } = KPolyhedronStyle.Wireframe;

    /// <summary>
    /// 强调色：**动画立体（多边体线框）**专用。
    ///
    /// 留空 / <c>NONE</c> = <see cref="DefaultAccentColor"/>（**白色**）。
    /// 支持全部写法 —— CustomColor 预设名、动态色（<c>LDTchara</c> / <c>Rainbow</c> / <c>Gradient:…</c>）、
    /// <c>#RGB</c> / <c>#RRGGBB</c> / <c>#AARRGGBB</c>、<c>R,G,B[,A]</c>、XNA 命名色；
    /// 由 CustomColorManager 统一解析，动态色逐帧刷新。
    /// 进度条另用 <see cref="ProgressColorRaw"/>；目标机状态的语义色（locked / unlocked）仍走主题色。
    /// </summary>
    [XmlElement("AccentColor")] public string AccentColorRaw { get; set; }

    /// <summary>
    /// 底部总进度条的颜色。留空 / <c>NONE</c> = **跟随强调色**（<see cref="AccentColor"/>）。
    /// 写法同 <see cref="AccentColorRaw"/>。
    /// </summary>
    [XmlElement("ProgressColor")] public string ProgressColorRaw { get; set; }

    /// <summary>强调色默认值：白色（程序自带配色，不跟随主题高亮色）。</summary>
    [XmlIgnore] public static Color DefaultAccentColor => Color.White;

    /// <summary>
    /// 解析后的强调色（静态部分；动态色由消费端每帧向 CustomColorManager 取）。
    /// 供 <c>OnInitialize</c> 之后不逐帧刷新的调用点使用。
    /// </summary>
    [XmlIgnore] public Color AccentColor { get; private set; } = Color.White;

    /// <summary>解析后的进度条颜色（静态部分）。</summary>
    [XmlIgnore] public Color ProgressColor { get; private set; } = Color.White;

    /// <summary>解析后的背景色（静态部分）。</summary>
    [XmlIgnore] public Color BackgroundColor { get; private set; } = Color.White;

    /// <summary>
    /// 背景效果名（<c>NONE</c> = 不画背景）。见 <see cref="ParseBackgroundEffect"/> 支持的取值。
    /// </summary>
    [XmlElement("BackgroundEffect")] public string BackgroundEffectRaw { get; set; }

    /// <summary>背景效果的解析结果。</summary>
    [XmlIgnore] public PortHackV2BackgroundEffect BackgroundEffect { get; private set; } = PortHackV2BackgroundEffect.ZoomingDotGrid;

    /// <summary>背景色。留空 / <c>NONE</c> = 跟随强调色。写法同 <see cref="AccentColorRaw"/>。</summary>
    [XmlElement("BackgroundColor")] public string BackgroundColorRaw { get; set; }

    /// <summary>
    /// 成功演出的中央文案。**留空 = <c>COMPLETE</c>**（所有语言都用它，不做本地化 ——
    /// 这个位置是"程序告诉你破解完了"的电子感文字，和原版的 PASSWORD/FOUND 同类）。
    /// 单行显示；过长会按面板宽度自动缩字号。
    /// </summary>
    [XmlElement("CompletionText")] public string CompletionText { get; set; } = DefaultCompletionText;

    /// <summary>成功演出文案的默认值。</summary>
    public const string DefaultCompletionText = "COMPLETE";

    /// <summary>
    /// 起转（加速）时长**占总时长的比例**。留空 = 0.1（总时长的 10%）。
    /// 与 <c>SpinUpTime</c> 同时写时以 <c>SpinUpTime</c> 为准。
    /// </summary>
    [XmlElement("SpinUpRatio")] public string SpinUpRatioRaw { get; set; }

    /// <summary>起转时长的**绝对秒数**。留空或负数 = 不用绝对值、改走 <c>SpinUpRatio</c>。</summary>
    [XmlElement("SpinUpTime")] public string SpinUpSecondsRaw { get; set; }

    /// <summary>急刹（减速）时长（秒）。留空 = 1.7；剩余时间不够时自动截断。</summary>
    [XmlElement("SpinDownTime")] public string SpinDownSecondsRaw { get; set; }

    /// <summary>成功演出结束后、退出前的停留秒数。留空 = 5（对齐原版 TIME_ALIVE_AFTER_SUCSESS）。</summary>
    [XmlElement("CompleteDelay")] public string CompleteDelayRaw { get; set; }

    // ---- 以下为解析结果（由 ResolveValues 在反序列化后填入）----

    /// <summary>起转时长占总时长的比例（解析结果）。</summary>
    [XmlIgnore] public float SpinUpRatio { get; private set; } = 0.1f;

    /// <summary>起转时长的绝对秒数（解析结果）；&lt; 0 = 用比例。</summary>
    [XmlIgnore] public float SpinUpSeconds { get; private set; } = -1f;

    /// <summary>急刹时长的解析结果（秒）。</summary>
    [XmlIgnore] public float SpinDownSeconds { get; private set; } = 1.7f;

    /// <summary>成功演出后退出前停留（秒）。</summary>
    [XmlIgnore] public float CompleteDelay { get; private set; } = 5f;

    /// <summary>
    /// 破解成功后是否把目标机 owner 设为玩家（原版 PortHack 的 takeAdmin 行为）。
    /// 省略 = false：默认只开端口，不擅自给管理员权限。
    /// </summary>
    public bool TakeAdmin { get; set; }

    /// <summary>
    /// 玩家离开本程序所在节点时是否自动关闭程序。
    /// 默认 true（原版 PortHack 行为）；设为 false 可让程序跨节点常驻。
    /// </summary>
    public bool AutoExitOnLeave { get; set; } = true;

    /// <summary>动作点：开始破解时执行。支持内联动作与动作文件路径，见 <see cref="ActionHook"/>。</summary>
    public ActionHook OnStart { get; set; }

    /// <summary>
    /// 动作点：整体收尾时执行。支持内联动作与动作文件路径，见 <see cref="ActionHook"/>。
    /// 根级**没有 RNG**，总时长跑满固定走成功演出，因此没有对应的失败动作点：
    /// 某个协议有没有破开由它自己的 <c>OnFail</c> 表达。
    /// </summary>
    public ActionHook OnComplete { get; set; }

    // ---- 一次性效果一律走动作文件 ----
    // 效果不再有专门的 XML 字段：要做的事写在动作点里（OnStart / OnComplete / OnFail），
    // 复用作者已经会写的动作文件，不必再学一套效果串语法；
    // 需要逐帧推进的代理辅助同样由 Action 承担（它们本来就依赖每帧推进）。
    // 程序自身只负责"成功协议各自把端口打开"这一件事。

    /// <summary>前置要求：这些端口必须先打开。List&lt;string&gt; 可被 XmlSerializer 直接处理。</summary>
    public List<string> RequireOpenPortsBefore { get; set; } = new();

    /// <summary>
    /// 可选前置：目标机上**已破解的端口数量**要求。
    ///
    /// <list type="bullet">
    ///   <item>不写 / 留空 / <c>0</c>（或任意负值）= **不检查**</item>
    ///   <item><c>auto</c> = 对齐**目标节点自己的要求**（照搬原版：需要
    ///     <c>max(portsNeededForCrack + 1, 0)</c> 个已破解端口，即原版 PortHack 的判定）</item>
    ///   <item>正整数 N = 固定要求 **至少 N 个**已破解端口</item>
    /// </list>
    ///
    /// ⚠️ 注意两条语义的差别：<c>auto</c> 沿用的是节点自身的 <c>portsNeededForCrack</c>
    /// （原版引擎按 security 算出来的值，界面上显示的也是它 +1），
    /// 手写数字则是"作者说至少几个就至少几个"，不做 +1。
    /// </summary>
    [XmlElement("RequireCrackedPorts")] public string RequireCrackedPortsRaw { get; set; }

    /// <summary><c>auto</c> 的写法（大小写不敏感）。</summary>
    public const string RequireCrackedPortsAuto = "auto";

    /// <summary>解析后的固定要求数量；&lt;= 0 = 不检查。</summary>
    [XmlIgnore] public int RequireCrackedPorts { get; private set; }

    /// <summary>是否用"对齐目标节点自身要求"的 auto 语义。</summary>
    [XmlIgnore] public bool RequireCrackedPortsAutoMode { get; private set; }

    public bool RequireSolveFirewall { get; set; }
    public bool RequireProxyOverload { get; set; }

    // 可选：构造函数，便于快速创建实例
    public PortHackV2Config() { }

    // ======================= 加载（flag 入口） =======================

    /// <summary>
    /// 存档 flag 前缀。**flag 是本程序的入口牌**：
    /// 形如 <c>PortHackV2_MyAttack</c> → 对应 <c>&lt;扩展根&gt;/PortHackV2/MyAttack.xml</c>。
    /// 无 flag → 程序锁定；多 flag → 作者配置错误。
    /// </summary>
    public const string FlagPrefix = "PortHackV2_";

    /// <summary>配置目录（扩展根下）。</summary>
    public const string ConfigDir = "PortHackV2";

    /// <summary>本配置对应的 flag 名（不含前缀）；由加载器填入，供反查与日志使用。</summary>
    [XmlIgnore] public string SourceName { get; set; }

    /// <summary>
    /// 从存档 flag 反查并加载配置。
    /// 失败（越界 / 文件缺失 / 解析异常 / 缺 &lt;Protocol&gt;）返回 null，并在日志里记 Error。
    /// </summary>
    public static PortHackV2Config LoadByFlag(string flag)
    {
        if (string.IsNullOrEmpty(flag)) return null;
        if (!flag.StartsWith(FlagPrefix, StringComparison.Ordinal))
        {
            KELog.Error($"[PortHackV2] flag '{flag}' does not start with '{FlagPrefix}'.");
            return null;
        }

        string name = flag.Substring(FlagPrefix.Length);
        if (string.IsNullOrWhiteSpace(name))
        {
            KELog.Error($"[PortHackV2] flag '{flag}' has an empty config name.");
            return null;
        }

        // 相对扩展根解析；越界由 KEPath 拦下（返回 null），不会读到扩展目录外
        string rel = $"{ConfigDir}/{name}.xml";
        string path = KEPath.ResolveInsideExtension(rel);
        if (path == null || !File.Exists(path))
        {
            KELog.Error($"[PortHackV2] config not found for flag '{flag}': {rel}");
            return null;
        }

        try
        {
            var cfg = XmlConfigLoader.Load<PortHackV2Config>(path, rel);
            if (cfg == null || cfg.ProtocolNames.Count == 0)
            {
                KELog.Error($"[PortHackV2] config '{rel}' needs at least one <Protocol> with a non-empty <Name>.");
                return null;
            }
            cfg.SourceName = name;
            cfg.ResolveValues();
            return cfg;
        }
        catch (Exception ex)
        {
            KELog.Error($"[PortHackV2] failed to load {rel}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 反序列化后解析并校验数值型原始字段，并驱动每个 <see cref="PortHackV2ProtocolConfig"/> 自己解析。
    /// 与 <see cref="LoadByFlag"/> 分开，便于将来从其它入口加载时复用。
    /// </summary>
    private void ResolveValues()
    {
        CompleteDelay = ConfigValue.ParseInRange(CompleteDelayRaw, 5f, 0f, float.MaxValue, out bool cdOk);
        if (!cdOk)
            KELog.Warn($"[PortHackV2] '{SourceName}': invalid <CompleteDelay> '{CompleteDelayRaw}', expected 0~600; using 5.");

        Time = ConfigValue.ParseInRange(TimeRaw, 10f, 0f, float.MaxValue, out bool tOk);
        if (!tOk)
            KELog.Warn($"[PortHackV2] '{SourceName}': invalid <Time> '{TimeRaw}', expected 0~36000; using 10.");

        PolyhedronShape = ParsePolyhedron(PolyhedronRaw);
        PolyhedronStyle = ParsePolyhedronStyle(PolyhedronStyleRaw);

        // 强调色：动态色（CC 预设/彩虹/渐变）也要能解析 —— 与其它颜色配置项走同一个入口。
        // 留空 / NONE 时 GetDynamicColor 直接返回默认色（白色），故不需要额外判空。
        AccentColor = CustomColorManager.GetDynamicColor(AccentColorRaw, DefaultAccentColor);

        // 进度条颜色：留空 = 跟随强调色（作者只想要一个颜色时不用写两遍）
        ProgressColor = string.IsNullOrWhiteSpace(ProgressColorRaw) || ConfigValue.IsNone(ProgressColorRaw)
            ? AccentColor
            : CustomColorManager.GetDynamicColor(ProgressColorRaw, AccentColor);

        // 背景色：留空 = 跟随强调色
        BackgroundColor = string.IsNullOrWhiteSpace(BackgroundColorRaw) || ConfigValue.IsNone(BackgroundColorRaw)
            ? AccentColor
            : CustomColorManager.GetDynamicColor(BackgroundColorRaw, AccentColor);

        BackgroundEffect = ParseBackgroundEffect(BackgroundEffectRaw);

        // 已破解端口数前置：空 / 非正数 = 不检查；auto（大小写不敏感）= 对齐目标节点自身要求
        string reqPorts = RequireCrackedPortsRaw?.Trim();
        if (string.IsNullOrEmpty(reqPorts)
            || reqPorts.Equals(RequireCrackedPortsAuto, StringComparison.OrdinalIgnoreCase))
        {
            RequireCrackedPortsAutoMode = reqPorts?.Equals(RequireCrackedPortsAuto, StringComparison.OrdinalIgnoreCase) == true;
            RequireCrackedPorts = 0;
        }
        else
        {
            // 上限只为挡住"转 int 溢出"（下面有 (int)n），不是平衡考虑
            float n = ConfigValue.ParseInRange(reqPorts, 0f, -1f, 1e18f, out bool rpOk);
            if (!rpOk)
                KELog.Warn($"[PortHackV2] '{SourceName}': invalid <RequireCrackedPorts> '{RequireCrackedPortsRaw}'; "
                    + "expected 'auto' or a non-negative number. Not checked.");
            RequireCrackedPortsAutoMode = false;
            RequireCrackedPorts = n > 0f ? (int)n : 0;
        }

        if (string.IsNullOrWhiteSpace(CompletionText)) CompletionText = DefaultCompletionText;

        SpinUpRatio = ConfigValue.ParseInRange(SpinUpRatioRaw, 0.1f, 0f, 1f, out bool surOk);
        if (!surOk)
            KELog.Warn($"[PortHackV2] '{SourceName}': invalid <SpinUpRatio> '{SpinUpRatioRaw}', expected 0~1; using 0.1.");

        // 负数 = 走比例（对齐本项目的「负数 = 用默认/继承」约定）
        SpinUpSeconds = ConfigValue.ParseInRange(SpinUpSecondsRaw, -1f, -1f, float.MaxValue, out bool susOk);
        if (!susOk)
            KELog.Warn($"[PortHackV2] '{SourceName}': invalid <SpinUpTime> '{SpinUpSecondsRaw}', expected -1~3600; using the ratio.");

        SpinDownSeconds = ConfigValue.ParseInRange(SpinDownSecondsRaw, 1.7f, 0f, float.MaxValue, out bool sdsOk);
        if (!sdsOk)
            KELog.Warn($"[PortHackV2] '{SourceName}': invalid <SpinDownTime> '{SpinDownSecondsRaw}', expected 0~600; using 1.7.");

        foreach (var p in Protocols) p?.ResolveValues(SourceName);
    }

    /// <summary>
    /// 解析背景效果名（大小写不敏感）；留空 = 默认 <see cref="PortHackV2BackgroundEffect.ZoomingDotGrid"/>
    /// （不记日志，那是正常省略）；写了但无法识别 = 回退默认 + Warn。
    /// </summary>
    private PortHackV2BackgroundEffect ParseBackgroundEffect(string raw)
    {
        if (ConfigValue.IsNone(raw)) return PortHackV2BackgroundEffect.ZoomingDotGrid;

        if (Enum.TryParse(raw.Trim(), ignoreCase: true, out PortHackV2BackgroundEffect effect))
            return effect;

        KELog.Warn($"[PortHackV2] '{SourceName}': unknown <BackgroundEffect> '{raw}'; "
            + "expected NONE / ZoomingDotGrid / HexGrid / Grid / DepthDotGrid. Using ZoomingDotGrid.");
        return PortHackV2BackgroundEffect.ZoomingDotGrid;
    }

    /// <summary>
    /// 解析动画立体名（大小写不敏感）；留空 = 默认立方（不记日志，那是正常省略）；
    /// 写了但无法识别 = 回退立方 + Warn（帮作者揪出拼写错误）。
    /// </summary>
    private KPlatonicShape ParsePolyhedron(string raw)
    {
        if (ConfigValue.IsNone(raw)) return KPlatonicShape.Cube;

        if (Enum.TryParse(raw.Trim(), ignoreCase: true, out KPlatonicShape shape))
            return shape;

        KELog.Warn($"[PortHackV2] '{SourceName}': unknown <Polyhedron> '{raw}'; "
            + "expected Tetrahedron / Cube / Octahedron / Dodecahedron / Icosahedron. Using Cube.");
        return KPlatonicShape.Cube;
    }

    /// <summary>
    /// 解析立体呈现方式（大小写不敏感）；留空 = 默认线框（不记日志，那是正常省略）；
    /// 写了但无法识别 = 回退线框 + Warn（帮作者揪出拼写错误）。
    ///
    /// <para>
    /// 默认刻意是 <c>Wireframe</c>：面渲染是**新增能力**，默认值必须等于加入之前的行为，
    /// 否则已有扩展的观感会无声改变。
    /// </para>
    /// </summary>
    private KPolyhedronStyle ParsePolyhedronStyle(string raw)
    {
        if (ConfigValue.IsNone(raw)) return KPolyhedronStyle.Wireframe;

        // 顺带接受几个同义写法，省得作者去猜该用哪个词
        string v = raw.Trim();
        if (v.Equals("wire", StringComparison.OrdinalIgnoreCase)
            || v.Equals("wireframe", StringComparison.OrdinalIgnoreCase)
            || v.Equals("line", StringComparison.OrdinalIgnoreCase))
            return KPolyhedronStyle.Wireframe;
        if (v.Equals("solid", StringComparison.OrdinalIgnoreCase)
            || v.Equals("filled", StringComparison.OrdinalIgnoreCase)
            || v.Equals("face", StringComparison.OrdinalIgnoreCase)
            || v.Equals("faces", StringComparison.OrdinalIgnoreCase))
            return KPolyhedronStyle.Solid;
        if (v.Equals("solidwire", StringComparison.OrdinalIgnoreCase)
            || v.Equals("solid+wire", StringComparison.OrdinalIgnoreCase)
            || v.Equals("both", StringComparison.OrdinalIgnoreCase))
            return KPolyhedronStyle.SolidWire;

        KELog.Warn($"[PortHackV2] '{SourceName}': unknown <PolyhedronStyle> '{raw}'; "
            + "expected Wireframe / Solid / SolidWire. Using Wireframe.");
        return KPolyhedronStyle.Wireframe;
    }
}

