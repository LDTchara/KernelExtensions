using Hacknet;
using Pathfinder.Port;
using Pathfinder.Util;

namespace KernelExtensions.Actions.Misc;

/// <summary>
/// 给目标机**注册并添加**一个端口，或从目标机**移除**一个端口。
///
/// <para>
/// XML 用法：
/// <code>
/// &lt;PortControl TargetID="targetNode" Protocol="ssh" DisplayName="SSH" PortNum="22" /&gt;
/// &lt;PortControl TargetID="targetNode" Protocol="ssh" Switch="remove" /&gt;
/// </code>
/// </para>
///
/// <para>
/// <c>Switch</c>：<c>add</c>（默认，大小写不敏感）/ <c>remove</c>，其它值记 Warn 并跳过。
/// </para>
///
/// <para>
/// 继承 <see cref="KEAction"/> 而非直接用 <c>PathfinderAction</c>：Pathfinder 的
/// <c>[XMLStorage]</c> 按字段名**精确匹配**属性表，作者写 <c>targetid=</c>（小写）会**静默失效**；
/// 基类会把属性表换成大小写不敏感副本，同时带来 <c>Delay</c> / <c>DelayHost</c> 支持。
/// </para>
/// </summary>
public class PortControlAction : KEAction
{
    [XMLStorage]
    public string TargetID;

    [XMLStorage]
    public string DisplayName;

    [XMLStorage]
    public string Protocol;

    [XMLStorage]
    public int PortNum;

    /// <summary>操作类型：<c>add</c>（默认）/ <c>remove</c>，大小写不敏感。</summary>
    [XMLStorage]
    public string Switch = "add";

    public override void Trigger(OS os)
    {
        Computer target = Programs.getComputer(os, TargetID);
        if (target == null)
        {
            os.write("Target computer not found: " + TargetID);
            return;
        }

        string op = (Switch ?? "add").Trim();

        if (op.Equals("add", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(Protocol) || string.IsNullOrWhiteSpace(DisplayName))
            {
                os.write("PortControl: Protocol and DisplayName are required when adding a port.");
                return;
            }

            // 先注册协议再装端口：AddPort 在协议未注册时会**自己补注册**，但它会顺手打一条
            // "not registered, please correct, this will crash in 6.0.0" 的警告 ——
            // 我们本来就打算注册，不该让 Pathfinder 记这条噪音。
            // ⚠️ 限定符必须写 global:: —— 本文件位于 KernelExtensions.Actions.Misc，
            // 裸写 `KernelExtensions.X` 会被解析成嵌套命名空间而不是那个同名类。
            global::KernelExtensions.KernelExtensions.LoadActionPorts(Protocol, DisplayName, PortNum);

            // 重复添加是安全的：Pathfinder 的 AddPort 内部按协议去重（已存在则直接返回）
            target.AddPort(Protocol, PortNum, DisplayName);
            target.closePort(PortNum, os.thisComputer.ip);
            os.write($"Added port {PortNum} ({DisplayName}) to {TargetID}");
        }
        else if (op.Equals("remove", StringComparison.OrdinalIgnoreCase))
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
                os.write($"Cannot remove port {PortNum}: protocol unknown on {TargetID}");
                return;
            }

            if (target.RemovePort(protocol))
                os.write($"Removed port {PortNum} ({protocol}) from {TargetID}");
            else
                os.write($"Port {PortNum} ({protocol}) not found on {TargetID}");
        }
        else
        {
            os.write("Invalid switch operation: " + Switch);
        }
    }
}
