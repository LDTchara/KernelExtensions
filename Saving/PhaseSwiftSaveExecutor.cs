using Hacknet;
using Pathfinder.Meta.Load;
using Pathfinder.Replacements;
using Pathfinder.Util.XML;
using KernelExtensions.Modules;

namespace KernelExtensions.Saving
{
    /// <summary>
    /// 读取存档中的 <PhaseSwiftData> 节点，恢复 PhaseSwift 持久化状态。
    /// 存入 PhaseSwiftManager.PendingRestore 供 OS 加载后自动恢复。
    /// </summary>
    // ParseInterior：必须解析子元素（DiscoveredScene/OrigLink 等），否则 info.Children 恒为空
    [SaveExecutor("PhaseSwiftData", ParseOption.ParseInterior)]
    public class PhaseSwiftSaveExecutor : SaveLoader.SaveExecutor
    {
        private static string GetAttr(ElementInfo info, string key, string fallback)
        {
            return info.Attributes.ContainsKey(key) ? info.Attributes[key] : fallback;
        }

        public override void Execute(EventExecutor exec, ElementInfo info)
        {
            var state = new PhaseSwiftPersistentState();

            state.ConfigName = GetAttr(info, "ConfigName", "");
            if (string.IsNullOrEmpty(state.ConfigName)) return;

            int.TryParse(GetAttr(info, "CurrentScene", "0"), out state.Scene);
            int.TryParse(GetAttr(info, "MusicPhase", "0"), out state.MusicPhase);
            state.Theme = GetAttr(info, "Theme", "");

            // 解析各场景已发现节点
            foreach (var child in info.Children)
            {
                if (child.Name != "DiscoveredScene") continue;
                if (!int.TryParse(GetAttr(child, "Index", "-1"), out int idx) || idx < 0) continue;
                var nodes = new HashSet<string>();
                foreach (var n in child.Children)
                {
                    if (!string.IsNullOrEmpty(n.Content))
                        nodes.Add(n.Content);
                }
                state.DiscoveredNodes[idx] = nodes;
            }

            // 解析原始链接（节点 ID → 节点 ID，OS 就绪后再转数字位置）
            foreach (var child in info.Children)
            {
                if (child.Name != "OrigLink") continue;
                string nodeId = GetAttr(child, "NodeId", "");
                string targets = GetAttr(child, "Targets", "");
                if (string.IsNullOrEmpty(nodeId) || string.IsNullOrEmpty(targets)) continue;
                state.OriginalLinkIds[nodeId] = new System.Collections.Generic.List<string>(
                    targets.Split(new char[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries));
            }

            // 解析运行时黑名单（9.8）
            foreach (var child in info.Children)
            {
                if (child.Name != "RuntimeBlockedScene") continue;
                if (!int.TryParse(GetAttr(child, "Index", "-1"), out int idx) || idx < 0) continue;
                var nodes = new HashSet<string>();
                foreach (var n in child.Children)
                {
                    if (!string.IsNullOrEmpty(n.Content))
                        nodes.Add(n.Content);
                }
                state.RuntimeBlocked[idx] = nodes;
            }

            // 解析各场景 admin 记录（9.16）
            foreach (var child in info.Children)
            {
                if (child.Name != "AdminScene") continue;
                if (!int.TryParse(GetAttr(child, "Index", "-1"), out int idx) || idx < 0) continue;
                var nodes = new HashSet<string>();
                foreach (var n in child.Children)
                {
                    if (!string.IsNullOrEmpty(n.Content))
                        nodes.Add(n.Content);
                }
                state.AdminNodes[idx] = nodes;
            }

            PhaseSwiftManager.PendingRestore = state;
        }
    }
}
