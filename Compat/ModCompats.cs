using System.Collections.Generic;
using KernelExtensions.Utilities;

namespace KernelExtensions.Compat
{
    /// <summary>
    /// 第三方模组兼容层总入口（结构参照 ZeroDayToolKit 的 <c>Compat/</c>）。
    ///
    /// 约定：各模组的兼容实现放在 <c>Compat/&lt;模组名&gt;/</c> 子目录；
    /// 本类只做**统一分发与汇总**，调用方（如 PhaseSwiftManager）不必知道具体是哪个模组存在。
    /// </summary>
    internal static class ModCompats
    {
        /// <summary>
        /// 是否有第三方音乐模组正在播放——会与 KE 的音频管线叠加。
        /// 目前只覆盖 Stuxnet.Audio（SASS）；其他第三方不在本层范围（KE 不为它们开特例）。
        /// </summary>
        internal static bool IsConflictingAudioPlaying()
            => Stuxnet.StuxnetAudioCompat.IsPlaying();

        /// <summary>若确有第三方音乐在播则停掉它（内部会先判断，未在播时不动）。</summary>
        internal static void StopConflictingAudio()
        {
            if (IsConflictingAudioPlaying())
                Stuxnet.StuxnetAudioCompat.Stop();
        }

        /// <summary>已探测到的兼容模组汇总，供启动日志/诊断使用。</summary>
        internal static string DescribeDetected()
        {
            var parts = new List<string>();
            if (Stuxnet.StuxnetAudioCompat.Detected) parts.Add("Stuxnet.Audio(SASS)");
            return parts.Count == 0 ? "none" : string.Join(", ", parts);
        }
    }
}
