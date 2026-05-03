using System;
using Hacknet;
using Hacknet.Gui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using HarmonyLib;
using Hacknet.Daemons.Helpers;
using KernelExtensions.AirCraft.Actions;

namespace KernelExtensions.AirCraft.Patches
{
    [HarmonyPatch]
    public static class OverlayPatches
    {
        // ========== 在 OS.drawModules 末尾绘制高度计 ==========
        [HarmonyPostfix]
        [HarmonyPatch(typeof(OS), "drawModules")]
        static void Postfix_DrawModules(OS __instance, GameTime gameTime)
        {
            if (!GlobalAircraftOverlayManager.IsOverlayActive ||
                GlobalAircraftOverlayManager.CurrentFlightDaemon == null)
                return;

            var fd = GlobalAircraftOverlayManager.CurrentFlightDaemon;
            SpriteBatch sb = GuiData.spriteBatch;

            // 计算出覆盖层矩形：从状态栏之下开始，到屏幕底部
            int topOffset = OS.TOP_BAR_HEIGHT + Module.PANEL_HEIGHT;
            Rectangle dest = new Rectangle(
                __instance.fullscreen.X,
                __instance.fullscreen.Y + topOffset,
                __instance.fullscreen.Width,
                __instance.fullscreen.Height - topOffset
            );

            // 绘制高度计（AircraftAltitudeIndicator 是 Hacknet 原生类）
            AircraftAltitudeIndicator.RenderAltitudeIndicator(
                dest,
                sb,
                (int)(fd.CurrentAltitude + 0.5),
                fd.IsInCriticalDescent(),
                AircraftAltitudeIndicator.GetFlashRateFromTimer(__instance.timer)
            );
        }

        // ========== 可选：在 OS.Update 中强制更新飞行数据（如果未订阅则手动更新） ==========
        [HarmonyPostfix]
        [HarmonyPatch(typeof(OS), "Update")]
        static void Postfix_Update(OS __instance, GameTime gameTime)
        {
            if (!GlobalAircraftOverlayManager.IsOverlayActive ||
                GlobalAircraftOverlayManager.CurrentFlightDaemon == null)
                return;

            var fd = GlobalAircraftOverlayManager.CurrentFlightDaemon;
            // 如果 FlightDaemon 已经通过 StartUpdating 订阅了 os.UpdateSubscriptions，
            // 那么这段代码不会重复更新。但为了保险，可以强制调用一次更新。
            // 注意：fd.Update 是 private 方法，我们可以通过反射或公开一个 PublicUpdate 方法。
            // 这里假设你在 FlightDaemon 中增加了一个 public void PublicUpdate(float t) 方法。
            // 如果没有，请忽略此补丁，或修改 FlightDaemon 使其 Update 成为 internal/public。
        }
    }
}
