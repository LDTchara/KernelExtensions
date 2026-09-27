using BepInEx;
using Hacknet.Extensions;

namespace KernelExtensions.Utilities
{
    public static class MusicPathResolver
    {
        /// <summary>
        /// 将配置中的音乐字符串转换为 MusicManager 能识别的路径。
        /// 规则：
        /// 0. NONE/空 → 原样返回；绝对路径或已带 "../Extensions/" 前缀 → 原样返回。
        /// 1. 字符串含路径分隔符（/ 或 \）：
        ///    a. 若扩展目录下**确实存在**该文件 → "../Extensions/扩展名/路径"（扩展内相对路径）；
        ///    b. 否则**原样返回** → 交由原版 Content 解析（如 "Music/Bit(Ending)" 指原版
        ///       Content/Music 下的曲子；FNA 的 SongReader.Normalize 会自动补 .ogg）。
        /// 2. 纯文件名（无分隔符）：
        ///    a. 扩展根目录下存在 → "../Extensions/扩展名/文件名"；
        ///    b. 扩展内 Music/ 下存在 → "../Extensions/扩展名/Music/文件名"；
        ///    c. Content/DLC/Music 下存在 → "DLC/Music/文件名"；
        ///    d. 都不存在 → 原样返回（原版音乐，Content/Music）。
        /// 注：返回时**保留调用方写的扩展名**（不主动剥离 .ogg）——FNA 靠 Normalize 猜扩展名，
        ///     保留显式扩展名更确定。
        /// </summary>
        public static string ResolveMusicPath(string musicPath, string extensionRoot)
        {
            if (ConfigValue.IsNone(musicPath))
                return musicPath;
            // 已经是绝对路径或已带有扩展前缀，直接返回
            if (Path.IsPathRooted(musicPath) || musicPath.StartsWith("../Extensions/"))
                return musicPath;

            // 获取真实的扩展文件夹名称
            string extFolderName;
            if (!string.IsNullOrEmpty(extensionRoot))
                extFolderName = Path.GetFileName(extensionRoot.TrimEnd('/'));
            else
                // 降级：仍尝试用 ExtensionInfo 的名称（通常不会执行）
                extFolderName = ExtensionLoader.ActiveExtensionInfo?.GetFoldersafeName();

            if (string.IsNullOrEmpty(extFolderName))
                return musicPath;

            string extBase = Path.Combine(Paths.GameRootPath, "Extensions", extFolderName).Replace('\\', '/');
            // 本地函数：检查文件是否存在（自动尝试补全 .ogg）
            bool Exists(string directory, string fileName) =>
                File.Exists(Path.Combine(directory, fileName)) ||
                File.Exists(Path.Combine(directory, fileName + ".ogg"));
            // 含路径分隔符时：**先看扩展内是否真有这个文件**
            //   · 有 → 视为扩展内相对路径，返回 "../Extensions/扩展名/路径"
            //   · 无 → 原样返回，交由原版 Content 解析（如 "Music/Bit(Ending)" 指原版结局曲；
            //          FNA 的 SongReader.Normalize 会自动补 .ogg）
            // 注意：不能再无条件当成扩展内路径 —— 那样 "Music/xxx" 写法永远无法指向原版音乐。
            if (musicPath.Contains('/') || musicPath.Contains('\\'))
            {
                string rel = musicPath.Replace('\\', '/');
                string file = Path.GetFileName(rel);
                string dir = Path.GetDirectoryName(rel)?.Replace('\\', '/');
                string probeDir = string.IsNullOrEmpty(dir)
                    ? extBase
                    : Path.Combine(extBase, dir.Replace('/', '\\'));

                if (Exists(probeDir, file))
                    return $"../Extensions/{extFolderName}/{rel}";

                return rel;
            }
            // 纯文件名：按优先级查找（保留用户写的扩展名，不主动剥离）
            if (Exists(extBase, musicPath))
                return $"../Extensions/{extFolderName}/{musicPath}";

            if (Exists(Path.Combine(extBase, "Music"), musicPath))
                return $"../Extensions/{extFolderName}/Music/{musicPath}";

            string dlcDir = Path.Combine(Paths.GameRootPath, "Content", "DLC", "Music");
            if (Exists(dlcDir, musicPath))
                return $"DLC/Music/{musicPath}";
            // 回退原版音乐
            return musicPath;
        }
    }
}