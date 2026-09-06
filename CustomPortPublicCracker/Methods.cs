using Hacknet;
using Pathfinder.Port;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.Remoting.Metadata.W3cXsd2001;
using System.Text;
using System.Threading.Tasks;

namespace KernelExtensions.CustomPortPublicCracker;
public class Methods
{
    public static void ClosePortsFromProtocol(List<string> Org_ptcls,OS os,string CompID)
    {
        HashSet<string> H_ptcls = new HashSet<string>();
        Computer c = Programs.getComputer(os,CompID) ?? null;
        if(c == null)
        {
            return;
        }
        for(int i = 0; i < Org_ptcls.Count; i++)
        {
            H_ptcls.Add(Org_ptcls[i]);
        }
        List<string> ptcls = H_ptcls.ToList();
        


        foreach(string p in ptcls)
        {
            // Pathfinder 下端口以 PortState 表为准（Computer.ports 不再承载端口）
            if (c.GetPortState(p) != null)
            {
                c.closePort(p, os.thisComputer.ip);
            }
            else
            {
                Console.WriteLine($"{p} is not here...");
            }
        }
    }
    public static string GetValue(List<string> args, string key)
    {
        if (args == null || string.IsNullOrEmpty(key)) return string.Empty;

        foreach (string arg in args)
        {
            if (string.IsNullOrWhiteSpace(arg)) continue;
            int eqIndex = arg.IndexOf('=');
            if (eqIndex == -1) continue; // 没有等号，跳过

            // 注意：这里不 Trim，保留原始键，因为你要区分大小写且不改变键
            string currentKey = arg.Substring(0, eqIndex);
            if (currentKey == key) // 区分大小写的比较
            {
                string value = arg.Substring(eqIndex + 1);
                return value; // 不 Trim，保留原始值
            }
        }

        return string.Empty;
    }
    public static T GetValue<T>(List<string> args, string key) where T : IConvertible
    {
        string raw = GetValue(args, key);

        // 使用 InvariantCulture 确保小数点解析正确
        return (T)Convert.ChangeType(raw, typeof(T), CultureInfo.InvariantCulture);
    }
    public static void OpenPortsFromProtocol(List<string> Org_ptcls, OS os, string CompID)
    {
        HashSet<string> H_ptcls = new HashSet<string>();
        Computer c = Programs.getComputer(os, CompID) ?? null;
        if (c == null)
        {
            return;
        }
        for (int i = 0; i < Org_ptcls.Count; i++)
        {
            H_ptcls.Add(Org_ptcls[i]);
        }
        List<string> ptcls = H_ptcls.ToList();



        foreach (string p in ptcls)
        {
            // Pathfinder 下端口以 PortState 表为准（Computer.ports 不再承载端口）
            if (c.GetPortState(p) != null)
            {
                c.openPort(p, os.thisComputer.ip);
            }
            else
            {
                Console.WriteLine($"{p} is not here...");
            }
        }
    }
    public static string StringCut(string s, int length)
    {
        // 防御性检查：空字符串、长度无效、或截断长度大于等于原长度，直接返回原字符串
        if (string.IsNullOrEmpty(s) || length <= 0 || length >= s.Length)
        {
            return s;
        }

        // 截取前 length 个字符
        return s.Substring(0, length);
    }
    public static void addFirewall(OS os,string compid,int lenth,float addtime,string solve)
    {
        Computer c = Programs.getComputer(os,compid);
        if(c == null) { Console.WriteLine($"Computer {compid} Not Found"); return; }
        Firewall f = new();

        f.additionalDelay = addtime;
        f.solutionLength = solve.Length;
        if (f.solution.ToLower() == "@random")
        {
            f.generateRandomSolution();
            string s = StringCut(f.solution, lenth);
            f.solution = s;
        }
        else f.solution = solve;
        c.firewall = f;
        c.firewall.solved=false;
    }

    public static void addProxy(OS os, string compid, int t)
    {
        Computer c = Programs.getComputer(os, compid);
        if(c == null) { Console.WriteLine($"Computer {compid} Not Found"); return; }

        
        c.addProxy(t);
        c.proxyActive = true;
    }

    public static void ProxyOverloadTicksHelper(OS os, string compid, float s,float dt)
    {
        Computer c = Programs.getComputer(os, compid);
        if(c == null) { Console.WriteLine($"Computer {compid} Not Found"); return; }

        float orgT = c.proxyOverloadTicks;
        c.proxyOverloadTicks += s * orgT * dt;
        
    }
    
    public static void ReRandomFirewallSolution(OS os, string compid, int lenth, float addTime, bool reset = true)
    {
        Computer c = Programs.getComputer(os, compid);
        if(c == null) { Console.WriteLine($"Computer {compid} Not Found"); return; }
        Firewall f = new();
        f.generateRandomSolution();
        string s = StringCut(f.solution, lenth);
        f.solution = s;
        f.additionalDelay = addTime;
        f.solutionLength = s.Length;

        c.firewall = f;
        if(reset) c.firewall.solved=false;
        
    }

    public static void ResetFirewall(OS os, string compid)
    {
        Computer c = Programs.getComputer(os, compid);  
        if(c == null) { Console.WriteLine($"Computer {compid} Not Found"); return; }

        c.firewallAnalysisInProgress = false;
    }

    public static void ResetProxy(OS os, string compid)
    {
        Computer c = Programs.getComputer(os, compid);
        if(c == null) { Console.WriteLine($"Computer {compid} Not Found"); return; }
        c.proxyActive=false;
        foreach (var exe in os.exes)
        {
            if (exe is PortHackExe)
            {
                exe.needsRemoval = true;
            }
        }

        c.proxyActive = true;
    }

    public static void solveFirewall(OS os, string compid, float delay)
    {
        Computer c = Programs.getComputer(os, compid);
        if(c == null) { Console.WriteLine($"Computer {compid} Not Found"); return; }
        if (delay <= 0) delay = 0;
        os.delayer.Post(ActionDelayer.Wait(delay), () =>
        {
            c.firewall.solved= true;
        });

    }
    public static void solveProxy(OS os, string compid, float delay)
    {
        Computer c = Programs.getComputer(os, compid);
        if (c == null) { Console.WriteLine($"Computer {compid} Not Found"); return; }
        if (delay <= 0) delay = 0;
        os.delayer.Post(ActionDelayer.Wait(delay), () =>
        {
            c.proxyActive = false;
        });

    }
    public static void AutoOverloadProxy(OS os ,string  compid, float speed,float dt)
    {
        Computer c = Programs.getComputer(os, compid);
        if (c == null) { Console.WriteLine($"Computer {compid} Not Found"); return; }
        c.proxyOverloadTicks += speed*dt;
    }

    public static void SetFirewallConfig(OS os,string compid,int lenth,float additiontime,string solution,bool reset)
    {
        Computer c = Programs.getComputer(os, compid);
        if (c == null) { Console.WriteLine($"Computer {compid} Not Found"); return; }
        if (c.firewall == null)
        {
            addFirewall(os, compid, lenth, additiontime, solution);
            return;
        }
        Firewall f = c.firewall;
        f.solutionLength = lenth;
        f.additionalDelay = additiontime;
        if (f.solution.ToLower() == "@random")
        {
            f.generateRandomSolution();
            string s = StringCut(f.solution, lenth);
            f.solution = s;
        }
        else f.solution = solution;
        c.firewall = f;
        if (reset){ c.firewall.solved = false; c.firewall.analysisPasses = 0; }
        
    }


}

