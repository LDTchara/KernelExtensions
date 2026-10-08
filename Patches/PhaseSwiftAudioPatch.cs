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

        /// <summary>
        /// 捕获玩家设定的音乐音量（E1）。
        /// 不调 <c>MusicManager.getVolume()</c>：它可被第三方模组（如 Stuxnet.Audio）patch 成
        /// 返回自有音量，甚至在该模组卸载后抛 NRE。
        /// <c>setVolume</c> 是安全的捕获点：它是 `return true`（原版照常执行），
        /// 且原版淡入淡出走的是直接赋 <c>MediaPlayer.Volume</c>，不会污染这里的参数。
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(MusicManager), "setVolume")]
        public static void OnSetVolume(float volume)
        {
            PhaseSwiftManager.OnPlayerVolumeChanged(volume);
        }
    }
}
