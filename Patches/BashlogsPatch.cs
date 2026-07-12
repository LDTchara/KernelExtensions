using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using Hacknet;
using Microsoft.Xna.Framework.Content;
using KernelExtensions.Utility;

namespace KernelExtensions.Patches;

/// <summary>
/// 完全替换 FileEntry.init()：OverrrideOriginal=true 时只用自定义，
/// false 时原版 + 自定义。
/// </summary>
[HarmonyPatch(typeof(FileEntry), nameof(FileEntry.init))]
internal static class CN_IRCLogInjector
{
    private static bool OverrideOriginal => KEConfigLoader.SkipVanillaIRCLogs;

    static bool Prefix(ContentManager content)
    {
        // 确保配置在 FileEntry.init 处理前已加载
        KEConfigLoader.Load();
        string extRoot = "";  // 稍后填

        // 1. 创建列表
        FileEntry.filenames = new List<string>(128);
        FileEntry.fileData = new List<string>(128);

        // 2. 原版文件（Content/files/）
        if (!OverrideOriginal)
        {
            DirectoryInfo dir = new DirectoryInfo(Path.Combine(content.RootDirectory, "files"));
            if (dir.Exists)
            {
                foreach (var f in dir.GetFiles("*.*"))
                {
                    FileEntry.filenames.Add(Path.GetFileNameWithoutExtension(f.Name));
                    FileEntry.fileData.Add(Utils.readEntireFile($"Content/files/{f.Name}"));
                }
            }
        }

        // 3. 原版 BashLogs
        if (!OverrideOriginal)
        {
            string bashFile = Settings.EducationSafeBuild
                ? "Content/BashLogs_StudentSafe.txt"
                : "Content/BashLogs.txt";
            string text = Utils.readEntireFile(bashFile);
            ParseAndAdd(text, "http://Bash.org", true);
        }

        // 4. 自定义日志（ExtensionLoader 可能尚未就绪，用安全访问）
        try
        {
            var extInfo = Hacknet.Extensions.ExtensionLoader.ActiveExtensionInfo;
            if (extInfo != null)
            {
                extRoot = extInfo.FolderPath.Replace('\\', '/');
                string path = Path.Combine(extRoot, "CustomIRCLogs.txt");
                if (File.Exists(path))
                {
                    string customText = File.ReadAllText(path);
                    ParseAndAdd(customText, extInfo.Name, false);
                    KELog.Info($"[IRCLogsPatch] Prefix injected from {path}");
                }
            }
        }
        catch (Exception ex)
        {
            KELog.Warn($"[IRCLogsPatch] Prefix error: {ex.Message}");
        }

        // 跳过原版 init()
        return false;
    }

    private static void ParseAndAdd(string text, string source, bool purify)
    {
        string[] entries = text.Split(new[] { "\n#" }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var entry in entries)
        {
            string clean = entry.TrimStart('#', '\r');
            int idx = clean.IndexOfAny(new[] { '\r', '\n' });
            if (idx < 0) continue;

            string topic = clean.Substring(0, idx).Trim();
            string data = clean.Substring(idx).TrimStart('\r', '\n', ' ').Replace("\n ", "\n");

            if (string.IsNullOrEmpty(topic) || string.IsNullOrEmpty(data))
                continue;

            // 支持 | 分隔符自定义来源。不填 | 时回退到扩展名
            string displaySource = source;
            int pipeIdx = topic.IndexOf('|');
            if (pipeIdx >= 0)
            {
                displaySource = topic.Substring(pipeIdx + 1).Trim();
                topic = topic.Substring(0, pipeIdx);
            }

            if (purify)
                data = FileSanitiser.purifyStringForDisplay(data);

            string filename = "IRC_Log:" + topic.Replace("- [X]", "").Replace(" ", "");
            FileEntry.filenames.Add(filename);
            FileEntry.fileData.Add(data + $"\n\nArchived Via : {displaySource}");
        }
    }
}
