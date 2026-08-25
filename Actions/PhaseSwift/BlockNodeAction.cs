using Hacknet;
using KernelExtensions.Managers;
using KernelExtensions.Modules;
using Pathfinder.Action;
using Pathfinder.Util;

namespace KernelExtensions.Actions.PhaseSwift
{
    /// <summary>
    /// 在 PhaseSwift 中为指定场景添加运行时黑名单节点。
    /// 不填 SceneIndex 则使用当前场景。
    /// 用法：<BlockNode NodeId="A" /> 或 <BlockNode NodeId="A" SceneIndex="0" />
    /// </summary>
    public class BlockNodeAction : DelayablePathfinderAction
    {
        [XMLStorage] public string NodeId;
        [XMLStorage] public int SceneIndex = -1;

        public override void Trigger(OS os)
        {
            if (!string.IsNullOrEmpty(NodeId))
                PhaseSwiftManager.BlockNode(NodeId, SceneIndex);
        }
    }
}
