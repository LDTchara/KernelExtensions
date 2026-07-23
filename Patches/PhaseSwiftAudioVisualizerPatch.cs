using System.Reflection;
using Hacknet.Effects;
using HarmonyLib;
using KernelExtensions.Modules;
using Microsoft.Xna.Framework.Media;

namespace KernelExtensions.Patches
{
    /// <summary>
    /// 临时设 MediaPlayer.State = Playing 使 AudioVisualizer 调用 GetVisualizationData。
    /// Prefix 在每帧 Draw 前备份 State 并设为 Playing，Postfix 恢复。
    /// 不影响实际音频播放——DSEI 有自己的音频管道。
    /// </summary>
    [HarmonyPatch(typeof(AudioVisualizer), "Draw")]
    public class PhaseSwiftAudioVisualizerPatch
    {
        private static readonly FieldInfo _stateField =
            typeof(MediaPlayer).GetField("INTERNAL_state",
                BindingFlags.Static | BindingFlags.NonPublic);

        //private static MediaState _savedState;

        [HarmonyPrefix]
        static void Prefix()
        {
            if (!PhaseSwiftManager.UseDualTrack) return;
            // 设 State = Playing 使 AudioVisualizer 调用 GetVisualizationData
            // 不保存旧值、不恢复——MusicManager 自己的操作会正确管理 State
            _stateField.SetValue(null, MediaState.Playing);
        }
        /*
        [HarmonyPostfix]
        static void Postfix()
        {
            // 不再恢复旧 State，避免覆盖 MusicManager 后续设的 Playing
        }*/
    }
}
