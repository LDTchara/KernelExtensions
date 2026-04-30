using Hacknet;
using Hacknet.Extensions;
using HarmonyLib;
using KernelExtensions.Config;
using KernelExtensions.Modules;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace KernelExtensions.Patches
{
    /// <summary>
    /// Harmony 补丁：修改原版 CrashModule 以支持自定义 VM 攻击。
    /// </summary>
    [HarmonyPatch(typeof(CrashModule))]
    internal static class CrashModuleVMAttackPatch
    {
        // 辅助方法：获取 CrashModule 内部的 os 字段
        private static OS GetOS(CrashModule instance) =>
            Traverse.Create(instance).Field("os").GetValue<OS>();
        // 获取私有字段的快捷方法
        private static T GetField<T>(this CrashModule instance, string name) =>
            Traverse.Create(instance).Field(name).GetValue<T>();
        private static void SetField(this CrashModule instance, string name, object value) =>
            Traverse.Create(instance).Field(name).SetValue(value);

        /// <summary>
        /// Prefix: 替换 Update 中的第50行检测和15秒结束逻辑。
        /// 只在 VM 攻击激活时生效。
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch("Update")]
        public static bool UpdatePrefix(CrashModule __instance, float t)
        {
            OS os = GetOS(__instance);
            if (os == null) return true;

            // 检查是否有 VM 攻击
            string flag = os.Flags.GetFlagStartingWith("Kernel_VMInfected_");
            if (string.IsNullOrEmpty(flag))
                return true; // 无攻击，走原版

            // 根据 flag 强制加载对应配置，覆盖旧值
            string configName = flag.Substring("Kernel_VMInfected_".Length);
            string configPath = Path.Combine(ExtensionLoader.ActiveExtensionInfo.FolderPath, "VMATK", configName + ".xml");
            if (!File.Exists(configPath))
            {
                os.Flags.RemoveFlag(flag);
                VMInfectionManager.CurrentConfig = null;
                return true;
            }

            VMAttackConfig config;
            try
            {
                var serializer = new System.Xml.Serialization.XmlSerializer(typeof(VMAttackConfig));
                using (var fs = new FileStream(configPath, FileMode.Open))
                    config = (VMAttackConfig)serializer.Deserialize(fs);
                VMInfectionManager.CurrentConfig = config;
            }
            catch
            {
                VMInfectionManager.CurrentConfig = null;
                return true;
            }

            // 获取 CrashModule 内部状态
            float elapsedTime = __instance.GetField<float>("elapsedTime");
            int bootTextCount = __instance.GetField<int>("bootTextCount");
            bool isInHostileFileCrash = __instance.GetField<bool>("IsInHostileFileCrash");

            // ---- 模拟原版 Update 的核心逻辑，但加入自定义条件 ----
            // 首先检查是否刚从第50行触发错误
            if (!isInHostileFileCrash)
            {
                // 我们不需要模拟整个 Update，只需在 bootTextCount 刚到达50时干涉
                if (bootTextCount == 50)
                {
                    bool shouldError;
                    if (config.Mode == RecoveryMode.Password)
                        shouldError = true;   // 密码模式无条件进入错误
                    else
                        shouldError = VMInfectionManager.CheckFileCondition(os, config);

                    if (shouldError)
                    {
                        // 强行设置原版错误状态
                        __instance.SetField("IsInHostileFileCrash", true);
                        __instance.SetField("elapsedTime", 0.2f); // 重置计时
                        __instance.SetField("bootTextTimer", 999999f); // 阻止继续滚动
                        os.thisComputer.bootTimer = 9999f;
                        return false; // 跳过原版 Update 其余部分
                    }
                }
                return true; // 继续执行原版 Update
            }
            else
            {
                // 已在错误状态，原版会累加 elapsedTime 并在>=15f时激活 BootAssitanceModule
                // 我们在这里修改15秒后的行为
                if (elapsedTime >= 14.9f) // 提前一点点防止错过
                {
                    // 激活我们的恢复模块
                    VMInfectionManager.ActivateRecovery(os, config);
                    // 让原版 CrashModule 退出错误状态
                    __instance.SetField("IsInHostileFileCrash", false);
                    __instance.SetField("elapsedTime", 0f);
                    os.bootingUp = false;
                    // 不激活其 BootAssitanceModule（设置 null）
                    os.BootAssitanceModule = null;
                    return false;
                }
                return true;
            }
        }

        /// <summary>
        /// Transpiler: 替换 Draw 中的错误消息字符串。
        /// </summary>
        [HarmonyTranspiler]
        [HarmonyPatch("Draw")]
        public static IEnumerable<CodeInstruction> DrawTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            foreach (var code in instructions)
            {
                if (code.opcode == System.Reflection.Emit.OpCodes.Ldstr &&
                    (string)code.operand == "ERROR: Critical boot error loading \"VMBootloaderTrap.dll\"")
                {
                    // 替换为自定义消息
                    code.opcode = System.Reflection.Emit.OpCodes.Call;
                    code.operand = AccessTools.Method(typeof(CrashModuleVMAttackPatch), nameof(GetErrorMessage));
                }
                yield return code;
            }
        }

        public static string GetErrorMessage()
        {
            if (VMInfectionManager.CurrentConfig != null)
            {
                if (VMInfectionManager.CurrentConfig.Mode == RecoveryMode.Password)
                {
                    // 若配置了自定义错误消息，优先使用；否则使用默认 TPM 消息
                    if (!string.IsNullOrEmpty(VMInfectionManager.CurrentConfig.ErrorMessage))
                        return VMInfectionManager.CurrentConfig.ErrorMessage;
                    return "ERROR: Critical boot error - TPM Platform Key Verification Failure";
                }
                return VMInfectionManager.CurrentConfig.ErrorMessage;
            }
            return "ERROR: Critical boot error loading \"VMBootloaderTrap.dll\"";
        }
    }
}