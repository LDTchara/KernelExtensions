using BepInEx;
using Hacknet;
using Hacknet.Extensions;
using System.IO;

namespace KernelExtensions.Utility
{
    public static class MusicPathResolver
    {
        /// <summary>
        /// 将配置中的音乐字符串转换为 MusicManager.transitionToSong 能识别的路径。
        /// 规则：
        /// 1. 如果字符串包含路径分隔符（/ 或 \），视为相对路径，基于扩展根目录解析并返回 "../Extensions/扩展名/路径"。
        /// 2. 如果是纯文件名（无路径分隔符）：
        ///    a. 首先检查扩展根目录下是否存在该文件（直接拼接），若存在则返回 "../Extensions/扩展名/文件名"。
        ///    b. 检查扩展内 Music 文件夹：检测 Extensions/当前扩展名/Music/文件名.ogg 是否存在，若存在返回 "../Extensions/扩展名/Music/文件名"。
        ///    c. 否则，检查是否为 DLC 音乐：检测 Content/DLC/Music/文件名.ogg 是否存在，若存在返回 "DLC/Music/文件名"。
        ///    d. 以上都不存在，作为原版音乐返回原字符串（MusicManager 会从 Content/Music/ 加载）。
        /// 就很...兜底。比起原版的逻辑多了个不用多填一个Music/的逻辑
        /// </summary>
        public static string ResolveMusicPath(string musicPath, string extensionRoot)
        {
            if (string.IsNullOrEmpty(musicPath))
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
            // 去除 .ogg 扩展名（MusicManager 不需要）
            string StripOgg(string name) =>
                name.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase)
                    ? name.Substring(0, name.Length - 4)
                    : name;
            // 若包含路径分隔符 → 视为相对扩展根目录的路径
            if (musicPath.Contains('/') || musicPath.Contains('\\'))
                return $"../Extensions/{extFolderName}/{StripOgg(musicPath.Replace('\\', '/'))}";
            // 纯文件名：按优先级查找
            if (Exists(extBase, musicPath))
                return $"../Extensions/{extFolderName}/{StripOgg(musicPath)}";

            if (Exists(Path.Combine(extBase, "Music"), musicPath))
                return $"../Extensions/{extFolderName}/Music/{StripOgg(musicPath)}";

            string dlcDir = Path.Combine(Paths.GameRootPath, "Content", "DLC", "Music");
            if (Exists(dlcDir, musicPath))
                return $"DLC/Music/{StripOgg(musicPath)}";
            // 回退原版音乐
            return musicPath;
        }
    }
}