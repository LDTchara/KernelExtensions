using Hacknet;
using HarmonyLib;
using KernelExtensions.Modules;

namespace KernelExtensions.Patches
{
    /// <summary>
    /// MusicManager 压制 Patch。
    /// PS 运行时吞掉 MM 的播放入口，防止 MM 复活与原版单曲叠播。
    /// 复活源: TuneswapExe/SequencerExe（玩家可运行，不可控）、
    ///         HackerScript/MissionFunctions（扩展作者可避开）、CrashModule（系统不可控）。
    /// 不拦截 setVolume/setIsMuted —— PS SyncVolume() 乘 getVolume() 需要联动。
    /// Stop 后恢复原版行为（玩家仍可用 Tuneswap）。
    /// </summary>
    [HarmonyPatch]
    public static class MusicManagerSuppressPatch
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(MusicManager), nameof(MusicManager.playSong))]
        [HarmonyPatch(typeof(MusicManager), nameof(MusicManager.playSongImmediatley))]
        [HarmonyPatch(typeof(MusicManager), nameof(MusicManager.transitionToSong))]
        static bool Prefix()
        {
            // PS 运行时吞掉播放请求（同 PhaseSwiftConnectionPatch 的 IsRunning 开关模式）
            return !PhaseSwiftManager.IsRunning;
        }
    }
}
