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
            // 先执行 Action 线程排入的主线程工作（D3：音频 API 必须回主线程），
            // 再刷新音频缓冲 —— 顺序不能反，否则本帧音频会先用上上一帧的旧状态。
            PhaseSwiftManager.DrainMainThreadQueue();
            PhaseSwiftManager.UpdateAudioBuffers();
            PhaseSwiftManager.UpdateCrossfade((float)gameTime.ElapsedGameTime.TotalSeconds);
        }
    }
}
