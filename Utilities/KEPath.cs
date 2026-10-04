using Hacknet;
using Hacknet.Extensions;

namespace KernelExtensions.Utilities
{
    /// <summary>
    /// 扩展作者书写路径的统一解析与越界校验。
    /// <para>
    /// 扩展配置里由作者书写的路径（<c>ConfigPath</c> / <c>HelpFile</c> / <c>SystemLogFiles</c> /
    /// <c>FakeFiles[].Source</c> / <c>CheckFilePath</c> / <c>CheckFilePattern</c> /
    /// <c>ActionOnGuideTextStart</c> 等）一律**相对于某个根目录**（扩展根目录或存档基础目录），
    /// 且**不得逃出该根目录**。
    /// </para>
    /// <para>
    /// 直接用 <see cref="Path.Combine(string, string)"/> 拼接是不安全的：<c>../</c> 可以读到根目录之外，
    /// 绝对路径与 Windows 盘符会让 <c>Combine</c> 直接丢弃根目录。
    /// 本类先用 <see cref="Path.GetFullPath(string)"/> 规范化（这会展开 <c>.</c> 与 <c>..</c>），
    /// 再比对规范化后的根前缀，一次覆盖上述三类逃逸。
    /// </para>
    /// <para>
    /// 已知盲区：根目录内的**符号链接**指向外部时无法察觉（<c>GetFullPath</c> 不做 realpath）。
    /// 在扩展威胁模型下可接受 —— 能创建符号链接的人本来就能读那些文件。
    /// </para>
    /// </summary>
    public static class KEPath
    {
        /// <summary>
        /// 解析相对扩展根目录的路径。越界或无效时返回 <c>null</c>。
        /// </summary>
        public static string ResolveInsideExtension(string relativePath)
        {
            return ResolveInside(relativePath, ExtensionLoader.ActiveExtensionInfo?.FolderPath);
        }

        /// <summary>
        /// 解析相对存档基础目录（<see cref="HostileHackerBreakinSequence.GetBaseDirectory"/>）的路径。
        /// 用于 <c>FakeFiles[].Path</c> / <c>CheckFilePath</c> 这类「相对存档目录」的配置项。
        /// </summary>
        public static string ResolveInsideSaveBase(string relativePath)
        {
            return ResolveInside(relativePath, HostileHackerBreakinSequence.GetBaseDirectory());
        }

        /// <summary>
        /// 把 <paramref name="relativePath"/> 解析为 <paramref name="rootDirectory"/> 内的绝对路径。
        /// <para>
        /// 拒绝：绝对路径、Windows 盘符、以及用 <c>..</c> 逃出根目录的路径。
        /// 失败（含路径无效、含非法字符）返回 <c>null</c>。
        /// </para>
        /// </summary>
        public static string ResolveInside(string relativePath, string rootDirectory)
        {
            if (string.IsNullOrWhiteSpace(relativePath) || string.IsNullOrEmpty(rootDirectory))
                return null;

            string root;
            string combined;
            try
            {
                root = Path.GetFullPath(rootDirectory);
                combined = Path.GetFullPath(Path.Combine(root, relativePath));
            }
            catch
            {
                // 非法字符、路径过长等
                return null;
            }

            // 比对时补上目录分隔符，避免 ".../Extensions/KE" 误命中 ".../Extensions/KE2"
            string prefix = root.EndsWith(Path.DirectorySeparatorChar.ToString())
                ? root
                : root + Path.DirectorySeparatorChar;

            // Windows 文件系统不区分大小写，Unix 区分
            StringComparison comparison = Path.DirectorySeparatorChar == '\\'
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;

            if (string.Equals(combined, root, comparison)) return combined;      // 相对路径为空 / "." 的情形
            return combined.StartsWith(prefix, comparison) ? combined : null;
        }

        /// <summary>
        /// 归一化为「相对路径」形式（正斜杠、去掉开头的 <c>./</c> 与 <c>/</c>），
        /// 用于生成稳定的标识（如 VM 攻击的感染 flag）。不做越界判断。
        /// </summary>
        public static string NormalizeRelative(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath)) return null;
            string p = relativePath.Trim().Replace('\\', '/');
            while (p.StartsWith("./")) p = p.Substring(2);
            p = p.TrimStart('/');
            return p.Length == 0 ? null : p;
        }
    }
}
