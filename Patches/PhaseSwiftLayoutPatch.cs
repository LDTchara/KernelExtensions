using Hacknet;
using HarmonyLib;

namespace KernelExtensions.Patches
{
    /// <summary>
    /// 拦截 ThemeManager.switchThemeLayout，允许 PhaseSwift 控制主题切换时
    /// 是否改变面板布局（terminal、netMap、display、ram 的 Bounds）。
    ///
    /// 当 SkipLayoutChange = true 时，switchThemeLayout 被跳过，
    /// 只有颜色和背景会变化，面板位置保持不变。
    /// </summary>
    [HarmonyPatch(typeof(ThemeManager), "switchThemeLayout")]
    public static class PhaseSwiftLayoutPatch
    {
        /// <summary>
        /// 设为 true 时跳过面板布局切换。
        /// PhaseSwift 的 ChangeLayout=false 时使用，
        /// 切换完成后需由调用方恢复为 false。
        /// </summary>
        internal static bool SkipLayoutChange = false;

        [HarmonyPrefix]
        static bool Prefix()
        {
            return !SkipLayoutChange;
        }
    }
}
