using Hacknet;
using KernelExtensions.Utilities;
using Pathfinder.Util;

namespace KernelExtensions.Actions.Misc
{
    /// <summary>
    /// 目标机原子动作共用的解析与日志。
    ///
    /// 为什么要统一：这些动作都作用在"某台电脑"上，而取不到电脑是**最常见的作者错误**
    /// （idName 拼错 / 那台机还没生成）。历史实现只在 Console 里写一句，玩家与作者都看不到，
    /// 排查全靠猜。此处统一成：取不到 → <see cref="KELog.Warn"/> 一条并**放弃本次操作**，
    /// 绝不静默继续（静默会让脚本"看起来跑过了"但什么都没发生）。
    /// </summary>
    internal static class TargetActionHelper
    {
        /// <summary>
        /// 解析动作的目标机。
        /// <paramref name="explicitIdName"/> 留空 / NONE = 当前连接的节点
        /// （对齐 <c>os.connectedComp ?? os.thisComputer</c>，即"没指定就是眼前这台"）。
        /// </summary>
        /// <param name="os">当前 OS。</param>
        /// <param name="explicitIdName">作者写的目标机 idName，可留空。</param>
        /// <param name="actionName">日志定位用的动作名。</param>
        /// <param name="target">解析结果；失败为 null。</param>
        /// <returns>是否解析成功。</returns>
        public static bool TryResolve(OS os, string explicitIdName, string actionName, out Computer target)
        {
            target = null;
            if (os == null) return false;

            if (ConfigValue.IsNone(explicitIdName))
            {
                target = os.connectedComp ?? os.thisComputer;
                if (target == null)
                {
                    KELog.Warn($"[{actionName}] no connected computer and no TargetComp given; skipped.");
                    return false;
                }
                return true;
            }

            target = Programs.getComputer(os, explicitIdName);
            if (target == null)
            {
                KELog.Warn($"[{actionName}] computer '{explicitIdName}' not found; skipped.");
                return false;
            }
            return true;
        }

        /// <summary>
        /// 逗号分隔的协议名列表 → 去空去重的 List。
        /// 支持 <c>ssh,ftp</c> 与 <c>ssh;ftp</c> 两种分隔（历史上两种都出现过）。
        /// </summary>
        public static List<string> ParseProtocolList(string raw)
        {
            List<string> result = new();
            if (string.IsNullOrWhiteSpace(raw)) return result;

            foreach (string part in raw.Split(',', ';'))
            {
                string p = part?.Trim();
                if (string.IsNullOrEmpty(p)) continue;
                if (ConfigValue.IsNone(p)) continue;
                if (!result.Contains(p)) result.Add(p);
            }
            return result;
        }

        /// <summary>记录一次成功操作（动作级结果，一次触发一条）。</summary>
        public static void LogDone(string actionName, string what, Computer target)
            => KELog.Info($"[{actionName}] {what} on '{target?.idName}'.");

        /// <summary>
        /// 解析兼容原版写法的布尔属性：<c>true/false</c>（大小写不敏感），
        /// 留空 / NONE / 无法识别 = <paramref name="fallback"/>。
        /// </summary>
        public static bool ParseBool(string raw, bool fallback)
        {
            if (ConfigValue.IsNone(raw)) return fallback;
            if (bool.TryParse(raw.Trim(), out bool v)) return v;
            return fallback;
        }
    }
}
