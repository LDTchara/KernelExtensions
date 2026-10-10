using Hacknet;
using Hacknet.Extensions;

namespace KernelExtensions.Utilities
{
    /// <summary>
    /// 动作点（action hook）执行器。
    ///
    /// 之所以单独抽出来：程序里的动作点是**两层**的（根级 + 每个协议各有），
    /// 且都相对扩展根书写、都可能越界或缺失。统一在这里做「越界/缺失 → Warn 并跳过」，
    /// 避免每个调用点各写一遍校验、也避免因为一个配错的动作文件中断整条破解流程。
    ///
    /// 语义约定：动作点缺失或无效**不是错误**，只是作者没配 —— 记 Warn 后继续跑。
    /// </summary>
    internal static class PortHackV2Hooks
    {
        /// <summary>
        /// 执行一个动作点文件；留空 / NONE / 越界 / 文件不存在 → 记日志后跳过，不抛。
        /// </summary>
        /// <param name="os">当前 OS。</param>
        /// <param name="actionPath">作者书写的路径，相对扩展根。</param>
        /// <param name="source">日志定位用，形如 <c>MyConfig/ssh</c>。</param>
        /// <param name="point">日志定位用，动作点名（如 OnComplete）。</param>
        public static void Run(OS os, string actionPath, string source, string point)
        {
            if (os == null) return;
            if (ConfigValue.IsNone(actionPath)) return;   // 作者没配这个动作点：正常情况，不记日志

            string root = ExtensionLoader.ActiveExtensionInfo?.FolderPath?.Replace('\\', '/');
            if (string.IsNullOrEmpty(root))
            {
                KELog.Warn($"[PortHackV2] '{source}': cannot run <{point}> '{actionPath}' - extension root unavailable.");
                return;
            }

            // 越界（../、绝对路径、盘符）由 KEPath 拦下；此处不生成任何文件、也不执行
            string full = KEPath.ResolveInside(actionPath, root);
            if (full == null)
            {
                KELog.Warn($"[PortHackV2] '{source}': <{point}> '{actionPath}' escapes the extension folder; skipped.");
                return;
            }

            if (!File.Exists(full))
            {
                KELog.Warn($"[PortHackV2] '{source}': <{point}> file not found: {actionPath}");
                return;
            }

            try
            {
                ActionHelper.ExecuteActionFile(os, actionPath, root);
                KELog.Info($"[PortHackV2] '{source}': <{point}> executed ({actionPath}).");
            }
            catch (Exception ex)
            {
                KELog.Error($"[PortHackV2] '{source}': <{point}> failed: {ex.Message}");
            }
        }
    }
}
