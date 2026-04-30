using Hacknet;
using KernelExtensions.Utility;
using Pathfinder.Action;
using Pathfinder.Util;
using Pathfinder.Util.XML;
using static System.Net.Mime.MediaTypeNames;

namespace KernelExtensions.Actions
{
    /// <summary>
    /// 播放扩展目录下的 WAV 音效文件。
    /// 用法：<PlaySound Path="Sounds/Click.wav" />
    /// 路径必须包含 .wav 扩展名。
    /// </summary>
    public class PlaySoundAction : DelayablePathfinderAction
    {
        [XMLStorage] public string Path;
        [XMLStorage] public float Volume = 0.5f;
        [XMLStorage] public float Pitch = 0.5f;
        [XMLStorage] public float Pan = 0f;

        public override void Trigger(OS os)
        {
            if (string.IsNullOrEmpty(Path))
            {
                Console.WriteLine("[KernelExtensions] PlaySoundAction: Missing 'Path' attribute.");
                return;
            }

            SoundHelper.PlaySound(os, Path, Volume, Pitch, Pan);
        }

        public override void LoadFromXml(ElementInfo info)
        {
            // 基类调用会读取 XMLStorage 标记的字段：Path, Volume, Pitch, Pan
            base.LoadFromXml(info);
            if (info.Attributes.TryGetValue("Delay", out string delayStr))
                Delay = delayStr;
            if (info.Attributes.TryGetValue("DelayHost", out string delayHost))
                DelayHost = delayHost;
        }
    }
}