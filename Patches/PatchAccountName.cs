using Hacknet;
using Hacknet.UIUtils;
using HarmonyLib;
using KernelExtensions.Configs;

namespace KernelExtensions.Patches
{
    /// <summary>
    /// 创建新账号时拦截禁用用户名（dev1 用户名系统 XML 化移植）。
    /// 检查 KE-Config.xml 的 BannedUsernames 段（UsernameProfiles），命中则显示原因并重置输入。
    /// </summary>
    [HarmonyPatch(typeof(SavefileLoginScreen), "Advance")]
    public static class PatchAccountName
    {
        [HarmonyPrefix]
        public static bool Prefix(SavefileLoginScreen __instance, string answer)
        {
            if (!UsernameProfiles.HasBans) return true;
            if (__instance.promptIndex != 0) return true;
            if (__instance.IsReady) return true;
            if (!UsernameProfiles.IsBanned(answer)) return true;

            string reason = UsernameProfiles.GetReason(answer);
            if (string.IsNullOrEmpty(reason))
                reason = "Unuseable User Name";

            // Pre 阶段钩子：允许外部修改原因
            UsernameProfiles.TriggerBeforeShowReason(ref reason, answer);
            __instance.History.Add(" -- " + reason + " -- ");
            // Post 阶段钩子：额外逻辑（不可修改原因）
            UsernameProfiles.TriggerAfterShowReason(answer, reason);

            // 重置输入状态（返回 false 拦截本次 Advance）
            __instance.promptIndex = 0;
            __instance.Answers.Clear();
            __instance.currentPrompt = __instance.PromptSequence[0];
            __instance.InPasswordMode = false;
            __instance.ClearTextBox();
            return false;
        }
    }
}
