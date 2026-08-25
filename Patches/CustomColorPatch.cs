using Hacknet;
using HarmonyLib;
using KernelExtensions.Managers;

namespace KernelExtensions.Patches
{
    /// <summary>
    /// 自定义动态颜色系统（Patch 部分）——仅挂载 ThemeManager.Update 驱动
    /// CustomColorManager.OnThemeUpdate；解析/注册表/预设逻辑见 Managers/CustomColorManager。
    /// </summary>
    [HarmonyPatch]
    public static class CustomColorPatch
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(ThemeManager), nameof(ThemeManager.Update))]
        static void ThemeUpdatePrefix()
        {
            CustomColorManager.OnThemeUpdate();
        }
    }
}
