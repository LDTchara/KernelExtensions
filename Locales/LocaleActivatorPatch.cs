using Hacknet;
using Hacknet.Localization;
using HarmonyLib;

namespace KernelExtensions.Locales
{
    /// <summary>
    /// 补丁：语言切换时重载扩展词条 + 全局自定义词条；支持 "dynamic" 语言代码。
    /// </summary>
    [HarmonyPatch(typeof(LocaleActivator), nameof(LocaleActivator.ActivateLocale))]
    public static class LocaleActivatorPatch
    {
        /// <summary>"dynamic" 语言直接解析为当前生效语言（与 ZDTK LocaleActivatorSupportDynamicLocale 一致）。</summary>
        public static void Prefix(ref string localeCode)
        {
            if (localeCode != null && localeCode.Equals("dynamic", System.StringComparison.OrdinalIgnoreCase))
                localeCode = Settings.ActiveLocale;
        }

        /// <summary>语言切换完成后重载词条，使新语言立即生效。</summary>
        public static void Postfix()
        {
            Localization.Reload(Localization.LastLoadedRoot);
            Localization.LoadGlobalCustomLocales();
        }
    }
}
