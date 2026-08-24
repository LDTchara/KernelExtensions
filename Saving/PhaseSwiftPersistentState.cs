using System.Collections.Generic;

namespace KernelExtensions.Saving
{
    /// <summary>
    /// PhaseSwift 持久化状态的数据传输对象。
    /// 由 PhaseSwiftSaveExecutor 解析存档 XML 填充，
    /// 存入 PhaseSwiftManager.PendingRestore，供 AutoRestore 使用。
    /// </summary>
    public class PhaseSwiftPersistentState
    {
        public string ConfigName;
        public int Scene;
        public int MusicPhase;
        public string Theme;

        /// <summary>场景索引 → 该场景已发现的节点 ID 列表</summary>
        public Dictionary<int, HashSet<string>> DiscoveredNodes = new();

        /// <summary>节点 ID → 原始链接目标节点 ID 列表（跨会话安全）</summary>
        public Dictionary<string, List<string>> OriginalLinkIds = new();

        /// <summary>场景索引 → 运行时黑名单节点 ID</summary>
        public Dictionary<int, HashSet<string>> RuntimeBlocked = new();

    }
}
