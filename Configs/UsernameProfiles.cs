using KernelExtensions.Utilities;
using System.Xml.Linq;

namespace KernelExtensions.Configs
{
    /// <summary>
    /// 禁用用户名配置（KE-Config.xml 的 &lt;BannedUsernames&gt; 段，dev1 用户名系统 XML 化）。
    /// 由 ConfigLoader 在 OSLoad 时解析（热重载）；PatchAccountName 在创建账号时检查。
    /// 结构：
    ///   &lt;Reasons&gt; 随机原因块（&lt;Block Name="X"&gt;&lt;Reason&gt;...）——被 Ban 的 ReasonBlock 引用后随机选一条
    ///   &lt;Ban Name="用户名" Reason="直接原因" ReasonBlock="块名" /&gt;（同名多条=多原因随机）
    /// </summary>
    public static class UsernameProfiles
    {
        private static readonly Dictionary<string, List<string>> _bans = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, List<string>> _reasons = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Random _rng = new();

        /// <summary>是否存在任何禁用规则（无规则时 PatchAccountName 直接放行）。</summary>
        public static bool HasBans { get; private set; }

        /// <summary>在显示禁用原因之前触发，允许修改原因（返回 null/空则保留原始）。</summary>
        public static Func<string, string, string> OnBeforeShowReason;

        /// <summary>在显示禁用原因之后触发，用于执行额外逻辑（不可修改原因）。</summary>
        public static event Action<string, string> OnAfterShowReason;

        /// <summary>解析 KE-Config.xml 的 BannedUsernames 段（null/解析失败保持空表）。</summary>
        public static void Apply(XElement section)
        {
            _bans.Clear();
            _reasons.Clear();
            HasBans = false;
            if (section == null) return;

            foreach (var block in section.Element("Reasons")?.Elements("Block") ?? Enumerable.Empty<XElement>())
            {
                string name = (string)block.Attribute("Name");
                if (string.IsNullOrWhiteSpace(name)) continue;
                var list = block.Elements("Reason")
                    .Select(e => e.Value.Trim())
                    .Where(v => !string.IsNullOrEmpty(v))
                    .ToList();
                if (list.Count > 0) _reasons[name] = list;
            }

            foreach (var ban in section.Elements("Ban"))
            {
                string name = (string)ban.Attribute("Name");
                if (string.IsNullOrWhiteSpace(name)) continue;
                var list = new List<string>();
                string direct = (string)ban.Attribute("Reason");
                if (!string.IsNullOrWhiteSpace(direct)) list.Add(direct.Trim());
                string blockRef = (string)ban.Attribute("ReasonBlock");
                if (!string.IsNullOrWhiteSpace(blockRef) && _reasons.TryGetValue(blockRef, out var blockList))
                    list.AddRange(blockList);
                if (list.Count > 0) _bans[name] = list;
            }

            HasBans = _bans.Count > 0;
        }

        /// <summary>是否命中禁用名单（大小写不敏感）。</summary>
        public static bool IsBanned(string username)
            => !string.IsNullOrEmpty(username) && _bans.ContainsKey(username);

        /// <summary>取禁用原因（多条时随机；无则默认文案，走 KELoc）。</summary>
        public static string GetReason(string username)
        {
            if (!_bans.TryGetValue(username, out var list) || list.Count == 0)
                return KELoc.Loc("USERNAME_DEFAULT_REASON", "Unusable User Name");
            return list[_rng.Next(list.Count)];
        }

        /// <summary>触发 Pre 阶段钩子（PatchAccountName 调用）。</summary>
        internal static void TriggerBeforeShowReason(ref string reason, string username)
        {
            if (OnBeforeShowReason == null) return;
            try
            {
                string newReason = OnBeforeShowReason(reason, username);
                if (!string.IsNullOrEmpty(newReason)) reason = newReason;
            }
            catch (Exception ex)
            {
                KELog.Warn($"[UsernameProfiles] OnBeforeShowReason error: {ex.Message}");
            }
        }

        /// <summary>触发 Post 阶段钩子（PatchAccountName 调用）。</summary>
        internal static void TriggerAfterShowReason(string username, string reason)
        {
            if (OnAfterShowReason == null) return;
            try { OnAfterShowReason?.Invoke(username, reason); }
            catch (Exception ex)
            {
                KELog.Warn($"[UsernameProfiles] OnAfterShowReason error: {ex.Message}");
            }
        }
    }
}
