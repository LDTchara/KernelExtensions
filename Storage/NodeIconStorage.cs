using System.Collections.Generic;

namespace KernelExtensions.Storage
{
    /// <summary>
    /// 全局存储节点的原始图标（OrgIcon）和当前图标（CurrIcon）。
    /// 键为节点的 idName，值为 icon 字符串（预设名如 "laptop" 或自定义路径）。
    /// 在游戏会话中持久化，并在存档/读档时同步。
    /// </summary>
    public static class NodeIconStorage
    {
        private static readonly Dictionary<string, string> _orgIcons = new();
        private static readonly Dictionary<string, string> _currIcons = new();

        /// <summary>
        /// 获取原始图标（可能为 null，表示从未设置过，使用默认图标）。
        /// </summary>
        public static string GetOrgIcon(string nodeId)
        {
            return _orgIcons.TryGetValue(nodeId, out var val) ? val : null;
        }

        /// <summary>
        /// 获取当前图标（可能为 null）。
        /// </summary>
        public static string GetCurrIcon(string nodeId)
        {
            return _currIcons.TryGetValue(nodeId, out var val) ? val : null;
        }

        /// <summary>
        /// 是否有记录的图标数据（用于判断是否需要写入存档）。
        /// </summary>
        public static bool HasAnyData => _orgIcons.Count > 0 || _currIcons.Count > 0;

        /// <summary>
        /// 获取所有 OrgIcon 条目（用于存档序列化）。
        /// </summary>
        public static Dictionary<string, string> GetAllOrgIcons()
        {
            return new Dictionary<string, string>(_orgIcons);
        }

        /// <summary>
        /// 获取所有 CurrIcon 条目（用于存档序列化）。
        /// </summary>
        public static Dictionary<string, string> GetAllCurrIcons()
        {
            return new Dictionary<string, string>(_currIcons);
        }

        /// <summary>
        /// 初始化节点的原始图标（仅当 OrgIcon 尚不存在时设置，幂等）。
        /// 应在节点首次被加载到 netMap 时调用（OSLoaded / 动态创建节点时）。
        /// </summary>
        /// <param name="nodeId">节点 idName</param>
        /// <param name="icon">节点当前的 icon 值（null 表示默认图标）</param>
        public static void InitOrgIcon(string nodeId, string icon)
        {
            if (!_orgIcons.ContainsKey(nodeId))
            {
                _orgIcons[nodeId] = icon ?? "";
            }
        }

        /// <summary>
        /// 设置节点的当前图标（CurrIcon），不触碰 OrgIcon。
        /// Action 的 Set 分支和存档加载恢复都应使用此方法。
        /// </summary>
        public static void SetCurrIcon(string nodeId, string icon)
        {
            _currIcons[nodeId] = icon ?? "";
        }

        /// <summary>
        /// securityLevel 内部图标名前缀。
        /// 如 SEC_LEVEL_2 表示 securityLevel=2 → Sprites/CompLogos/Computer。
        /// </summary>
        public const string SEC_PREFIX = "SEC_LEVEL_";

        /// <summary>
        /// 根据 securityLevel 生成内部图标名。
        /// </summary>
        public static string GetSecurityIconName(int secLevel)
        {
            return SEC_PREFIX + (secLevel >= 6 ? 6 : secLevel);
        }

        /// <summary>
        /// 检查指定节点的 OrgIcon 是否已被初始化（包括初始化为默认图标的空字符串）。
        /// </summary>
        public static bool HasOrgIcon(string nodeId)
        {
            return _orgIcons.ContainsKey(nodeId);
        }

        /// <summary>
        /// 重置节点图标为原始图标，并覆盖 CurrIcon。
        /// </summary>
        /// <returns>原始图标值（空字符串表示默认图标，null 表示从未初始化）</returns>
        public static string ResetIcon(string nodeId)
        {
            if (_orgIcons.TryGetValue(nodeId, out var orgIcon))
            {
                _currIcons[nodeId] = orgIcon;
                return orgIcon;
            }
            return null;
        }

        /// <summary>
        /// 从存档加载时批量设置（直接覆盖现有数据）。
        /// </summary>
        /// <param name="nodeId">节点 idName</param>
        /// <param name="orgIcon">原始图标（空字符串表示 null）</param>
        /// <param name="currIcon">当前图标（空字符串表示 null）</param>
        public static void LoadFromSave(string nodeId, string orgIcon, string currIcon)
        {
            if (orgIcon != null)
                _orgIcons[nodeId] = orgIcon;
            if (currIcon != null)
                _currIcons[nodeId] = currIcon;
        }

        /// <summary>
        /// 清除所有数据（用于新游戏或重置）。
        /// </summary>
        public static void Clear()
        {
            _orgIcons.Clear();
            _currIcons.Clear();
        }
    }
}
