using Hacknet;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml;

namespace KernelExtensions.Locales
{
    /// <summary>
    /// KernelExtensions 多语言支持核心（机制参照 ZeroDayToolKit）：
    /// - 从扩展根目录的 <c>Locales/*.xml</c>（以及插件目录下的 <c>Locales/*.xml</c>）加载词条。
    /// - 词条使用 Hacknet 原生语言文件格式：根元素为语言代码，子元素 <c>&lt;L key="KEY"&gt;值&lt;/L&gt;</c>，
    ///   可选 <c>exact="true"</c> 表示仅整串匹配（不参与文本子串替换）。
    /// - 优先级：当前语言 &gt; en-us &gt; default；后加载的文件覆盖先加载的文件。
    /// - 提供 <see cref="Loc"/>（整串查询）、<see cref="LocFormat"/>（带参数）、<see cref="Localize"/>（{{KEY}} 语法）。
    /// </summary>
    public static class Localization
    {
        /// <summary>当前扩展词条（含内置 en-us 兜底表）。</summary>
        private static Dictionary<string, string> _terms = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>参与文本子串替换的非 exact 词条键，按长度降序（最长优先）。</summary>
        private static List<string> _bareKeys = new();

        /// <summary>exact 词条键集合（仅整串匹配）。</summary>
        private static readonly HashSet<string> _exactKeys = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>调试日志开关（与插件类的 Debug 独立，便于脱离游戏环境测试）。</summary>
        public static bool DebugLog = true;

        /// <summary>上一次成功加载词条的扩展根目录（用于语言切换时重载）。</summary>
        public static string LastLoadedRoot { get; private set; }

        /// <summary>是否在当前语言文件中找到了匹配的词条（dynamic 语言判定用）。</summary>
        public static bool FoundActiveLocaleTerms { get; private set; }

        /// <summary>是否已加载任何词条。</summary>
        public static bool HasTerms => _terms.Count > 0;

        /// <summary>内置 en-us 兜底词条表：即使扩展未放置 Locales 文件夹，模组自身文本也能以英文正常工作。</summary>
        private static readonly Dictionary<string, string> BuiltInEnglish = new(StringComparer.OrdinalIgnoreCase)
        {
            // ---- CustomTrialExe UI ----
            { "TRIAL_BEGIN_BUTTON", "BEGIN TRIAL" },
            { "TRIAL_LOCKED", "TRIAL LOCKED" },
            { "TRIAL_INITIALIZING", "INITIALIZING" },
            { "TRIAL_COMPLETE", "COMPLETE" },
            { "TRIAL_FAILED", "FAILED" },
            { "TRIAL_EXIT_BUTTON", "EXIT" },
            // ---- FakeRecoveryModule ----
            { "VM_MATCH_SUCCESS", "MATCH: FULL. Restarting in 3s..." },
            { "VM_MATCH_FAIL", "MATCH: MISMATCH" },
            { "VM_HELP_BUTTON", "HELP" },
            // ---- CrashModule 错误信息 ----
            { "VM_ERROR_BOOTLOADER", "ERROR: Critical boot error loading \"VMBootloaderTrap.dll\"" },
            { "VM_ERROR_TPM", "ERROR: Critical boot error - TPM Platform Key Verification Failure" },
            // ---- 自定义 Action 消息 ----
            { "ERR_OVERLAY_NO_COMPUTERID", "ERROR: ActivateAircraftOverlayAction requires a ComputerID attribute." },
            { "ERR_OVERLAY_COMPUTER_NOT_FOUND", "ERROR: Computer with idName '{0}' not found." },
            { "MSG_OVERLAY_ACTIVATED", "Aircraft overlay activated for {0} (idName: {1})." },
            { "MSG_OVERLAY_DEACTIVATED", "Aircraft overlay deactivated." },
            { "ERR_OVERLAY_NO_DAEMON", "ERROR: No FlightDaemon found on computer '{0}'." },
            { "ERR_AIRCRAFT_COMPUTER_NOT_FOUND", "ERROR: Computer '{0}' not found." },
            { "ERR_AIRCRAFT_NO_DAEMON", "ERROR: No FlightDaemon on computer '{0}'." },
            { "ERR_RENAME_NODE_NOT_FOUND", "RenameNode: node {0} not found." },
            { "ERR_ACTION_FILE_NOT_FOUND", "Action file not found: {0}" },
        };

