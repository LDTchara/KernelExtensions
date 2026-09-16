using Hacknet;
using KernelExtensions.Utilities;
using Pathfinder.Event.Loading;
using Pathfinder.Event.Saving;
using Pathfinder.Util.XML;
using System.Xml.Linq;

namespace KernelExtensions.Storage
{
    /// <summary>
    /// org（组织）链接基线：静态状态 + 存档钩子 + 重置/增删操作。
    /// 由 LinkControlReset / LinkControlAdd / LinkControlRemove 三个 Action 共用；
    /// 事件钩子在主入口 KernelExtensions.Load 注册。
    ///
    /// org 基线生命周期：
    ///   · 新游戏 —— OSLoaded 时对每台电脑的当前 links 做快照（内容 XML 的 dlink + &lt;OrgLinks&gt; 解析结果）；
    ///   · 内容 XML —— 电脑子元素 &lt;OrgLinks&gt;compA,compB&lt;/OrgLinks&gt; 定义初始组织链接
    ///     （Storage/OrgLinksExecutor 加载时并入 links，从而进入新游戏基线）；
    ///   · 保存 —— 基线写入存档 &lt;OrgLinks&gt; 标签（附 ALLSAVED 标记，读取时过滤）；
    ///   · 读档 —— SaveComputerLoadedEvent 暂存、OSLoaded 统一恢复（避免逐台加载顺序丢链接）。
    /// 基线 key 用 idName（大小写不敏感），跨存档稳定；读档后按 idName 重新解析目标。
    /// </summary>
    internal static class OrgLinksStorage
    {
        private static readonly Dictionary<string, List<string>> _orgBaseline = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, List<string>> _pendingOrgLinkIds = new(StringComparer.OrdinalIgnoreCase);
        private const string AllSavedMarker = "ALLSAVED";

        // ==================== 操作 ====================

        /// <summary>把 SourceComp 的 links 整体恢复为 org 基线（丢弃所有临时增删）。</summary>
        public static bool Reset(OS os, string sourceComp)
        {
            if (string.IsNullOrWhiteSpace(sourceComp))
            {
                KELog.Error("[LinkControl] SourceComp is required.");
                return false;
            }

            Computer src = Programs.getComputer(os, sourceComp);
            if (src == null)
            {
                KELog.Error($"[LinkControl] reset: SourceComp unknown: {sourceComp}");
                return false;
            }

            if (!_orgBaseline.TryGetValue(src.idName ?? sourceComp, out var baseline) || baseline.Count == 0)
            {
                KELog.Warn($"[LinkControl] reset: no OrgLinks recorded for {sourceComp}");
                return false;
            }

            // 基线 idName → 节点索引（读档后 netMap 可能是新对象，按 idName 解析目标）
            var indexes = new List<int>();
            foreach (string targetId in baseline)
            {
                Computer target = Programs.getComputer(os, targetId);
                if (target == null) continue;
                int idx = os.netMap.nodes.IndexOf(target);
                if (idx >= 0) indexes.Add(idx);
            }
            src.links = indexes;
            return true;
        }

        /// <summary>运行时临时加/删一条链接（不写 org 基线；存档时由原版处理 links）。</summary>
        public static bool Modify(OS os, string sourceComp, string targetComp, bool add)
        {
            string op = add ? "add" : "remove";

            if (string.IsNullOrWhiteSpace(sourceComp))
            {
                KELog.Error("[LinkControl] SourceComp is required.");
                return false;
            }
            if (string.IsNullOrWhiteSpace(targetComp))
            {
                KELog.Error($"[LinkControl] {op} requires TargetComp.");
                return false;
            }

            Computer sc = Programs.getComputer(os, sourceComp);
            Computer tc = Programs.getComputer(os, targetComp);
            if (sc == null || tc == null)
            {
                KELog.Error($"[LinkControl] {op}: SourceComp or TargetComp unknown: {sourceComp} / {targetComp}");
                return false;
            }

            int idx = os.netMap.nodes.IndexOf(tc);
            if (idx < 0) return false;

            if (add)
            {
                if (!sc.links.Contains(idx)) sc.links.Add(idx);
            }
            else
            {
                sc.links.Remove(idx);
            }
            return true;
        }

        // ==================== 存档钩子（主入口 KernelExtensions.Load 注册） ====================

        /// <summary>保存：把 org 基线写入该电脑存档的 &lt;OrgLinks&gt; 标签（附 ALLSAVED 标记，读取时过滤）。</summary>
        public static void OnSaveComputer(SaveComputerEvent e)
        {
            string id = e.Comp?.idName;
            if (string.IsNullOrEmpty(id)) return;

            string content = AllSavedMarker;
            if (_orgBaseline.TryGetValue(id, out var list) && list.Count > 0)
                content = string.Join(",", list) + "," + AllSavedMarker;
            e.Element.Add(new XElement("OrgLinks", content));
        }

        /// <summary>读档：解析存档 &lt;OrgLinks&gt;，暂存 idName 列表（推迟到 OSLoaded 统一解析）。</summary>
        public static void OnLoadComputer(SaveComputerLoadedEvent e)
        {
            string id = e.Comp?.idName;
            if (string.IsNullOrEmpty(id) || e.Info == null) return;

            ElementInfo orgLinks = e.Info.Children.GetElement("OrgLinks");
            if (orgLinks == null) return;

            var names = (orgLinks.Content ?? "")
                .Split(',')
                .Select(s => s.Trim())
                .Where(s => s.Length > 0 && s != AllSavedMarker)
                .ToList();
            _pendingOrgLinkIds[id] = names;
        }

        /// <summary>OSLoaded：先恢复读档暂存的基线，再为无基线节点用当前 links 补建（新游戏=内容定义快照）。</summary>
        public static void OnOSLoaded(OSLoadedEvent e)
        {
            OS os = e.Os;

            // 读档场景：所有电脑已加载，暂存 → 基线（无顺序依赖）
            foreach (var pending in _pendingOrgLinkIds)
            {
                if (!_orgBaseline.ContainsKey(pending.Key))
                    _orgBaseline[pending.Key] = pending.Value;
            }
            _pendingOrgLinkIds.Clear();

            // 新游戏 / 无基线节点：当前 links 快照 = org 基线（内容 XML 的 dlink + <OrgLinks> 解析结果）
            foreach (Computer c in os.netMap.nodes)
            {
                if (c.idName == null || _orgBaseline.ContainsKey(c.idName)) continue;
                _orgBaseline[c.idName] = c.links
                    .Where(i => i >= 0 && i < os.netMap.nodes.Count && os.netMap.nodes[i].idName != null)
                    .Select(i => os.netMap.nodes[i].idName!)
                    .ToList();
            }
        }
    }
}
