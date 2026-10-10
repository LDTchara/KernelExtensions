using HarmonyLib;
using KernelExtensions.Utilities;
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

    /// <summary>动作点：本协议开始破解时执行（相对扩展根的动作文件路径）。</summary>
    public string OnStart { get; set; } = string.Empty;

    /// <summary>动作点：本协议破解成功时执行。</summary>
    public string OnComplete { get; set; } = string.Empty;

    /// <summary>动作点：本协议破解失败时执行。</summary>
    public string OnFail { get; set; } = string.Empty;

    public PortHackV2ProtocolConfig() { }

    /// <summary>反序列化后解析并校验本协议的数值字段；<paramref name="sourceName"/> 仅用于日志定位。</summary>
    public void ResolveValues(string sourceName)
    {
        string tag = $"{sourceName}/{Name}";

        SuccessChance = ConfigValue.ParseInRange(RngRaw, 100f, 0f, 100f, out bool rngOk) / 100f;
        if (!rngOk)
            KELog.Warn($"[PortHackV2] '{tag}': invalid <Rng> '{RngRaw}', expected 0~100; using 100 (always succeeds).");

        TimeIfSuccess = ConfigValue.ParseInRange(TimeIfSuccessRaw, 10f, 0f, 3600f, out bool tsOk);
        if (!tsOk)
            KELog.Warn($"[PortHackV2] '{tag}': invalid <TimeIfSuccess> '{TimeIfSuccessRaw}', expected 0~3600; using 10.");

        TimeIfFail = ConfigValue.ParseInRange(TimeIfFailRaw, 3f, 0f, 3600f, out bool tfOk);
        if (!tfOk)
            KELog.Warn($"[PortHackV2] '{tag}': invalid <TimeIfFail> '{TimeIfFailRaw}', expected 0~3600; using 3.");

        StartDelay = ConfigValue.ParseInRange(StartDelayRaw, 0f, 0f, 36000f, out bool sdOk);
        if (!sdOk)
            KELog.Warn($"[PortHackV2] '{tag}': invalid <StartDelay> '{StartDelayRaw}', expected 0~36000; using 0.");
    }
}

public class PortHackV2Config
{
    // 属性而非字段，便于后续添加验证或变更通知

    /// <summary>终端里显示的程序名（对齐 PhaseSwift 的 ProgramName）。注册名 #KE_PORTHACKV2# 不受影响。</summary>
    [XmlElement("ProgramName")] public string ProgramName { get; set; } = "PortHackV2";

    public bool Usable { get; set; } = true;
    public string Title { get; set; } = string.Empty;
    public int RamCost { get; set; }     // 单位：MB

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

    /// <summary>成功演出结束后、退出前的停留秒数。留空 = 5（对齐原版 TIME_ALIVE_AFTER_SUCSESS）。</summary>
    [XmlElement("CompleteDelay")] public string CompleteDelayRaw { get; set; }

    // ---- 以下为解析结果（由 ResolveValues 在反序列化后填入）----

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

    /// <summary>动作点：开始破解时执行（相对扩展根的动作文件路径）。</summary>
    public string OnStart { get; set; } = string.Empty;

    /// <summary>动作点：破解成功、演出结束后执行。</summary>
    public string OnComplete { get; set; } = string.Empty;

    /// <summary>动作点：破解失败后执行。</summary>
    public string OnFail { get; set; } = string.Empty;

    // ---- 效果串用 AC 原始字符串语法 + XmlIgnore 字典 ----
    // XmlSerializer **不支持 IDictionary**（会抛 "Cannot serialize member ... because it
    // implements IDictionary"），直接把 Dictionary 当属性会让整份配置无法加载。
    // 所以 XML 里收字符串，反序列化后再用 ResolveAnyBuffsList 展开成字典。

