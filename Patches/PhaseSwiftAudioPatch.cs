using Hacknet;
using HarmonyLib;
using KernelExtensions.Managers;
using Microsoft.Xna.Framework;

namespace KernelExtensions.Patches
{
    /// <summary>
    /// PhaseSwift 音频每帧更新补丁。
    /// 独立于 PhaseSwiftExe，确保 EXE 不运行时音频缓冲和音量仍持续更新。
    /// </summary>
    [HarmonyPatch]
    public static class PhaseSwiftAudioPatch
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(OS), "Update")]
        public static void OnOSUpdate(OS __instance, GameTime gameTime)
        {
            PhaseSwiftManager.UpdateAudioBuffers();
            PhaseSwiftManager.UpdateCrossfade((float)gameTime.ElapsedGameTime.TotalSeconds);
        }
    }
}
