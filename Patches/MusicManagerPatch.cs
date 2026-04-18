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
        /// 搜索顺序：
        /// 1. 如果字符串本身是存在的文件路径（绝对或相对），直接使用。
        /// 2. 如果字符串不含路径分隔符，尝试在扩展根目录下的 Music 文件夹中查找 文件名.ogg。
        /// 3. 如果字符串是相对于游戏根目录的路径（如 "Music/my_song.ogg"），尝试从游戏根目录加载。
        /// </summary>
        private static Song LoadExternalSong(string songname)
        {
            if (string.IsNullOrEmpty(songname))
                return null;

            string fullPath = null;

            // 1. 直接作为文件路径检查
            if (File.Exists(songname))
                fullPath = songname;
            // 2. 相对于游戏根目录的路径
            else if (songname.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase))
            {
                string gamePath = Path.Combine(Paths.GameRootPath, songname);
                if (File.Exists(gamePath))
                    fullPath = gamePath;
            }
            // 3. 纯文件名（不含路径），尝试在扩展目录的 Music 子文件夹中查找
            else if (!songname.Contains("/") && !songname.Contains("\\"))
            {
                var extInfo = ExtensionLoader.ActiveExtensionInfo;
                if (extInfo != null && !string.IsNullOrEmpty(extInfo.FolderPath))
                {
                    string candidate = Path.Combine(extInfo.FolderPath, "Music", songname + ".ogg");
                    if (File.Exists(candidate))
                        fullPath = candidate;
                }
            }

            if (fullPath != null)
            {
                try
                {
                    // 使用 Song.FromUri 加载 .ogg 文件（XNA/FNA 支持）
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