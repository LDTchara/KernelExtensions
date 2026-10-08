using Hacknet;
using KernelExtensions.Utilities;
using Pathfinder.Event;
using Pathfinder.Event.Loading;
using System.Text.RegularExpressions;

namespace KernelExtensions.Patches
{
    /// <summary>
    /// 节点自替换符：<c>#IP_&lt;节点id&gt;#</c> / <c>#NAME_&lt;节点id&gt;#</c>。
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
            EventManager<OSLoadedEvent>.AddHandler(OnOSLoaded);
            KELog.Info("[SelfReplace] #IP_<id># / #NAME_<id># handler registered.");
        }

        /// <summary>取消订阅（卸载时调用，避免持有已卸载程序集的委托）。</summary>
        public static void Dispose()
        {
            EventManager<TextReplaceEvent>.RemoveHandler(OnTextReplace);
            EventManager<OSLoadedEvent>.RemoveHandler(OnOSLoaded);
        }

        private static void OnTextReplace(TextReplaceEvent e)
        {
            string text = e.Replacement;
            if (string.IsNullOrEmpty(text)) return;

            // 防御：正常调用前 os 已就绪（原版 filter 自身就用 os.thisComputer），仍加保护
            OS os = ComputerLoader.os;
            if (os?.netMap?.nodes == null) return;

            e.Replacement = ReplaceAll(text, os);
        }

        /// <summary>
        /// 加载完成后的**补替换**。
        /// <para>
        /// <c>filter()</c> 在**节点加载时**跑，那一刻 <c>netMap.nodes</c> 可能还不完整 ——
        /// 若文件内容引用了「尚未加载的节点」，那一轮只能查不到、保留原文
        /// （实测：`playerComp` 的文件引用 `testNode6`，而后者加载更晚）。
        /// 因此在 OSLoaded（<c>OS.LoadContent</c> 的 Postfix，此时 netMap 已完整）再扫一遍。
        /// </para>
        /// <para>
        /// **天然安全**：已替换过的文本不再含 <c>#IP_</c> / <c>#NAME_</c> 前缀，不会被二次处理。
        /// 只遍历**文件内容**；电脑的 name/ip 属性等按需再加。
        /// </para>
        /// </summary>
        private static void OnOSLoaded(OSLoadedEvent e)
        {
            OS os = e.Os;
            if (os?.netMap?.nodes == null) return;

            int resolved = 0;
            foreach (Computer comp in os.netMap.nodes)
            {
                if (comp?.files?.root == null) continue;
                resolved += PatchFolder(comp.files.root, os);
            }

            if (resolved > 0)
                KELog.Info($"[SelfReplace] resolved {resolved} placeholder file(s) after load (referenced nodes loaded later).");
        }

        /// <summary>递归遍历文件夹下的所有文件，对仍含占位符的内容做一次补替换。返回命中的文件数。</summary>
        private static int PatchFolder(Folder folder, OS os)
        {
            int count = 0;
            for (int i = 0; i < folder.files.Count; i++)
            {
                FileEntry f = folder.files[i];
                if (f?.data == null || f.data.Length == 0) continue;

                // 快速预判：绝大多数文件不含前缀，直接跳过
                if (f.data.IndexOf("#IP_", StringComparison.Ordinal) < 0
                    && f.data.IndexOf("#NAME_", StringComparison.Ordinal) < 0)
                    continue;

                string replaced = ReplaceAll(f.data, os);
                if (!string.Equals(replaced, f.data, StringComparison.Ordinal))
                {
                    f.data = replaced;
                    count++;
                }
            }
            for (int i = 0; i < folder.folders.Count; i++)
            {
                Folder sub = folder.folders[i];
                if (sub != null) count += PatchFolder(sub, os);
            }
            return count;
        }

        /// <summary>核心替换（主 handler 与补替换共用）。不含前缀时原样返回。</summary>
        private static string ReplaceAll(string text, OS os)
        {
            // 快速预判：不含 KE 前缀直接返回 —— filter() 调用极频繁，先把正则开销挡掉
            if (text.IndexOf("#IP_", StringComparison.Ordinal) < 0
                && text.IndexOf("#NAME_", StringComparison.Ordinal) < 0)
                return text;

            string result = IpPattern.Replace(text, m => Resolve(os, m, useIp: true));
            return NamePattern.Replace(result, m => Resolve(os, m, useIp: false));
        }

        /// <summary>查到节点则替换；查不到保留原文（不记日志）。</summary>
        private static string Resolve(OS os, Match m, bool useIp)
        {
            Computer comp = FindNode(os, m.Groups[1].Value);
            if (comp == null) return m.Value;
            return useIp ? comp.ip : comp.name;
        }

        /// <summary>
        /// 按 <c>idName</c> 查找（大小写不敏感）。
        /// <para>
        /// **只匹配 idName**：节点自替换符的语义是「用你在节点 XML 里写的那个 id 引用它」。
        /// 刻意不去匹配 <c>ip</c> 或 <c>name</c>——没人会写 <c>#IP_235.7.94.131#</c>，
        /// 而 <c>name</c> 本身可能含占位符（如 <c>#PLAYERNAME# 作战基地</c>），用它匹配会引入歧义。
        /// </para>
        /// </summary>
        private static Computer FindNode(OS os, string id)
        {
            var nodes = os.netMap.nodes;
            for (int i = 0; i < nodes.Count; i++)
            {
                Computer n = nodes[i];
                if (n == null) continue;
                if (string.Equals(n.idName, id, StringComparison.OrdinalIgnoreCase))
                    return n;
            }
            return null;
        }
    }
}
