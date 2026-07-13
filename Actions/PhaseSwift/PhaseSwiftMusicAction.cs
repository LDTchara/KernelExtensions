using Hacknet;
using KernelExtensions.Modules;
using Pathfinder.Action;

namespace KernelExtensions.Actions.PhaseSwift
{
    /// <summary>
    /// 切换 PhaseSwift 音乐组，不切换场景。
    ///
    /// 用法：<PhaseSwiftMusic Phase="1" />
    ///
    /// 参数：
    ///   Phase  (int, 必填) 目标音乐组索引（对应 MusicPhases 列表中的 id）
    /// </summary>
    public class PhaseSwiftMusicAction : DelayablePathfinderAction
    {
        [Pathfinder.Util.XMLStorage] public int Phase;

        public override void Trigger(OS os)
        {
            PhaseSwiftManager.SwitchMusicPhase(Phase);
        }
    }
}