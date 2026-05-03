using Hacknet;
using Pathfinder.Action;
using Pathfinder.Util;

namespace KernelExtensions.AirCraft.Actions
{
    /// <summary>
    /// 激活指定计算机（通过 idName）的全局高度计覆盖层。
    /// </summary>
    public class ShowAircraftOverlay : PathfinderAction
    {
        [XMLStorage]
        public string NodeID;

        public override void Trigger(object os_obj)
        {
            OS os = (OS)os_obj;
            if (string.IsNullOrEmpty(NodeID))
            {
                os.write("ERROR: ActivateAircraftOverlayAction requires a ComputerID attribute.");
                return;
            }

            // 在 os.netMap.nodes 中查找具有匹配 idName 的 Computer
            Computer targetComp = null;
            foreach (var comp in os.netMap.nodes)
            {
                if (comp.idName == NodeID)
                {
                    targetComp = comp;
                    break;
                }
            }

            if (targetComp == null)
            {
                os.write($"ERROR: Computer with idName '{NodeID}' not found.");
                return;
            }

            // 从 FlightDaemon.CompToDamons 字典获取对应的 FlightDaemon
            if (Daemon.FlightDaemon.CompToDamons.TryGetValue(targetComp, out var fd))
            {
                GlobalAircraftOverlayManager.CurrentFlightDaemon = fd;
                GlobalAircraftOverlayManager.IsOverlayActive = true;

                // 确保该飞行守护进程已经订阅了更新（如果尚未订阅，则开始）
                fd.StartUpdating();

                os.write($"Aircraft overlay activated for {targetComp.name} (idName: {NodeID}).");
            }
            else
            {
                os.write($"ERROR: No FlightDaemon found on computer '{NodeID}'.");
            }
        }
    }
}
