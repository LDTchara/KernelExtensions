using BepInEx;
using Hacknet;
using Hacknet.Extensions;
using HarmonyLib;
using Microsoft.Xna.Framework.Media;
using System;
using System.IO;

namespace KernelExtensions.Patches
{
    /// <summary>
    /// Harmony 补丁：使 MusicManager 支持从扩展文件夹加载自定义 .ogg 音乐文件，
    /// 同时完全保留原版的淡入淡出效果（通过补丁 loadSong 和 loadAsCurrentSongUnsafe 实现）。
    /// </summary>
    [HarmonyPatch]
    public static class MusicManagerPatch
    {
        /// <summary>
        /// 补丁 loadSong 方法（私有静态方法，在后台线程中调用，负责实际加载 Song）。
        /// 优先尝试加载外部 .ogg 文件，如果失败则回退到原 ContentManager 加载。
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(MusicManager), "loadSong")]
        public static bool LoadSongPrefix()
        {
            // 获取 nextSongName 字段的值（原方法会从静态字段中读取要加载的歌曲名）
            string songName = (string)AccessTools.Field(typeof(MusicManager), "nextSongName").GetValue(null);
            if (string.IsNullOrEmpty(songName))
                return true; // 没有要加载的歌曲，继续原方法

            // 尝试加载外部 .ogg 文件
            Song externalSong = LoadExternalSong(songName);
            if (externalSong != null)
            {
                // 将加载的歌曲存入 nextSong 字段（原方法后续会使用）
                AccessTools.Field(typeof(MusicManager), "nextSong").SetValue(null, externalSong);
                // 同时存入缓存字典（loadedSongs）
                var loadedSongs = (System.Collections.Generic.Dictionary<string, Song>)AccessTools.Field(typeof(MusicManager), "loadedSongs").GetValue(null);
                if (!loadedSongs.ContainsKey(songName))
                    loadedSongs.Add(songName, externalSong);
                // 跳过原方法（因为已经完成加载）
                return false;
            }
            // 回退到原方法（使用 ContentManager 加载）
            return true;
        }

        /// <summary>
        /// 补丁 loadAsCurrentSongUnsafe 方法，支持外部文件。
        /// 注意：此方法原版没有淡入淡出，直接替换当前歌曲。
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(MusicManager), nameof(MusicManager.loadAsCurrentSongUnsafe))]
        public static bool LoadAsCurrentSongUnsafePrefix(string songname)
        {
            Song externalSong = LoadExternalSong(songname);
            if (externalSong != null)
            {
                // 模仿原方法的行为
                AccessTools.Field(typeof(MusicManager), "curentSong").SetValue(null, externalSong);
                AccessTools.Field(typeof(MusicManager), "isPlaying").SetValue(null, false);
                AccessTools.Field(typeof(MusicManager), "currentSongName").SetValue(null, songname);
                return false; // 跳过原方法
            }
            return true; // 回退到原方法
        }

        /// <summary>
        /// 尝试将字符串解析为外部 .ogg 文件路径，并加载为 Song 对象。
        /// 检测顺序（优先级从高到低）：
        /// 1. 将字符串作为相对于扩展根目录的路径（例如 "Music/1/2.ogg"），如果文件存在则加载。
        /// 2. 如果字符串本身是存在的绝对路径或相对于游戏根目录的路径，则加载。
        /// 3. 如果字符串是不含路径分隔符的纯文件名，则在扩展根目录下的 Music 文件夹中查找 文件名.ogg。
        /// </summary>
        private static Song LoadExternalSong(string songname)
        {
            if (string.IsNullOrEmpty(songname))
                return null;

            string fullPath = null;

            // 获取扩展根目录（如果当前在扩展模式下运行）
            string extRoot = ExtensionLoader.ActiveExtensionInfo?.FolderPath;

            // 1. 优先尝试：将字符串作为相对于扩展根目录的路径
            if (!string.IsNullOrEmpty(extRoot))
            {
                // 支持带或不带 .ogg 扩展名
                string candidate = Path.Combine(extRoot, songname);
                if (File.Exists(candidate))
                    fullPath = candidate;
                else if (!songname.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase) && File.Exists(candidate + ".ogg"))
                    fullPath = candidate + ".ogg";
            }

            // 2. 如果未找到，尝试作为绝对路径或相对于游戏根目录的路径
            if (fullPath == null)
            {
                if (File.Exists(songname))
                    fullPath = songname;
                else if (songname.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase))
                {
                    string gamePath = Path.Combine(Paths.GameRootPath, songname);
                    if (File.Exists(gamePath))
                        fullPath = gamePath;
                }
            }

            // 3. 如果是纯文件名（不含路径分隔符），在扩展目录的 Music 子文件夹中查找
            if (fullPath == null && !songname.Contains("/") && !songname.Contains("\\") && !string.IsNullOrEmpty(extRoot))
            {
                string candidate = Path.Combine(extRoot, "Music", songname + ".ogg");
                if (File.Exists(candidate))
                    fullPath = candidate;
            }

            if (fullPath != null)
            {
                try
                {
                    return Song.FromUri(songname, new Uri(fullPath));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[KernelExtensions] Failed to load external song '{fullPath}': {ex.Message}");
                }
            }
            return null;
        }
    }
}