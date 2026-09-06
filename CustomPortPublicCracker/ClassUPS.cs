using HarmonyLib;
using Pathfinder.Port;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KernelExtensions.CustomPortPublicCracker;

public class UPSConfig
{
    // 属性而非字段，便于后续添加验证或变更通知
    public bool Useable { get; set; } = true;
    public string Protocol { get; set; } = string.Empty;
    public float Time { get; set; }      // 单位：秒（建议明确）
    public string Title { get; set; } = string.Empty;
    public int RamCost { get; set; }     // 单位：MB

    // 列表初始化为空列表，不要包含默认空字符串
    public Dictionary<string,List<string>> Debuffs { get; set; } = new();
    public Dictionary<string, List<string>> Buffs { get; set; } = new();
    public List<string> RequireOpenPortsBefore { get; set; } = new();

    public bool RequireSolveFirewall { get; set; }
    public bool RequireProxyOverload { get; set; }

    // 可选：构造函数，便于快速创建实例
    public UPSConfig() { }
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
    /// 返回：Dictionary<string, List<string>>，命令名 → 参数列表
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
            if (ls.TryGetValue("closeports", out List<string>? closePorts) && closePorts != null && closePorts.Count > 0)
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
            if (ls.TryGetValue("addfirewall", out List<string>? firewallParams) && firewallParams != null && firewallParams.Count > 0)
            {
                string s = "AddFirewall:" + string.Join(", ", firewallParams);
                res = SJoin(res, s);
            }

            // ----- addproxy -----
            if (ls.TryGetValue("addproxy", out List<string>? proxyParams) && proxyParams != null && proxyParams.Count > 0)
            {
                string s = "AddProxy:" + string.Join(", ", proxyParams);
                res = SJoin(res, s);
            }

            // ----- ProxyOverloadSpeedHelper -----
            if (ls.TryGetValue("ProxyOverloadSpeedHelper", out List<string>? speedParams) && speedParams != null && speedParams.Count > 0)
            {
                string s = "ProxyOverloadSpeedHelper:" + string.Join(", ", speedParams);
                res = SJoin(res, s);
            }

            // ----- ReRandomFirewall -----
            if (ls.TryGetValue("ReRandomFirewall", out List<string>? reRandomParams) && reRandomParams != null && reRandomParams.Count > 0)
            {
                string s = "ReRandomFirewall:" + string.Join(", ", reRandomParams);
                res = SJoin(res, s);
            }

            // ----- ResetFirewall -----
            if (ls.TryGetValue("ResetFirewall", out List<string>? resetFirewall) && resetFirewall != null && resetFirewall.Count > 0)
            {
                string s = "ResetFirewall:" + string.Join(", ", resetFirewall);
                res = SJoin(res, s);
            }

            // ----- ResetProxy -----
            if (ls.TryGetValue("ResetProxy", out List<string>? resetProxy) && resetProxy != null && resetProxy.Count > 0)
            {
                string s = "ResetProxy:" + string.Join(", ", resetProxy);
                res = SJoin(res, s);
            }

            // ----- SetFirewallConfig -----
            if (ls.TryGetValue("SetFirewallConfig", out List<string>? setFirewallParams) && setFirewallParams != null && setFirewallParams.Count > 0)
            {
                string s = "SetFirewallConfig:" + string.Join(", ", setFirewallParams);
                res = SJoin(res, s);
            }
        }
        else // buffs
        {
            // ----- openports -----
            if (ls.TryGetValue("openports", out List<string>? openPorts) && openPorts != null && openPorts.Count > 0)
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
            if (ls.TryGetValue("solvefirewall", out List<string>? solveFirewall) && solveFirewall != null && solveFirewall.Count > 0)
            {
                string s = "SolveFirewall:" + string.Join(", ", solveFirewall);
                res = SJoin(res, s);
            }

            // ----- solveproxy -----
            if (ls.TryGetValue("solveproxy", out List<string>? solveProxy) && solveProxy != null && solveProxy.Count > 0)
            {
                string s = "SolveProxy:" + string.Join(", ", solveProxy);
                res = SJoin(res, s);
            }

            // ----- ProxyOverloadSpeedHelper -----
            if (ls.TryGetValue("ProxyOverloadSpeedHelper", out List<string>? speedParams) && speedParams != null && speedParams.Count > 0)
            {
                string s = "ProxyOverloadSpeedHelper:" + string.Join(", ", speedParams);
                res = SJoin(res, s);
            }

            // ----- AutoOverloadProxy -----
            if (ls.TryGetValue("AutoOverloadProxy", out List<string>? autoProxyParams) && autoProxyParams != null && autoProxyParams.Count > 0)
            {
                string s = "AutoOverloadProxy:" + string.Join(", ", autoProxyParams);
                res = SJoin(res, s);
            }

            // ----- SetFirewallConfig -----
            if (ls.TryGetValue("SetFirewallConfig", out List<string>? setFirewallParams) && setFirewallParams != null && setFirewallParams.Count > 0)
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

