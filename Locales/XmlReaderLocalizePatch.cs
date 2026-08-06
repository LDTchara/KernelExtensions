using Hacknet;
using HarmonyLib;
using System;
using System.IO;
using System.Xml;

namespace KernelExtensions.Locales
{
    /// <summary>
    /// 补丁：让扩展的 XML 内容支持多语言（与 ZeroDayToolKit 的
    /// XmlReaderSettingsLocalizeExtensions 机制一致）：
    /// - 若文本含 <c>Language="dynamic"</c>，替换为当前生效语言；
    /// - 对 {{KEY}} 语法做本地化替换。
    /// 仅在模组已加载词条时生效；无词条时对输入不做任何改动。
    /// </summary>
    [HarmonyPatch(typeof(XmlReaderSettings), "CreateReader",
        typeof(TextReader), typeof(string), typeof(XmlParserContext))]
    public static class XmlReaderLocalizePatch
    {
        public static void Prefix(ref TextReader input)
        {
            if (input == null) return;
            // 无词条时不干预，避免全局扫描开销
            if (!Localization.HasTerms) return;

            string content;
            try
            {
                content = input.ReadToEnd();
                input.Close();
            }
            catch
            {
                return;
            }

            if (content.IndexOf("Language=\"dynamic\"", StringComparison.OrdinalIgnoreCase) >= 0 ||
                content.Contains("{{"))
            {
                content = content.Replace("Language=\"dynamic\"", "Language=\"" + Settings.ActiveLocale + "\"");
                content = Localization.Localize(content);
            }

            input = new StringReader(content);
        }
    }
}
