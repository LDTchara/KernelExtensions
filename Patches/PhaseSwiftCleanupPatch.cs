using HarmonyLib;
using KernelExtensions.Modules;

namespace KernelExtensions.Patches
{
    /// <summary>
    /// 扩展卸载/返回主菜单时清理 PhaseSwift 残留的静态状态。
    /// </summary>
    [HarmonyPatch(typeof(Hacknet.MainMenu), nameof(Hacknet.MainMenu.resetOS))]
    public class PhaseSwiftCleanupPatch
    {
        static void Postfix()
        {
            // 清理 PhaseSwift 运行时状态
            if (PhaseSwiftManager.IsRunning || PhaseSwiftManager.IsInitialized)
                PhaseSwiftManager.Stop("none");

            // 清理 CustomColor 预设缓存，使下次加载扩展时重新读取
            Patches.CustomColorPatch.ResetPresets();
        }
    }
}