        /// <summary>
        /// 查询词条：先查扩展词条，未命中则回退到内置英文兜底表，最后回退到游戏内置词条。
        /// </summary>
        public static string Loc(string key)
        {
            if (string.IsNullOrEmpty(key)) return key;
            if (_terms.TryGetValue(key, out var term)) return term;
            if (BuiltInEnglish.TryGetValue(key, out var fallback)) return fallback;
            return LocaleTerms.Loc(key);
        }

        /// <summary>
        /// 查询词条并做 string.Format 参数替换。key 缺失时按原样返回（格式化占位符保留）。
        /// </summary>
        public static string LocFormat(string key, params object[] args)
        {
            if (string.IsNullOrEmpty(key)) return key;
            string value = Loc(key);
            try { return args.Length > 0 ? string.Format(value, args) : value; }
            catch (FormatException) { return value; }
        }

        /// <summary>仅查扩展词条（供 LocaleTerms.Loc 补丁使用，避免递归）。</summary>
        internal static bool TryLoc(string key, out string value)
        {
            if (!string.IsNullOrEmpty(key) && _terms.TryGetValue(key, out value)) return true;
            value = null;
            return false;
        }

        /// <summary>
        /// 本地化一段文本（与 ZeroDayToolKit 的 localizeThis 兼容的超集）：
        /// 1. 整串词条查询（如 "TRIAL_COMPLETE" 单独作为整行）；
        /// 2. {{KEY}} 花括号语法替换，支持 \{{ 与 \}} 转义（转义结果不参与后续子串替换）；
        /// 3. 对非 exact 词条做最长优先的子串替换（可让裸键直接出现在文本中）。
        /// 未命中的 {{KEY}} 会保留为 KEY 本身（与 ZDTK 行为一致）。
        /// </summary>
        public static string Localize(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            if (_terms.Count == 0 && BuiltInEnglish.Count == 0) return text;

            // 1. 整串匹配：优先游戏词条（含 LocaleTerms.Loc 补丁解析的扩展词条），
            //    再查自身词条表（不依赖补丁也能解析 exact 词条）
            string exact = LocaleTerms.Loc(text);
            if (!text.Equals(exact)) return exact;
            if (TryLoc(text, out string own)) return own;

            // 2. {{KEY}} 花括号语法（转义/字面区用私有区哨兵保护，防止第 3 步子串替换破坏）
            text = ReplaceBracedKeys(text);

            // 3. 非 exact 词条子串替换（最长优先），哨兵字面区内的文本不参与
            if (_bareKeys.Count > 0)
                text = ReplaceBareKeys(text);

            // 还原转义哨兵
            return text.Replace(SentinelOpen, "{{").Replace(SentinelClose, "}}");
        }

        // 转义哨兵：\{{ 与 \}} 先替换为私有区字符，避免被子串替换再次处理
        private const string SentinelOpen = "\uE000";
        private const string SentinelClose = "\uE001";

        /// <summary>对非 exact 词条做最长优先的子串替换；\{{ 开启的字面区（至 \}} 或文末）原样保留。</summary>
        private static string ReplaceBareKeys(string text)
        {
            var sb = new StringBuilder(text.Length);
            bool inLiteral = false;
            int i = 0;
            while (i < text.Length)
            {
                if (inLiteral)
                {
                    // 字面区：原样复制直到关闭哨兵
                    int close = text.IndexOf(SentinelClose, i, StringComparison.Ordinal);
                    if (close < 0) { sb.Append(text, i, text.Length - i); break; }
                    sb.Append(text, i, close - i).Append(SentinelClose);
                    i = close + SentinelClose.Length;
                    inLiteral = false;
                    continue;
                }

                if (text[i] == '\uE000')
                {
                    sb.Append(SentinelOpen);
                    i += SentinelOpen.Length;
                    inLiteral = true;
                    continue;
                }

                // 找下一个可能匹配的最长键
                bool matched = false;
                foreach (string k in _bareKeys)
                {
                    if (k.Length > 0 && i + k.Length <= text.Length &&
                        string.CompareOrdinal(text, i, k, 0, k.Length) == 0)
                    {
                        if (_terms.TryGetValue(k, out string v))
                            sb.Append(v);
                        i += k.Length;
                        matched = true;
                        break;
                    }
                }
                if (!matched)
                {
                    sb.Append(text[i]);
                    i++;
                }
            }
            return sb.ToString();
        }

