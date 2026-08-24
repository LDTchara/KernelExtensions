using System;
using System.Reflection;
using Hacknet;
using HarmonyLib;
using KernelExtensions.Utility;
using PorthackHeartDaemon = KernelExtensions.Daemons.PorthackHeartDaemon;

namespace KernelExtensions.Patches
{
    /// <summary>
    /// AutoOnPorthack 支持：反射 patch 原版 PortHackExe.Update。
    ///
    /// 原版触发链（PortHackExe.Update，progress&gt;0.5 时 typeof(PorthackHeartDaemon) 检测
    /// 目标 → BreakHeart）；PortHackExe 与 ExeModule 均为 internal，KE 无法编译期引用，
    /// 故运行时反射：AccessTools.TypeByName + AccessTools.Method + harmony.Patch(Postfix)。
    ///
    /// Postfix 逻辑：目标含 AutoOnPorthack=true 的 KE PorthackHeartDaemon 且破解进度&gt;50%
    /// → BreakHeart()（daemon 内部 heartbreakFinished 防重复；porthack 破解过程要求保持
    /// 连接，故不再额外校验 os.connectedComp，对齐原版最终行为）。
    /// </summary>
    internal static class PorthackAutoPatch
    {
        private static FieldInfo targetField;
        private static FieldInfo progressField;
        private static FieldInfo targetingField;   // IsTargetingPorthackHeart（heart 特殊状态标志）
        private static FieldInfo cubeSeqField;     // cubeSeq（破解界面立方体序列）

        public static void ApplyPatch(Harmony harmony)
        {
            try
            {
                var type = AccessTools.TypeByName("Hacknet.PortHackExe");
                if (type == null) { KELog.Warn("[PorthackAuto] Hacknet.PortHackExe type not found"); return; }

                var update = AccessTools.Method(type, "Update");
                if (update == null) { KELog.Warn("[PorthackAuto] PortHackExe.Update not found"); return; }

                targetField = AccessTools.Field(type, "target");
                progressField = AccessTools.Field(type, "progress");
                targetingField = AccessTools.Field(type, "IsTargetingPorthackHeart");
                cubeSeqField = AccessTools.Field(type, "cubeSeq");

                harmony.Patch(update, postfix: new HarmonyMethod(
                    typeof(PorthackAutoPatch).GetMethod(nameof(Postfix), BindingFlags.Static | BindingFlags.NonPublic)));
                KELog.Info("[PorthackAuto] PortHackExe.Update postfix patched");
            }
            catch (Exception ex)
            {
                KELog.Warn("[PorthackAuto] patch failed: " + ex.Message);
            }
        }

        private static void Postfix(object __instance)
        {
            try
            {
                if (targetField == null || __instance == null) return;
                var target = targetField.GetValue(__instance) as Computer;
                if (target == null) return;

                PorthackHeartDaemon phd = null;
                foreach (var d in target.daemons)
                    if (d is PorthackHeartDaemon h) { phd = h; break; }
                if (phd == null || !phd.AutoOnPorthack) return;

                // 对齐原版时机：破解进度 > 50%
                if (progressField != null && progressField.GetValue(__instance) is float prog && prog <= 0.5f) return;

                // 对齐原版：把 porthack 置为 heart 特殊状态——IsTargetingPorthackHeart=true
                // （progress 随机卡住永不完成 + 界面 DarkRed 闪烁显示 UNKNOWN ERROR），
                // 以及破解界面立方体序列无限旋转（cubeSeq.ShouldCentralSpinInfinitley=true）
                try { targetingField?.SetValue(__instance, true); } catch { }
                try
                {
                    var cubeSeq = cubeSeqField?.GetValue(__instance);
                    if (cubeSeq != null)
                        cubeSeq.GetType().GetField("ShouldCentralSpinInfinitley")?.SetValue(cubeSeq, true);
                }
                catch { }

                phd.BreakHeart();
            }
            catch
            {
                // 反射/运行时异常静默：不干扰破解流程
            }
        }
    }
}
