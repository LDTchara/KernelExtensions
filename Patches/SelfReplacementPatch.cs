using Hacknet;
using KernelExtensions.Utilities;
using Pathfinder.Event;
using Pathfinder.Event.Loading;
using System.Text.RegularExpressions;

namespace KernelExtensions.Patches
{
    /// <summary>
    /// 节点自替换符（9.63）：<c>#IP_&lt;节点id&gt;#</c> / <c>#NAME_&lt;节点id&gt;#</c>。
    /// <para>
    /// 挂 Pathfinder 的 <see cref="TextReplaceEvent"/>（它由 Pathfinder 在 <c>ComputerLoader.filter</c>
    /// 的 Postfix 上触发），对**原版替换链跑完的结果** <c>e.Replacement</c> 再做一轮参数化替换。
    /// 走官方扩展点 → 覆盖全部调用 <c>filter()</c> 的位置，也不与 Pathfinder / 其它模组的 filter patch
    /// 抢执行顺序（事件链统一）。
    /// </para>
    /// <para>
    /// 命名用**前缀式**：原版标识符空间里没有任何一条以 <c>IP_</c> / <c>NAME_</c> 开头，
    /// 结构上不会撞车（后缀式 <c>#&lt;id&gt;_IP#</c> 会与原版 <c>#PLAYER_IP#</c> / <c>#RANDOM_IP#</c> /
    /// <c>#GIBSON_IP#</c> 形态重叠，安全性依赖「原版先执行」这一隐式契约）。
    /// </para>
    /// <para>
    /// 行为约定：节点不存在 → **保留原文**（不记日志，作者看到没被替换即知写错）；大小写不敏感；
    /// **单遍不递归**（替换出的文本里再出现占位符不会再展开，与原版 <c>Replace</c> 链一致）；
    /// 替换是**一次性固化**的 —— 节点 IP 之后变化（如飞机坠机改 <c>DCLOC:</c> 前缀）不会回溯更新已替换文本。
    /// </para>
    /// </summary>
    internal static class SelfReplacementPatch
    {
        private static readonly Regex IpPattern = new("#IP_([^#]+)#", RegexOptions.Compiled);
        private static readonly Regex NamePattern = new("#NAME_([^#]+)#", RegexOptions.Compiled);

        /// <summary>订阅入口（主入口调用；handler 常驻整个插件生命周期）。</summary>
        public static void Initialize()
        {
            EventManager<TextReplaceEvent>.AddHandler(OnTextReplace);
            KELog.Info("[SelfReplace] #IP_<id># / #NAME_<id># handler registered.");
        }

        /// <summary>取消订阅（卸载时调用，避免持有已卸载程序集的委托）。</summary>
        public static void Dispose()
        {
            EventManager<TextReplaceEvent>.RemoveHandler(OnTextReplace);
        }

        private static void OnTextReplace(TextReplaceEvent e)
        {
            string text = e.Replacement;
            if (string.IsNullOrEmpty(text)) return;

            // 快速预判：不含 KE 前缀直接返回 —— filter() 调用极频繁，先把正则开销挡掉
            if (text.IndexOf("#IP_", StringComparison.Ordinal) < 0
                && text.IndexOf("#NAME_", StringComparison.Ordinal) < 0)
                return;

            // 防御：正常调用前 os 已就绪（原版 filter 自身就用 os.thisComputer），仍加保护
            OS os = ComputerLoader.os;
            if (os?.netMap?.nodes == null) return;

            string result = IpPattern.Replace(text, m => Resolve(os, m, useIp: true));
            result = NamePattern.Replace(result, m => Resolve(os, m, useIp: false));
            e.Replacement = result;
        }

        /// <summary>查到节点则替换；查不到保留原文（不记日志）。</summary>
        private static string Resolve(OS os, Match m, bool useIp)
        {
            Computer comp = FindNode(os, m.Groups[1].Value);
            if (comp == null) return m.Value;
            return useIp ? comp.ip : comp.name;
        }

        /// <summary>
        /// 大小写不敏感的「三路查找」（ip / idName / name）。
        /// <para>
        /// 刻意不用 <c>Programs.getComputer</c>：它内部就是这套线性遍历，但用 <c>string.Equals</c>
        /// （大小写敏感）；也不去 patch 它（会外溢影响所有模组与原版脚本）。
        /// 原版本就是 O(n)，Hacknet 的节点规模下成本可忽略，**无需缓存索引**。
        /// </para>
        /// </summary>
        private static Computer FindNode(OS os, string id)
        {
            var nodes = os.netMap.nodes;
            for (int i = 0; i < nodes.Count; i++)
            {
                Computer n = nodes[i];
                if (n == null) continue;
                if (string.Equals(n.ip, id, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(n.idName, id, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(n.name, id, StringComparison.OrdinalIgnoreCase))
                    return n;
            }
            return null;
        }
    }
}
