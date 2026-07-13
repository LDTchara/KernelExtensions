using Hacknet;
using KernelExtensions.Modules;
using Pathfinder.Action;
using Pathfinder.Util;

namespace KernelExtensions.Actions.PhaseSwift
{
    /// <summary>
    /// 淡出所有音轨到静音。
    /// 使用 Manager 的交叉淡化系统，不释放 DSEI，可随时切场景恢复。
    ///
    /// 用法：<PhaseSwiftFadeOut Duration="2" />
    ///
    /// 参数：
    ///   Duration  (float, 可选) 淡出时长（秒），默认 1
    /// </summary>
    public class PhaseSwiftFadeOutAction : DelayablePathfinderAction
    {
        [XMLStorage] public float Duration = 1f;

        public override void Trigger(OS os)
        {
            PhaseSwiftManager.StartFadeOut(Duration);
        }
    }
}