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
            if (PhaseSwiftManager.IsRunning || PhaseSwiftManager.IsInitialized)
            {
                PhaseSwiftManager.Stop("none");
            }
        }
    }
}
