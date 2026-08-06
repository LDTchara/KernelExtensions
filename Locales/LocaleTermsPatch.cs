using Hacknet;
using HarmonyLib;

namespace KernelExtensions.Locales
{
    /// <summary>
    /// 补丁：让扩展词条在游戏任意位置经 <see cref="LocaleTerms.Loc"/> 可解析
    /// （与 ZeroDayToolKit 的 ExtensionLocaleSupport 机制一致）。
    /// 也允许扩展作者用 Locales 词条覆盖原版词条。
    /// </summary>
    [HarmonyPatch(typeof(LocaleTerms), nameof(LocaleTerms.Loc))]
    public static class LocaleTermsPatch
    {
        public static void Postfix(string input, ref string __result)
        {
            if (Localization.TryLoc(input, out var value))
                __result = value;
        }
    }
}