        /// <summary>解析 {{KEY}} 语法：\{{ 开启字面区（输出字面 {{，内容至 }} 原样），\}} 输出字面 }}，{{ 开 KEY }} 关。</summary>
        private static string ReplaceBracedKeys(string text)
        {
            var sb = new StringBuilder(text.Length);
            bool inKey = false;
            bool inLiteral = false;
            var key = new StringBuilder();

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (inLiteral)
                {
                    // 字面区：原样复制，直到 }} 关闭
                    if (c == '}' && i + 1 < text.Length && text[i + 1] == '}')
                    {
                        sb.Append(SentinelClose);
                        i++;
                        inLiteral = false;
                    }
                    else sb.Append(c);
                }
                else if (inKey)
                {
                    // 键内转义：\}} 输出字面 }} 且保持键模式（与 ZDTK 一致）
                    if (c == '\\' && i + 1 < text.Length && text[i + 1] == '}' && i + 2 < text.Length && text[i + 2] == '}')
                    {
                        key.Append("}}");
                        i += 2;
                    }
                    else if (c == '}' && i + 1 < text.Length && text[i + 1] == '}')
                    {
                        inKey = false;
                        sb.Append(Loc(key.ToString()));
                        key.Clear();
                        i++;
                    }
                    else key.Append(c);
                }
                else
                {
                    // 文本内转义：\{{ 开启字面区；\}} 输出字面 }}（以哨兵暂存）
                    if (c == '\\' && i + 1 < text.Length && text[i + 1] == '{' && i + 2 < text.Length && text[i + 2] == '{')
                    {
                        sb.Append(SentinelOpen);
                        inLiteral = true;
                        i += 2;
                    }
                    else if (c == '\\' && i + 1 < text.Length && text[i + 1] == '}' && i + 2 < text.Length && text[i + 2] == '}')
                    {
                        sb.Append(SentinelClose);
                        i += 2;
                    }
                    else if (c == '{' && i + 1 < text.Length && text[i + 1] == '{')
                    {
                        inKey = true;
                        i++;
                    }
                    else sb.Append(c);
                }
            }
            if (inKey) sb.Append("{{").Append(key); // 未闭合：按字面保留
            return sb.ToString();
        }

        /// <summary>
        /// 从扩展根目录加载词条：{extRoot}/Locales/*.xml 与插件目录下的 Locales/*.xml。
        /// 语言优先级：当前语言 &gt; en-us &gt; default；后处理的文件覆盖先处理的。
        /// </summary>
        public static void Reload(string extensionRoot)
        {
            var def = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var en = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var active = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var exact = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            bool foundActive = false;

            foreach (string folder in GetLocaleFolders(extensionRoot))
            {
                if (!Directory.Exists(folder)) continue;
                foreach (string file in Directory.GetFiles(folder, "*.xml", SearchOption.AllDirectories)
                             .OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
                {
                    foundActive |= ReadLocaleFile(file, def, en, active, exact);
                }
            }

            // 合并：default → en-us → 当前语言（后者覆盖前者）
            var merged = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var dict in new[] { def, en, active })
                foreach (var kv in dict) merged[kv.Key] = kv.Value;

            _terms = merged;
            _exactKeys.Clear();
            _exactKeys.UnionWith(exact);
            _bareKeys = merged.Keys.Where(k => !_exactKeys.Contains(k))
                                   .OrderByDescending(k => k.Length).ToList();
            LastLoadedRoot = extensionRoot;
            FoundActiveLocaleTerms = foundActive;

            if (DebugLog)
                Console.WriteLine($"[KernelExtensions] Localization: {merged.Count} terms loaded (active locale matched: {foundActive}).");
        }

