using Hacknet;
using Pathfinder.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using KernelExtensions;
using System.Threading.Tasks;
using Pathfinder.Meta.Load;
using Pathfinder.Replacements;
using Pathfinder.Util.XML;
using KernelExtensions.Utilities;

namespace KernelExtensions.ConnectControl;
public class ConnectionControlAction : Pathfinder.Action.PathfinderAction
{

    [XMLStorage]
    public string sourceComp;

    [XMLStorage]

    public string targetComp;

    [XMLStorage]
    public string mode;//reset add remove
    private int ComputerToNodeIndex(OS os, Computer computer)
    {
        return os.netMap.nodes.IndexOf(computer);
    }
    public override void Trigger(object os_obj)
    {
        OS os = (OS)os_obj;
        try
        {
            if (mode=="reset")
            {
                Computer src = Programs.getComputer(os, sourceComp);
                if (src == null)
                {
                    KELog.Error("ConnectControl reset: sourceComp unknown: " + sourceComp);
                    return;
                }

                List<Computer> computers = null;
                if (KernelExtensions.Computer_OrgLinkdComps.TryGetValue(src, out var direct))
                {
                    computers = direct;
                }
                else
                {
                    // 读档后 netMap.nodes 里同 idName 可能有多个对象，按 idName 兜底查找
                    foreach (var kv in KernelExtensions.Computer_OrgLinkdComps)
                    {
                        if (kv.Key != null && kv.Key.idName == sourceComp)
                        {
                            computers = kv.Value;
                            break;
                        }
                    }
                }

                if (computers == null)
                {
                    KELog.Warn("ConnectControl reset: no OrgLinks recorded for " + sourceComp);
                    return;
                }

                src.links = KernelExtensions.ComputersToNodeIndexes(os, computers);
                return;
            }
            Computer sc = Hacknet.Programs.getComputer(os, sourceComp);
            Computer tc = Hacknet.Programs.getComputer(os, targetComp);
            if (tc == null || sc == null )
            {
                KELog.Error("sourceComp or targetComp unknown.");
                throw new Exception("sourceComp or targetComp unknown.");
                
            }
            if(mode=="add")
            {
                int idx = ComputerToNodeIndex(os, tc);
                if (idx >= 0 && !sc.links.Contains(idx))
                {
                    sc.links.Add(idx);
                }
                // 注意：add 是临时连接，不写入字典（字典 = org 基线，避免污染）
            }

            if(mode=="remove")
            {
                int idx = ComputerToNodeIndex(os, tc);
                if (idx >= 0)
                {
                    sc.links.Remove(idx);
                }
                // 注意：remove 是临时连接操作，不写入字典（字典 = org 基线，避免污染）
            }



        }
        catch (Exception e)
        {
            throw new Exception(e.ToString());
        }
        
       

    }
    
}
[ComputerExecutor("OrgLinks")]
public class OrgLinksExecutor : ContentLoader.ComputerExecutor
{
    public override void Execute(EventExecutor exec, ElementInfo info)
    {
        // Comp = 正在解析的电脑，Os = 当前 OS，info = <OrgLinks> 元素的 ElementInfo
        List<string> linkedNames = info.Content?
            .Split(',')
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .ToList() ?? new List<string>();

        foreach (string name in linkedNames)
        {
            Computer target = Programs.getComputer(Os, name);
            if (target != null)
            {
                Comp.links.Add(Os.netMap.nodes.IndexOf(target));   // 还是存索引
            }
        }
    }
}