    /// <summary>Debuff 效果串（AC 语法，逗号分隔多个效果，如 <c>closeports_ftp,addfirewall_lenth:12</c>）。</summary>
    [XmlElement("Debuffs")] public string DebuffsRaw { get; set; } = string.Empty;

    /// <summary>Buff 效果串（同语法，如 <c>openports_ssh</c>）。</summary>
    [XmlElement("Buffs")] public string BuffsRaw { get; set; } = string.Empty;

    /// <summary>展开后的 debuff 字典（由 <see cref="DebuffsRaw"/> 解析）。</summary>
    [XmlIgnore] public Dictionary<string, List<string>> Debuffs { get; private set; } = new();

    /// <summary>展开后的 buff 字典。</summary>
    [XmlIgnore] public Dictionary<string, List<string>> Buffs { get; private set; } = new();

    /// <summary>前置要求：这些端口必须先打开。List&lt;string&gt; 可被 XmlSerializer 直接处理。</summary>
    public List<string> RequireOpenPortsBefore { get; set; } = new();

    public bool RequireSolveFirewall { get; set; }
    public bool RequireProxyOverload { get; set; }

    // 可选：构造函数，便于快速创建实例
    public PortHackV2Config() { }

    // ======================= 加载（flag 入口） =======================

    /// <summary>
    /// 存档 flag 前缀。**flag 是本程序的入口牌**：
    /// 形如 <c>CustomPortHackV2_MyAttack</c> → 对应 <c>&lt;扩展根&gt;/PortHackV2/MyAttack.xml</c>。
    /// 无 flag → 程序锁定；多 flag → 作者配置错误。
    /// </summary>
    public const string FlagPrefix = "CustomPortHackV2_";

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
            var serializer = new XmlSerializer(typeof(PortHackV2Config));
            using var fs = new FileStream(path, FileMode.Open);
            var cfg = (PortHackV2Config)serializer.Deserialize(fs);
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
        CompleteDelay = ConfigValue.ParseInRange(CompleteDelayRaw, 5f, 0f, 600f, out bool cdOk);
        if (!cdOk)
            KELog.Warn($"[PortHackV2] '{SourceName}': invalid <CompleteDelay> '{CompleteDelayRaw}', expected 0~600; using 5.");

        Time = ConfigValue.ParseInRange(TimeRaw, 10f, 0f, 36000f, out bool tOk);
        if (!tOk)
            KELog.Warn($"[PortHackV2] '{SourceName}': invalid <Time> '{TimeRaw}', expected 0~36000; using 10.");

        PolyhedronShape = ParsePolyhedron(PolyhedronRaw);

        foreach (var p in Protocols) p?.ResolveValues(SourceName);

