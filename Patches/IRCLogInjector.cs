using HarmonyLib;
using Hacknet;
using Microsoft.Xna.Framework.Content;
using KernelExtensions.Configs;
using KernelExtensions.Utilities;

namespace KernelExtensions.Patches;

/// <summary>
/// 完全替换 FileEntry.init()：OverrrideOriginal=true 时只用自定义，
/// false 时原版 + 自定义。
///
/// <para>
/// 自定义日志的**入口可配** —— 列表来自 KE-Config.xml 的
/// <c>&lt;CustomIRCLogs&gt;&lt;File&gt;…&lt;/File&gt;&lt;/CustomIRCLogs&gt;</c>（可多文件、路径自定义）；
/// 一个都没配时回退到扩展根下的 <c>CustomIRCLogs.txt</c>。
/// </para>
/// </summary>
[HarmonyPatch(typeof(FileEntry), nameof(FileEntry.init))]
internal static class IRCLogInjector
{
    /// <summary>未配置 &lt;CustomIRCLogs&gt; 时回退使用的默认文件名（相对扩展根）。</summary>
    private const string DefaultLogFileName = "CustomIRCLogs.txt";

    private static bool OverrideOriginal => ConfigLoader.SkipVanillaIRCLogs;

    static bool Prefix(ContentManager content)
    {
        // 确保配置在 FileEntry.init 处理前已加载
        ConfigLoader.Load();

        // 1. 创建列表
        FileEntry.filenames = new List<string>(128);
        FileEntry.fileData = new List<string>(128);

        void LoadContentFiles()
        {
            DirectoryInfo dir = new DirectoryInfo(Path.Combine(content.RootDirectory, "files"));
            if (!dir.Exists) return;
            foreach (var f in dir.GetFiles("*.*"))
            {
                FileEntry.filenames.Add(Path.GetFileNameWithoutExtension(f.Name));
                FileEntry.fileData.Add(Utils.readEntireFile($"Content/files/{f.Name}"));
            }
        }

        void LoadVanillaBashLogs()
        {
            string bashFile = Settings.EducationSafeBuild
                ? "Content/BashLogs_StudentSafe.txt"
                : "Content/BashLogs.txt";
            ParseAndAdd(Utils.readEntireFile(bashFile), "http://Bash.org", true);
        }

        // 2/3. 原版文件 + 原版 BashLogs（SkipVanillaIRCLogs=true 时跳过）
        if (!OverrideOriginal)
        {
            LoadContentFiles();
            LoadVanillaBashLogs();
        }

        // 4. 自定义日志（ExtensionLoader 可能尚未就绪，用安全访问）
        try
        {
            var extInfo = Hacknet.Extensions.ExtensionLoader.ActiveExtensionInfo;
            if (extInfo != null)
            {
                // 入口可配：KE-Config.xml 的 <CustomIRCLogs><File>…</File></CustomIRCLogs>
                // （路径相对扩展根，解析与越界校验已在 ConfigLoader 里做过）。
                // 一个都没配时回退到默认文件名 CustomIRCLogs.txt —— 未写 KE-Config.xml
                // 的扩展与旧版写法因此照旧可用。
                // 一个都没配 = 用默认文件名回退。这种「回退」失败**不是作者的错**
                // （绝大多数扩展根本没有 CustomIRCLogs.txt，也不需要有），
                // 所以只记 Debug；只有作者**显式配了** <CustomIRCLogs> 却找不到文件才 Warn。
                bool usingFallback = ConfigLoader.CustomIRCLogFiles.Count == 0;
                List<string> files = usingFallback
                    ? new List<string> { DefaultLogFileName }
                    : new List<string>(ConfigLoader.CustomIRCLogFiles);

                foreach (string rel in files)
                {
                    string path = KEPath.ResolveInsideExtension(rel);
                    if (path == null)
                    {
                        KELog.WarnT($"[IRCLogsPatch] log path escapes the extension folder, skipped: '{rel}'");
                        continue;
                    }
                    if (!File.Exists(path))
                    {
                        // 回退默认名缺失 = 绝大多数扩展的常态（它们根本不需要 IRC 日志）
                        // → 只记 Debug，绝不能刷终端；显式配置的才 Warn。
                        if (usingFallback)
                            KELog.Debug($"[IRCLogsPatch] default IRC log absent, skipped: {path}");
                        else
                            KELog.WarnT($"[IRCLogsPatch] log file not found, skipped: {path}");
                        continue;
                    }

                    // ⚠️ 内部解析逻辑不动 —— 它已通过实测（见 TestSuite IRC 段）。
                    ParseAndAdd(File.ReadAllText(path), extInfo.Name, false);
                    KELog.Info($"[IRCLogsPatch] injected from {path}");
                }
            }
        }
        catch (Exception ex)
        {
            KELog.Warn($"[IRCLogsPatch] Prefix error: {ex.Message}");
        }

        // ⚠️ 兜底：列表为空时原版 FileEntry() 会 Random.Next(0, Count-1) 直接崩
        // （典型：SkipVanillaIRCLogs=true 且既没配 <CustomIRCLogs>、也没有 CustomIRCLogs.txt）。
        // 此时强制回退原版日志并告警——空列表是"扩展起不来"，比"没有 IRC 日志"严重得多。
        if (FileEntry.filenames.Count == 0)
        {
            KELog.WarnT("[IRCLogsPatch] no IRC logs available (vanilla skipped and no custom logs found); "
                + "falling back to vanilla logs to avoid an empty FileEntry list crash.");
            try
            {
                LoadContentFiles();
                LoadVanillaBashLogs();
            }
            catch (Exception ex)
            {
                KELog.Warn($"[IRCLogsPatch] vanilla fallback failed: {ex.Message}");
            }

            // 最后一道保险：连原版日志都读不到时，塞一个占位条目，绝不让列表为空。
            if (FileEntry.filenames.Count == 0)
            {
                KELog.ErrorT("[IRCLogsPatch] still no IRC logs after vanilla fallback; adding a placeholder entry "
                    + "to avoid a crash in FileEntry().");
                FileEntry.filenames.Add("SYSLOG");
                FileEntry.fileData.Add("System log entry placeholder (no IRC logs were available).");
            }
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
