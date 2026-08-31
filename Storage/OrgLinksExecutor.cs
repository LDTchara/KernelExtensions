using Hacknet;
using Pathfinder.Meta.Load;
using Pathfinder.Replacements;
using Pathfinder.Util;
using Pathfinder.Util.XML;

namespace KernelExtensions.Storage
{
    /// <summary>
    /// 内容加载时解析电脑 XML 的 &lt;OrgLinks&gt; 子元素（逗号分隔 idName），并入 links。
    /// 内容侧定义组织基线（ConnectControl reset 的恢复对象）；读档侧基线由存档 &lt;OrgLinks&gt; 恢复
    /// （Actions/ConnectionControlAction 的存档钩子处理）。
    /// 使用 [ComputerExecutor] 特性自动注册，无需手动调用（对齐 NodeIconComputerExecutor 模式）。
    /// </summary>
    [ComputerExecutor("OrgLinks")]
    public class OrgLinksExecutor : ContentLoader.ComputerExecutor
    {
        public override void Execute(EventExecutor exec, ElementInfo info)
        {
            var linkedNames = (info.Content ?? "")
                .Split(',')
                .Select(s => s.Trim())
                .Where(s => s.Length > 0)
                .ToList();

            foreach (string name in linkedNames)
            {
                Computer target = Programs.getComputer(Os, name);
                if (target == null) continue;
                int idx = Os.netMap.nodes.IndexOf(target);
                if (idx >= 0 && !Comp.links.Contains(idx))
                    Comp.links.Add(idx);
            }
        }
    }
}
