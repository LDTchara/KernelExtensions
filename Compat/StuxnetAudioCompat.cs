using System.Reflection;
using KernelExtensions.Utilities;

namespace KernelExtensions.Compat
{
    /// <summary>
    /// Stuxnet.Audio（SASS）兼容层。
    ///
    /// 背景：SASS 用自己的 <c>DynamicSoundEffectInstance</c> 播放音乐，且其 <c>MediaPlayer.Play</c>
    /// 拦截是 <c>return false</c> —— 原版 <c>MusicManager.isPlaying</c> 恒为 false，因此 PS 的
    /// <c>MusicManager.stop()</c> 停不到它。实测（2026-09-22）：
    ///   · 手动开 PS 时 SASS 已播稳，可经 SASS 的 <c>stop</c> Prefix 转发停掉；
    ///   · 读档恢复时 SASS 尚在**异步加载**，<c>stop()</c> 落在「还没开始播」的瞬间 → 漏掉。
    ///
    /// 做法：反射直接询问并停止它。不引入编译期依赖；SASS 不存在 / 改名 / 改版本时静默失效。
    /// ⚠️ 本层**只针对 SASS 一个模组**。其他第三方音乐模组不受保护——KE 不为它们开特例。
    /// </summary>
    internal static class StuxnetAudioCompat
    {
        private const string TypeName = "StuxnetHN.Audio.Replacements.StuxnetMusicManager";

        private static bool _probed;
        private static PropertyInfo _playingProp;
        private static MethodInfo _stopSong;

        internal static bool Detected => _playingProp != null;

        private static void Probe()
        {
            if (_probed) return;
            _probed = true;
            try
            {
                // 不用 Assembly.Load（程序集名与插件 ID 不一定一致），直接遍历已加载程序集
                Type t = Type.GetType(TypeName);
                if (t == null)
                {
                    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        try { t = asm.GetType(TypeName); } catch { t = null; }
                        if (t != null) break;
                    }
                }
                if (t == null) return;

                _playingProp = t.GetProperty("Playing", BindingFlags.Public | BindingFlags.Static);
                _stopSong = t.GetMethod("StopSong", BindingFlags.Public | BindingFlags.Static);
                KELog.Debug($"[Compat/SASS] detected (Playing={_playingProp != null}, StopSong={_stopSong != null})");
            }
            catch (Exception ex)
            {
                KELog.Debug($"[Compat/SASS] probe failed: {ex.Message}");
            }
        }

        /// <summary>SASS 是否正在播放。SASS 不存在时恒 false。</summary>
        internal static bool IsPlaying()
        {
            Probe();
            if (_playingProp == null) return false;
            try { return (bool)_playingProp.GetValue(null); }
            catch { return false; }
        }

        /// <summary>停掉 SASS 的播放（仅在它确实在播时调用）。SASS 不存在或探测失败时静默返回。</summary>
        internal static void Stop()
        {
            if (_stopSong == null) return;
            try
            {
                _stopSong.Invoke(null, null);
                KELog.Info("[Compat/SASS] stopped Stuxnet.Audio playback (PS takes over the audio pipeline).");
            }
            catch (Exception ex)
            {
                KELog.Debug($"[Compat/SASS] StopSong failed: {ex.Message}");
            }
        }
    }
}
