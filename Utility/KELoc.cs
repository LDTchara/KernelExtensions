using Hacknet;
using Hacknet.Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;

namespace KernelExtensions.Utility
{
    /// <summary>
    /// KE 内置文本本地化（9.27）。
    /// 语言文件 KE-Locales.xml 内嵌于 dll（EmbeddedResource）；首次运行导出到扩展根目录
    /// （文件不存在才导出，用户可自由编辑，后续不覆盖）；外部文件存在时优先加载，
    /// 删除后回退 dll 内嵌副本。
    /// 回退链：当前语言（精确 → 前缀）→ en-us → 调用方 fallback。
    /// 语言跟随 Settings.ActiveLocale（原版 10 语言集合）。
    /// </summary>
    public static class KELoc
    {
        private const string EmbeddedResourceName = "KernelExtensions.Locales.KE-Locales.xml";
        private const string ExternalFileName = "KE-Locales.xml";

        private static Dictionary<string, Dictionary<string, string>> _langs = new(StringComparer.OrdinalIgnoreCase);
        private static bool _loaded = false;

        /// <summary>（重新）加载语言表：内嵌为基础表 + 外部文件覆盖/新增。每次调用可热重载。</summary>
        public static void Load()
        {
            _langs = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            _loaded = false;

            string external = null;
            var extInfo = ExtensionLoader.ActiveExtensionInfo;
            if (extInfo != null)
                external = Path.Combine(extInfo.FolderPath.Replace('\\', '/'), ExternalFileName);

            // 内嵌为基础表：保证 KE 更新后旧语言文件缺的 key 也有默认翻译
            string embeddedXml = ReadEmbedded();
            if (embeddedXml != null)
            {
                // 首次导出（供用户查看/编辑；失败静默，内嵌仍可用）
                if (external != null && !File.Exists(external))
                {
                    try { File.WriteAllText(external, embeddedXml); }
                    catch (Exception ex) { KELog.Warn($"[KELoc] export {ExternalFileName} failed: {ex.Message}"); }
                }
                try { ParseInto(embeddedXml, overwrite: false); }
                catch (Exception ex) { KELog.Warn($"[KELoc] embedded parse failed: {ex.Message}"); }
            }

            // 外部覆盖：用户改过的 key 保持覆盖；KE 新增 key 由内嵌补齐
            if (external != null && File.Exists(external))
            {
                try { ParseInto(File.ReadAllText(external), overwrite: true); }
                catch (Exception ex) { KELog.Warn($"[KELoc] read {ExternalFileName} failed: {ex.Message}"); }
            }

            _loaded = _langs.Count > 0;
            if (!_loaded)
                KELog.Error("[KELoc] no locale data available (embedded resource missing?)");
        }

        private static void ParseInto(string xmlText, bool overwrite)
        {
            var doc = XDocument.Parse(xmlText);
            foreach (var langEl in doc.Root?.Elements("Language") ?? Enumerable.Empty<XElement>())
            {
                string langName = (string)langEl.Attribute("Name");
                if (string.IsNullOrWhiteSpace(langName)) continue;
                var lang = langName.ToLowerInvariant();
                if (!_langs.TryGetValue(lang, out var terms))
                {
                    terms = new Dictionary<string, string>(StringComparer.Ordinal);
                    _langs[lang] = terms;
                }
                foreach (var term in langEl.Elements("Term"))
                {
                    string k = (string)term.Attribute("Key");
                    string v = (string)term.Attribute("Value");
                    if (!string.IsNullOrEmpty(k) && v != null && (overwrite || !terms.ContainsKey(k)))
                        terms[k] = v;
                }
            }
        }

        private static string ReadEmbedded()
        {
            try
            {
                var asm = Assembly.GetExecutingAssembly();
                using var s = asm.GetManifestResourceStream(EmbeddedResourceName);
                if (s == null) return null;
                using var r = new StreamReader(s, System.Text.Encoding.UTF8);
                return r.ReadToEnd();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>取当前语言的词条；回退链：当前语言（精确 → 前缀）→ en-us → fallback。</summary>
        public static string Loc(string key, string fallback)
        {
            if (!_loaded) Load();
            string lang = Settings.ActiveLocale?.ToLowerInvariant() ?? "en-us";
            if (TryGet(lang, key, out var v)) return v;
            int dash = lang.IndexOf('-');
            if (dash > 0 && TryGet(lang.Substring(0, dash), key, out v)) return v;
            if (!lang.StartsWith("en") && TryGet("en-us", key, out v)) return v;
            return fallback;
        }

        /// <summary>取词条并格式化占位符（{0} 等，互斥提示等动态文案用）。</summary>
        public static string Format(string key, string fallback, params object[] args)
        {
            string t = Loc(key, fallback);
            try { return string.Format(t, args); }
            catch (FormatException) { return t; }
        }

        private static bool TryGet(string lang, string key, out string value)
        {
            value = null;
            return _langs.TryGetValue(lang, out var terms) && terms.TryGetValue(key, out value);
        }
    }
}
