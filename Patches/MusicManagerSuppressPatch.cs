using Hacknet;
using HarmonyLib;
using KernelExtensions.Managers;

namespace KernelExtensions.Patches
{
    /// <summary>
    /// MusicManager 压制 Patch。
    /// 仅当 PS **正在接管音频输出**（运行中且多轨模式）时吞掉 MM 的播放入口，
    /// 防止 MM 与 PS 的 DSEI 叠播。
    /// 单轨模式下 PS 自己就用 MusicManager 播放，不能压制。
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
            // 只有多轨模式才拦截；单轨模式必须放行 MusicManager 本身。
            return !PhaseSwiftManager.IsDrivingAudio;
        }
    }
}
