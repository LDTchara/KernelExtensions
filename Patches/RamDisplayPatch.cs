using Hacknet;
using HarmonyLib;
using KernelExtensions.Managers;
using KernelExtensions.Utilities;
using System.Reflection;

namespace KernelExtensions.Patches
{
    /// <summary>
    /// 9.12：反射 patch 原版 <c>RamModule.Update</c>，改写 <c>"USED RAM: x / y mb"</c> 文字。
    /// <para>
    /// <c>RamModule</c> 是 <c>internal</c>，KE 无法编译期引用，故运行时用
    /// <c>AccessTools.TypeByName</c> + <c>harmony.Patch(Postfix)</c>
    /// （与 <c>PorthackAutoPatch</c> 同一套路，由主入口手动调用）。
    /// </para>
    /// <para>
    /// Postfix 读出 private 字段 <c>os</c> / <c>infoString</c>，按
    /// <see cref="RamDisplayManager"/> 的倍率与单位重写 <c>infoString</c>。
    /// <b>只改文字</b> —— 条的长度（在 <c>Draw</c> 里按 <c>ramAvaliable/totalRam</c> 计算）与其它绘制一律不碰。
    /// </para>
    /// </summary>
    internal static class RamDisplayPatch
    {
        private static FieldInfo osField;
        private static FieldInfo infoStringField;

        public static void ApplyPatch(Harmony harmony)
        {
            try
            {
                var type = AccessTools.TypeByName("Hacknet.RamModule");
                if (type == null)
                {
                    KELog.Warn("[RamDisplay] Hacknet.RamModule type not found");
                    return;
                }

                var update = AccessTools.Method(type, "Update");
                if (update == null)
                {
                    KELog.Warn("[RamDisplay] RamModule.Update not found");
                    return;
                }

                osField = AccessTools.Field(type, "os");
                infoStringField = AccessTools.Field(type, "infoString");
                if (osField == null || infoStringField == null)
                {
                    KELog.Warn("[RamDisplay] RamModule fields (os / infoString) not found");
                    return;
                }

                harmony.Patch(update, postfix: new HarmonyMethod(
                    AccessTools.Method(typeof(RamDisplayPatch), nameof(UpdatePostfix))));

                KELog.Info("[RamDisplay] RamModule.Update patched.");
            }
            catch (Exception ex)
            {
                KELog.Warn($"[RamDisplay] patch failed: {ex.Message}");
            }
        }

        private static void UpdatePostfix(object __instance)
        {
            bool unchanged = Math.Abs(RamDisplayManager.Multiplier - RamDisplayManager.DefaultMultiplier) < 0.0001f
                             && RamDisplayManager.Unit == RamDisplayManager.DefaultUnit;
            if (unchanged) return;   // 没人改过：不动原版文字，省掉一次重构

            try
            {
                if (!(osField.GetValue(__instance) is OS os)) return;

                long used = os.totalRam - os.ramAvaliable;
                long total = os.totalRam;
                string unit = RamDisplayManager.Unit;

                infoStringField.SetValue(__instance,
                    "USED RAM: " + RamDisplayManager.Scale(used) + unit
                    + " / " + RamDisplayManager.Scale(total) + unit);
            }
            catch (Exception ex)
            {
                KELog.Warn($"[RamDisplay] UpdatePostfix failed: {ex.Message}");
            }
        }
    }
}