        // 效果串展开成字典（空串 → 空字典，避免把无配置误认成"有键但值为空"）
        Debuffs = string.IsNullOrWhiteSpace(DebuffsRaw) ? new() : ResolveAnyBuffsList(DebuffsRaw, false);
        Buffs = string.IsNullOrWhiteSpace(BuffsRaw) ? new() : ResolveAnyBuffsList(BuffsRaw, true);
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
    /*
    [
    ================debuffs================
    ["closeports",["n1","n2"...]],
    ["addfirewall",["lenth=12","additiontime=0.1","solution=SACOSHCA/@random"]],
    ["addproxy",["10"]],
    ["ProxyOverloadSpeedHelper",["0.7x"]],
    ["ReRandomFirewall",["lenth=12","additiontime=0.3","reset=true"]],
    ["ResetFirewall",["true"]],
    ["ResetProxy",["true"]],
    ["SetFirewallConfig",["lenth=20","additiontime=0.2","solution=ADJI/@random","reset=true"]],
    ================buffs==================
    ["openports",["n1","n2"...]],
    ["solvefirewall",["12"]],(12 is delay and can be 0 or lowwer than 0)
    ["solveproxy",["11"],
    ["ProxyOverloadSpeedHelper",["1.2x"]],(if this also defed in debuffs , use buff;)
    ["AutoOverloadProxy",["2"]],(proxyoverloadtick += dt * 2)
    ["SetFirewallConfig",["lenth=20","additiontime=0.2","solution=ADJI/@random","reset=true"]](same as ProxyOverloadSpeedHelper)
    ]
     */
    /// <summary>
    /// 解析命令列表（支持 debuffs 或 buffs 格式）
    /// 输入：string,bool 对于true则是buff否则是debuff
    /// 返回：Dictionary&lt;string, List&lt;string&gt;&gt;，命令名 → 参数列表
    /// </summary>
    public static Dictionary<string, List<string>> ResolveAnyBuffsList(string s,bool isbuff)
    {
        Dictionary<string, List<string>> result = new();
        if (s == null) return result;
        List<string> ls = s.Split(',').ToList();
        if (!isbuff)
        {
            List<string> tmp = new();
            tmp.Add("");
            result.Add("closeports", tmp);
            result.Add("addfirewall", tmp);
            result.Add("addproxy", tmp);
            result.Add("ProxyOverloadSpeedHelper", tmp);
            result.Add("ReRandomFirewall", tmp);
            result.Add("ResetFirewall", tmp);
            result.Add("ResetProxy", tmp);
            result.Add("SetFirewallConfig", tmp);

            for (int i  = 0; i < ls.Count; i++)
            {
                string debuff = ls[i];
                if (debuff.Contains("closeports"))
                {
                    List<string> listprotocol = new List<string>(); // 创建空列表
                    listprotocol = debuff.Replace("closeports_","").Split(';').ToList(); // 覆盖了上面的对象
                    result["closeports"] = listprotocol;
                }
                if (debuff.Contains("addfirewall"))
                {
                    List<string> l = new();
                    l = debuff.Replace("addfirewall_","").Split('_').ToList();
                    List<string> formattedParams = l.Select(p => p.Replace(':', '=')).ToList();
                    result["addfirewall"] = formattedParams;
                }
                if (debuff.Contains("addproxy"))
                {
                    List<string> l =new();
                    l.Add(debuff.Replace("addproxy_",""));
                    result["addproxy"] = l;
                }
                if (debuff.Contains("SolveProxySpeed"))
                {
                    List<string> l = new();
                    l.Add(debuff.Replace("SolveProxySpeed_", ""));
                    result["ProxyOverloadSpeedHelper"] = l;
                }
                if (debuff.Contains("ReRandomFirewall"))
                {
                    List<string> l = new();
                    l = debuff.Replace("ReRandomFirewall_", "").Split('_').ToList();
                    List<string> formattedParams = l.Select(p => p.Replace(':', '=')).ToList();
                    if (!formattedParams.Any( p => p.Contains("reset")))
                    {
                        formattedParams.Add("reset=true");
                    }
                    result["ReRandomFirewall"] = formattedParams;

                    
                }
                if (debuff.Contains("ResetFirewall"))
                {
                    List<string> l = new();
                    l.Add("true");
                    result["ResetFirewall"] = l;
                }
                if (debuff.Contains("ResetProxy"))
                {
                    List<string> l = new();
                    l.Add("true");
                    result["ResetProxy"] = l;
                }
                if (debuff.Contains("SetFirewallConfig"))
                {
                    List<string> l = new();
                    l = debuff.Replace("SetFirewallConfig_", "").Split('_').ToList();
                    List<string> formattedParams = l.Select(p => p.Replace(':', '=')).ToList();
                    if (!formattedParams.Any(p => p.Contains("reset")))
                    {
                        formattedParams.Add("reset=true");
                    }
                    if (!formattedParams.Any(p => p.Contains("solution")))
                    {
                        formattedParams.Add("solution=@Org");
                    }
                    result["SetFirewallConfig"] = formattedParams;

                }
                

            }
            return result;
        }

        if (isbuff)
        {
            List<string> tmp = new();
            tmp.Add("");
            result.Add("openports", tmp);
            result.Add("solvefirewall", tmp);
            result.Add("solveproxy", tmp);
            result.Add("ProxyOverloadSpeedHelper", tmp);
            result.Add("AutoOverloadProxy", tmp);
            result.Add("SetFirewallConfig", tmp);
            for (int i = 0; i < ls.Count; i++)
            {
                string buff = ls[i];
                if (buff.Contains("openports"))
                {
                    List<string> listprotocol = new List<string>(); // 创建空列表
                    listprotocol = buff.Replace("openports_", "").Split(';').ToList(); // 覆盖了上面的对象
                    result["openports"] = listprotocol;
                }
                if (buff.Contains("solvefirewall"))
                {
                    List<string> l = new();
                    //int x = 0;
                    string x = buff.Replace("solvefirewall_delay:", "");
                    if (string.IsNullOrEmpty(x) || Int32.Parse(x) <= 0)
                    {
                        l.Add("0");
                        result["solvefirewall"] = l;
                    }
                    
                    else 
                    {
                        l.Add(x);
                        result["solvefirewall"] = l;
                    }


                }
                if (buff.Contains("solveproxy"))
                {
                    List<string> l = new();
                    string x = buff.Replace("solveproxy_delay:", "");
                    if (string.IsNullOrEmpty(x) || Int32.Parse(x) <= 0)
                    {
                        l.Add("0");
                        result["solveproxy"] = l;
                    }
                    else
                    {
                        l.Add(x);
                        result["solveproxy"] = l;
                    } 
                }
                if (buff.Contains("SolveProxySpeed"))
                {
                    List<string> l = new();
                    l.Add(buff.Replace("SolveProxySpeed_", ""));
                    result["ProxyOverloadSpeedHelper"] = l;
                }
                if (buff.Contains("AutoOverloadProxy"))
                {
                    List<string> l = new();
                    l.Add(buff.Replace("AutoOverloadProxy_", ""));
                    result["AutoOverloadProxy"] = l; 

                }
                if (buff.Contains("SetFirewallConfig"))
                {
                    List<string> l = new();
                    l = buff.Replace("SetFirewallConfig_", "").Split('_').ToList();
                    List<string> formattedParams = l.Select(p => p.Replace(':', '=')).ToList();
                    if (!formattedParams.Any(p => p.Contains("reset")))
                    {
                        formattedParams.Add("reset=true");
                    }
                    if (!formattedParams.Any(p => p.Contains("solution")))
                    {
                        formattedParams.Add("solution=@Org");
                    }
                    result["SetFirewallConfig"] = formattedParams;

                }
            }
            return result;

            /*
            foreach (var sub in commandList)
            {
                if (sub == null || sub.Count < 2) continue;
                string cmd = sub[0]?.ToString() ?? "";
                if (string.IsNullOrEmpty(cmd)) continue;

                // 提取参数（从索引 1 到末尾）
                var args = sub.Skip(1).ToList();

                // 若命令已存在，可以覆盖（根据你的需求，buffs 覆盖 debuffs 时用这个）
                // 也可以选择合并（但一般不合并，因为参数结构可能不同）
                result[cmd] = args;
            }
            */
        }

        return result;
        
    }

