using Hacknet;
using HarmonyLib;

namespace KernelExtensions.Patches
{
    [HarmonyPatch(typeof(ThemeManager), "switchThemeLayout")]
    public static class PhaseSwiftLayoutPatch
    {
        internal static bool SkipLayoutChange = false;

        [HarmonyPrefix]
        static bool Prefix()
        {
            return !SkipLayoutChange;
        }
    }

    [HarmonyPatch(typeof(OS), "Update")]
    public static class PhaseSwiftLayoutResetPatch
    {
        /// <summary>
        /// 每次 OS Update 第一帧强制重置 SkipLayoutChange = false，
        /// 确保跨会话残留的 true 不会导致新游戏开局布局丢失。
        /// 重置后不再执行。
        /// 感觉像是强制扳回来，目前也没弄明白为什么重进后主题会变仅终端，之后再想
        /// </summary>
        internal static bool _resetDone = false;

        [HarmonyPrefix]
        static void Prefix()
        {
            if (!_resetDone)
            {
                _resetDone = true;
                PhaseSwiftLayoutPatch.SkipLayoutChange = false;
            }
        }

        public static void Reset()
        {
            _resetDone = false;
        }
    }
}
