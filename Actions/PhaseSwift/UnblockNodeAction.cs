using Hacknet;
using KernelExtensions.Modules;
using Pathfinder.Action;
using Pathfinder.Util;

namespace KernelExtensions.Actions.PhaseSwift
{
    /// <summary>
    /// 移除 PhaseSwift 中指定场景的运行时黑名单节点。
    /// 不填 SceneIndex 则使用当前场景。
    /// 用法：<UnblockNode NodeId="A" /> 或 <UnblockNode NodeId="A" SceneIndex="0" />
    /// </summary>
    public class UnblockNodeAction : DelayablePathfinderAction
    {
        [XMLStorage] public string NodeId;
        [XMLStorage] public int SceneIndex = -1;

        public override void Trigger(OS os)
        {
            if (!string.IsNullOrEmpty(NodeId))
                PhaseSwiftManager.UnblockNode(NodeId, SceneIndex);
        }
    }
}
