using System.Collections.Generic;

namespace KernelExtensions.Storage
{
    /// <summary>
    /// 全局存储每个试炼配置删除的节点索引（int 列表）。
    /// 用于在游戏会话中持久化删除记录，并在存档/读档时同步。
    /// </summary>
    public static class CustomTrialNodeStorage
    {
        private static readonly Dictionary<string, List<int>> _deletedNodesMap = new();

        /// <summary>
        /// 为指定配置添加一个被删除的节点索引。
        /// </summary>
        public static void AddDeletedNode(string configName, int nodeIndex)
        {
            if (!_deletedNodesMap.TryGetValue(configName, out var list))
            {
                list = new List<int>();
                _deletedNodesMap[configName] = list;
            }
            if (!list.Contains(nodeIndex))
                list.Add(nodeIndex);
        }

        /// <summary>
        /// 获取指定配置的所有被删除节点索引列表（副本）。
        /// </summary>
        public static List<int> GetDeletedNodes(string configName)
        {
            if (_deletedNodesMap.TryGetValue(configName, out var list))
                return new List<int>(list);
            return new List<int>();
        }

        /// <summary>
        /// 设置指定配置的被删除节点列表（用于从存档加载时覆盖）。
        /// </summary>
        public static void SetDeletedNodes(string configName, List<int> nodes)
        {
            _deletedNodesMap[configName] = new List<int>(nodes);
        }

        /// <summary>
        /// 清除指定配置的所有删除记录。
        /// </summary>
        public static void ClearDeletedNodes(string configName)
        {
            _deletedNodesMap.Remove(configName);
        }

        /// <summary>
        /// 检查指定配置是否有删除记录。
        /// </summary>
        public static bool HasDeletedNodes(string configName)
        {
            return _deletedNodesMap.ContainsKey(configName) && _deletedNodesMap[configName].Count > 0;
        }
    }
}