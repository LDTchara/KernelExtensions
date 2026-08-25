using Hacknet;
using HarmonyLib;
using System.Reflection;
using PorthackHeartDaemon = KernelExtensions.Daemons.PorthackHeartDaemon;

namespace KernelExtensions.Patches
{
    /// <summary>
    /// 连接 KE PorthackHeartDaemon 节点时自动显示 daemon 界面（对齐原版 DisplayModule
    /// 对原版 heart daemon 的优先显示：doCommandModule 里 connectedComp 有 heart daemon 时
    /// 直接 doDaemonDisplay，不显示 ls/connect 等）。KE 复刻版是不同类，原版分支不识别，
    /// 需要此 patch——否则心碎触发（锁输入）后无法手动输入 daemon 名查看，序列无法推进。
    /// doDaemonDisplay 与 os 字段在 internal 基类/私有成员，反射调用。
    /// </summary>
    [HarmonyPatch(typeof(DisplayModule), "doCommandModule")]
    internal static class PorthackHeartDisplayPatch
    {
        private static FieldInfo osField;
        private static MethodInfo doDaemonDisplay;

        [HarmonyPrefix]
        private static bool Prefix(DisplayModule __instance)
        {
            try
            {
                if (osField == null)
                    osField = AccessTools.Field(typeof(DisplayModule), "os");
                if (doDaemonDisplay == null)
                    doDaemonDisplay = AccessTools.Method(typeof(DisplayModule), "doDaemonDisplay");

                var os = osField?.GetValue(__instance) as OS;
                if (os?.connectedComp != null)
                {
                    foreach (var d in os.connectedComp.daemons)
                    {
                        if (d is PorthackHeartDaemon)
                        {
                            doDaemonDisplay?.Invoke(__instance, null);
                            return false; // 跳过原方法（不显示 ls/connect 等）
                        }
                    }
                }
            }
            catch
            {
                // 反射失败静默：回退原版行为
            }
            return true;
        }
    }
}