        /// <summary>待扫描的 Locales 目录：扩展根目录优先，插件自身目录兜底。</summary>
        private static IEnumerable<string> GetLocaleFolders(string extensionRoot)
        {
            var folders = new List<string>();
            if (!string.IsNullOrEmpty(extensionRoot))
                folders.Add(Path.Combine(extensionRoot, "Locales"));
            try
            {
                string pluginDir = Path.GetDirectoryName(typeof(Localization).Assembly.Location);
                if (!string.IsNullOrEmpty(pluginDir))
                    folders.Add(Path.Combine(pluginDir, "Locales"));
            }
            catch { /* 忽略：某些宿主环境拿不到程序集位置 */ }
            return folders;
        }

        /// <summary>
        /// 解析单个语言文件。根元素为语言代码（或 "default"），
        /// <c>&lt;L key="KEY" [exact="true"]&gt;值&lt;/L&gt;</c> 为词条。
        /// 返回该文件是否包含当前语言的根元素。
        /// </summary>
        private static bool ReadLocaleFile(string file,
            Dictionary<string, string> def, Dictionary<string, string> en,
            Dictionary<string, string> active, HashSet<string> exact)
        {
            string activeLocale = Settings.ActiveLocale?.ToLowerInvariant() ?? "en-us";
            bool foundActive = false;

            try
            {
                using var reader = XmlReader.Create(file);
                reader.MoveToContent();
                if (reader.NodeType != XmlNodeType.Element) return false;

                string root = reader.Name.ToLowerInvariant();
                Dictionary<string, string> target;
                if (root == activeLocale) { target = active; foundActive = true; }
                else if (root == "en-us") target = en;
                else if (root == "default") target = def;
                else return false; // 未知语言的文件，跳过

                while (reader.Read())
                {
                    if (reader.NodeType != XmlNodeType.Element) continue;
                    if (reader.Name.Equals("L", StringComparison.OrdinalIgnoreCase))
                    {
                        string key = reader.GetAttribute("key");
                        if (string.IsNullOrEmpty(key)) continue;
                        bool isExact = bool.TryParse(reader.GetAttribute("exact"), out bool e) && e;
                        string value = reader.ReadElementContentAsString();
                        if (value == null) continue;
                        target[key] = value;
                        if (isExact) exact.Add(key);
                    }
                }
                return foundActive;
            }
            catch (Exception ex)
            {
                if (DebugLog)
                    Console.WriteLine($"[KernelExtensions] Localization: failed to read '{file}': {ex.Message}");
                return false;
            }
        }

        /// <summary>处理扩展语言的 "dynamic"：有当前语言词条则用当前语言，否则回退 en-us。</summary>
        public static string ResolveDynamicLanguage(string language)
        {
            if (string.IsNullOrEmpty(language)) return language;
            if (!language.Equals("dynamic", StringComparison.OrdinalIgnoreCase)) return language;
            return FoundActiveLocaleTerms ? Settings.ActiveLocale : "en-us";
        }

        /// <summary>
        /// 加载 Hacknet 全局自定义词条（游戏根目录 locales/Custom/*.xml），
        /// 与 ZeroDayToolKit 的 GlobalLocales 机制一致：直接合并进 LocaleTerms.ActiveTerms。
        /// </summary>
        public static void LoadGlobalCustomLocales()
        {
            try
            {
                string customDir = Path.Combine(Directory.GetCurrentDirectory(), "locales", "Custom");
                if (!Directory.Exists(customDir)) return;
                string activeLocale = Settings.ActiveLocale?.ToLowerInvariant() ?? "en-us";
                foreach (string file in Directory.GetFiles(customDir, "*.xml").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
                {
                    var def = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    var en = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    var active = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    var exact = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    ReadLocaleFile(file, def, en, active, exact);
                    foreach (var kv in new[] { def, en, active }.SelectMany(d => d))
                        LocaleTerms.ActiveTerms[kv.Key] = kv.Value;
                }
            }
            catch (Exception ex)
            {
                if (DebugLog)
                    Console.WriteLine($"[KernelExtensions] Localization: failed to load global custom locales: {ex.Message}");
            }
        }
    }
}
