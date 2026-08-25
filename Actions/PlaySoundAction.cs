using Hacknet;
using KernelExtensions.Utilities;
using Pathfinder.Action;
using Pathfinder.Util;
using Pathfinder.Util.XML;

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
                KELog.Error("[PlaySound] Missing 'Path' attribute.");
                return;
            }

            SoundHelper.PlaySound(os, Path, Volume, Pitch, Pan);
        }

        public override void LoadFromXml(ElementInfo info)
        {
            base.LoadFromXml(info);
        }
    }
}