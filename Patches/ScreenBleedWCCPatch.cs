using Hacknet;
using Hacknet.Effects;
using HarmonyLib;
using KernelExtensions.Modules;
using Microsoft.Xna.Framework;

namespace KernelExtensions.Patches
{
    /// <summary>
    /// ScreenBleedWCC 的 Harmony 补丁。
    /// 通过 PatchAll 自动注册，驱动 WCC 效果每帧更新和取消兼容。
    /// </summary>
    [HarmonyPatch]
    public static class ScreenBleedWCCPatch
    {
        /// <summary>
        /// OS.Update Postfix：每帧驱动 WCC ScreenBleed 计时和渲染。
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(OS), "Update")]
        public static void OnOSUpdate(OS __instance, GameTime gameTime)
        {
            ScreenBleedWCCManager.Update(__instance, (float)gameTime.ElapsedGameTime.TotalSeconds);
        }

        /// <summary>
        /// ActiveEffectsUpdater.CancelScreenBleedEffect Postfix：
        /// 原版 CancelScreenBleedEffect Action 触发时同步停止 WCC 效果。
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(ActiveEffectsUpdater), "CancelScreenBleedEffect")]
        public static void OnCancelScreenBleed(ActiveEffectsUpdater __instance)
        {
            ScreenBleedWCCManager.OnCancelScreenBleed(__instance);
        }
    }
}
