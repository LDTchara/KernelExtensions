using Hacknet;
using Pathfinder;
using Pathfinder.Action;
using Pathfinder.Port;
using Pathfinder.Util;
using Pathfinder.Util.XML;


public class PortControlAction : PathfinderAction
{
    [XMLStorage]
    public string targetID;

    [XMLStorage]
    public string DisplayName;

    [XMLStorage]
    public string Protocol;

    [XMLStorage]
    public int PortNum;

    [XMLStorage]
    public string Switch = "add"; // 默认值为add   add/remove

    public override void Trigger(object os_obj)
    {
        OS os = (OS)os_obj;

        // 获取目标计算机
        Computer target = Programs.getComputer(os, targetID);
        if (target == null)
        {
            os.write("Target computer not found: " + targetID);
            return;
        }

        // 根据操作类型执行添加/移除
        if (Switch.ToLower() == "add")
        {
            // 注册端口到Pathfinder
            KernelExtensions.KernelExtensions.LoadActionPorts(Protocol, DisplayName, PortNum);

            // 添加到目标计算机
            target.AddPort(Protocol, PortNum, DisplayName);
            target.closePort(PortNum, os.thisComputer.ip);
            os.write($"Added port {PortNum} ({DisplayName}) to {targetID}");
        }
        else if (Switch.ToLower() == "remove")
        {
            // 自定义端口（如 114514）不在原版 PortExploits.services（端口号→服务名）中，
            // 直接 services[PortNum] 反查会抛 KeyNotFoundException。
            // 正确做法：与 AddPort 对称，按 Protocol 移除；
            // 未提供 Protocol 时再从端口注册表按端口号反查（同时覆盖原版/自定义）。
            string protocol = Protocol;
            if (string.IsNullOrEmpty(protocol))
                protocol = PortManager.GetPortRecordFromNumber(PortNum)?.Protocol;

            if (string.IsNullOrEmpty(protocol))
            {
                os.write($"Cannot remove port {PortNum}: protocol unknown on {targetID}");
                return;
            }

            if (target.RemovePort(protocol))
                os.write($"Removed port {PortNum} ({protocol}) from {targetID}");
            else
                os.write($"Port {PortNum} ({protocol}) not found on {targetID}");
        }
        else
        {
            os.write("Invalid switch operation: " + Switch);
        }
    }
}