    public static List<PortRecord> ProtocolToPortRecords(List<string> ls)
    {
        List<PortRecord> lpr = new();
        for(int i = 0; i <ls.Count; i++)
        {
            if (!PortManager.IsPortRegistered(ls[i])) continue;
            lpr.Add(PortManager.GetPortRecordFromProtocol(ls[i]));
        }
        return lpr;
    }
    public static List<string> ProtocolToPortDefcultName(List<string> lp)
    {
        List<string> ls = new();
        for(int i = 0;i < lp.Count; i++)
        {
            if (!PortManager.IsPortRegistered(lp[i])) continue;
            ls.Add(PortManager.GetPortRecordFromProtocol(lp[i]).DefaultDisplayName);

        }
        return ls;
    }
    public static string SJoin(string current, string newPart, string separator = ", ")
    {
        if (string.IsNullOrEmpty(current)) return newPart ?? "";
        if (string.IsNullOrEmpty(newPart)) return current;
        return current + separator + newPart;
    }
    public static string OutputAnybuffConfings(Dictionary<string, List<string>> ls, bool isbuff)
    {
        if (ls == null) return string.Empty;

        string res = "";

        if (!isbuff) // debuffs
        {
            // ----- closeports -----
            if (ls.TryGetValue("closeports", out List<string> closePorts) && closePorts != null && closePorts.Count > 0)
            {
                string s = "ClosePorts:";
                foreach (string protocol in closePorts)
                {
                    var record = PortManager.GetPortRecordFromProtocol(protocol);
                    string displayName = record?.DefaultDisplayName ?? protocol; // 安全处理 null
                    s = SJoin(s, displayName);
                }
                res = SJoin(res, s);
            }

            // ----- addfirewall -----
            if (ls.TryGetValue("addfirewall", out List<string> firewallParams) && firewallParams != null && firewallParams.Count > 0)
            {
                string s = "AddFirewall:" + string.Join(", ", firewallParams);
                res = SJoin(res, s);
            }

            // ----- addproxy -----
            if (ls.TryGetValue("addproxy", out List<string> proxyParams) && proxyParams != null && proxyParams.Count > 0)
            {
                string s = "AddProxy:" + string.Join(", ", proxyParams);
                res = SJoin(res, s);
            }

            // ----- ProxyOverloadSpeedHelper -----
            if (ls.TryGetValue("ProxyOverloadSpeedHelper", out List<string> speedParams) && speedParams != null && speedParams.Count > 0)
            {
                string s = "ProxyOverloadSpeedHelper:" + string.Join(", ", speedParams);
                res = SJoin(res, s);
            }

            // ----- ReRandomFirewall -----
            if (ls.TryGetValue("ReRandomFirewall", out List<string> reRandomParams) && reRandomParams != null && reRandomParams.Count > 0)
            {
                string s = "ReRandomFirewall:" + string.Join(", ", reRandomParams);
                res = SJoin(res, s);
            }

            // ----- ResetFirewall -----
            if (ls.TryGetValue("ResetFirewall", out List<string> resetFirewall) && resetFirewall != null && resetFirewall.Count > 0)
            {
                string s = "ResetFirewall:" + string.Join(", ", resetFirewall);
                res = SJoin(res, s);
            }

            // ----- ResetProxy -----
            if (ls.TryGetValue("ResetProxy", out List<string> resetProxy) && resetProxy != null && resetProxy.Count > 0)
            {
                string s = "ResetProxy:" + string.Join(", ", resetProxy);
                res = SJoin(res, s);
            }

            // ----- SetFirewallConfig -----
            if (ls.TryGetValue("SetFirewallConfig", out List<string> setFirewallParams) && setFirewallParams != null && setFirewallParams.Count > 0)
            {
                string s = "SetFirewallConfig:" + string.Join(", ", setFirewallParams);
                res = SJoin(res, s);
            }
        }
        else // buffs
        {
            // ----- openports -----
            if (ls.TryGetValue("openports", out List<string> openPorts) && openPorts != null && openPorts.Count > 0)
            {
                string s = "OpenPorts:";
                foreach (string protocol in openPorts)
                {
                    var record = PortManager.GetPortRecordFromProtocol(protocol);
                    string displayName = record?.DefaultDisplayName ?? protocol;
                    s = SJoin(s, displayName);
                }
                res = SJoin(res, s);
            }

            // ----- solvefirewall -----
            if (ls.TryGetValue("solvefirewall", out List<string> solveFirewall) && solveFirewall != null && solveFirewall.Count > 0)
            {
                string s = "SolveFirewall:" + string.Join(", ", solveFirewall);
                res = SJoin(res, s);
            }

            // ----- solveproxy -----
            if (ls.TryGetValue("solveproxy", out List<string> solveProxy) && solveProxy != null && solveProxy.Count > 0)
            {
                string s = "SolveProxy:" + string.Join(", ", solveProxy);
                res = SJoin(res, s);
            }

            // ----- ProxyOverloadSpeedHelper -----
            if (ls.TryGetValue("ProxyOverloadSpeedHelper", out List<string> speedParams) && speedParams != null && speedParams.Count > 0)
            {
                string s = "ProxyOverloadSpeedHelper:" + string.Join(", ", speedParams);
                res = SJoin(res, s);
            }

            // ----- AutoOverloadProxy -----
            if (ls.TryGetValue("AutoOverloadProxy", out List<string> autoProxyParams) && autoProxyParams != null && autoProxyParams.Count > 0)
            {
                string s = "AutoOverloadProxy:" + string.Join(", ", autoProxyParams);
                res = SJoin(res, s);
            }

            // ----- SetFirewallConfig -----
            if (ls.TryGetValue("SetFirewallConfig", out List<string> setFirewallParams) && setFirewallParams != null && setFirewallParams.Count > 0)
            {
                string s = "SetFirewallConfig:" + string.Join(", ", setFirewallParams);
                res = SJoin(res, s);
            }
        }
        res = res.Replace("@random", "RANDOM")
             .Replace("@Org", "ORG");
        return res;
    }

