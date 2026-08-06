using Hacknet;
using HarmonyLib;

namespace KernelExtensions.Locales
{
    /// <summary>
    /// 补丁：对终端输出（OS.write / OS.writeSingle）做本地化处理，
    /// 支持 {{KEY}} 语法与非 exact 词条的子串替换（与 ZeroDayToolKit 一致）。
    /// </summary>
    [HarmonyPatch(typeof(OS), nameof(OS.write))]
    public static class OSWritePatch
    {
        public static void Prefix(ref string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            text = Localization.Localize(text);
        }
    }

    /// <summary>OS.writeSingle 的本地化补丁（write 的无换行变体）。</summary>
    [HarmonyPatch(typeof(OS), nameof(OS.writeSingle))]
    public static class OSWriteSinglePatch
    {
        public static void Prefix(ref string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            text = Localization.Localize(text);
        }
    }
}
