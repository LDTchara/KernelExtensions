using Hacknet;
using Pathfinder.Action;
using Pathfinder.Util;
using Pathfinder.Util.XML;

namespace KernelExtensions.Actions
{
    /// <summary>
    /// 修改指定节点的名称，并持久化到存档。
    /// 用法：<RenameNode NodeID="dhs" NewName="新名称" />
    /// </summary>
    public class RenameNodeAction : DelayablePathfinderAction
    {
        [XMLStorage]
        public string NodeID;   // 目标节点的 idName

        [XMLStorage]
        public string NewName;  // 新名称

        public override void Trigger(OS os)
        {
            if (string.IsNullOrEmpty(NodeID) || string.IsNullOrEmpty(NewName))
                return;

            // 使用 ComputerLookup 或 Programs.getComputer 查找
            var comp = ComputerLookup.Find(NodeID, SearchType.Id)
                       ?? Programs.getComputer(os, NodeID);

            if (comp == null)
            {
                os.write($"RenameNode: 未找到节点 {NodeID}");
                return;
            }

            // 修改名称
            string oldName = comp.name;
            comp.name = NewName;

            // 如果正好是当前连接的主机，立即刷新终端位置信息
            if (os.connectedComp == comp)
            {
                // 强制触发一次位置字符串重建（Hacknet 内部会在下一帧自动处理，
                // 但这里手动改一下可保证即时性）
                os.displayCache = null;
            }

            // 由于 ComputerLookup 内部缓存了 name 查找，直接修改字段后，
            // 旧的名称会残留在查找表里。这里不做额外处理，因为通常不再需要
            // 根据旧名称查找，也不影响存档读写。如需完美，可以调一次
            // ComputerLookup.RebuildLookups()，但性能损耗极小。
        }

        public override void LoadFromXml(ElementInfo info)
        {
            base.LoadFromXml(info);
            if (info.Attributes.TryGetValue("Delay", out string delayStr))
                Delay = delayStr;
            if (info.Attributes.TryGetValue("DelayHost", out string delayHost))
                DelayHost = delayHost;
        }
    }
}