    /// <summary>
    /// 将 debuffs/buffs 配置按“每个效果一行”展开，供 Confings GUI 逐行展示（
    /// 避免多个效果挤在一行被缩小字号）。内容与顺序对齐 OutputAnybuffConfings；
    /// 没有任何效果时返回空列表（由调用方显示 None）。
    /// </summary>
    public static List<string> OutputAnybuffConfingsLines(Dictionary<string, List<string>> ls, bool isbuff)
    {
        List<string> lines = new();
        if (ls == null) return lines;

        void Push(string key, string label, bool protocolsAreNames)
        {
            if (!ls.TryGetValue(key, out List<string> args) || args == null) return;
            if (args.Count == 0 || string.IsNullOrEmpty(args[0])) return;

            if (protocolsAreNames)
            {
                // 值是协议名：转成默认显示名（未注册协议回退原文，便于排查）
                List<string> names = new();
                foreach (string p in args)
                {
                    var rec = PortManager.GetPortRecordFromProtocol(p);
                    names.Add(rec?.DefaultDisplayName ?? p);
                }
                lines.Add($"{label}: {string.Join(", ", names)}");
            }
            else
            {
                lines.Add($"{label}: {string.Join(", ", args)}");
            }
        }

        if (!isbuff)
        {
            Push("closeports", "ClosePorts", true);
            Push("addfirewall", "AddFirewall", false);
            Push("addproxy", "AddProxy", false);
            Push("ProxyOverloadSpeedHelper", "ProxyOverloadSpeedHelper", false);
            Push("ReRandomFirewall", "ReRandomFirewall", false);
            Push("ResetFirewall", "ResetFirewall", false);
            Push("ResetProxy", "ResetProxy", false);
            Push("SetFirewallConfig", "SetFirewallConfig", false);
        }
        else
        {
            Push("openports", "OpenPorts", true);
            Push("solvefirewall", "SolveFirewall", false);
            Push("solveproxy", "SolveProxy", false);
            Push("ProxyOverloadSpeedHelper", "ProxyOverloadSpeedHelper", false);
            Push("AutoOverloadProxy", "AutoOverloadProxy", false);
            Push("SetFirewallConfig", "SetFirewallConfig", false);
        }

        for (int i = 0; i < lines.Count; i++)
        {
            lines[i] = lines[i].Replace("@random", "RANDOM").Replace("@Org", "ORG");
        }
        return lines;
    }
}

