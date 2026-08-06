using Hacknet.Extensions;
using HarmonyLib;

namespace KernelExtensions.Locales
{
    /// <summary>
    /// 补丁：每个扩展会话加载时扫描其 Locales/*.xml（与 ZeroDayToolKit 的
    /// ExtensionLoaderReadCustomLocale 一致），并支持扩展元数据 Language="dynamic"。
    /// </summary>
    [HarmonyPatch(typeof(ExtensionLoader), nameof(ExtensionLoader.LoadNewExtensionSession))]
    public static class ExtensionLoaderLocalePatch
    {
        public static void Prefix(ref ExtensionInfo info, object os_obj)
        {
            if (info == null) return;

            Localization.Reload(info.FolderPath);

            if (!string.IsNullOrEmpty(info.Language) &&
                info.Language.Equals("dynamic", System.StringComparison.OrdinalIgnoreCase))
            {
                string resolved = Localization.ResolveDynamicLanguage(info.Language);
                info.Language = resolved;
                if (Localization.FoundActiveLocaleTerms)
                    Hacknet.Settings.ActiveLocale = resolved;
            }
        }
    }
}
