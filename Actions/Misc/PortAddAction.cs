using Hacknet;
using Pathfinder.Port;
using Pathfinder.Util;

namespace KernelExtensions.Actions.Misc;

/// <summary>
/// 给目标机**添加**一个端口（协议未注册时顺带注册它）。
///
/// <para>
/// 这是原来 <c>PortControl</c>（<c>Switch=add</c> 分支）拆出来的一半 ——
/// 拆分理由：一个 Action 只干一件事，且「加端口」与「删端口」的写法、失败模式完全不同。
/// </para>
///
/// <para>XML 用法：</para>
/// <code>
/// &lt;PortAdd TargetComp="targetNode" Protocol="ssh" DisplayName="SSH" PortNum="22" /&gt;
/// &lt;PortAdd Protocol="ssh" DisplayName="SSH" PortNum="22" /&gt;   （省略 TargetComp = 当前所连节点）
/// </code>
///
/// <para>
/// <c>TargetComp</c>：目标机 idName；**留空 / NONE = 当前连接的节点**（与本组其它目标机动作一致）。
/// <c>Protocol</c> / <c>DisplayName</c>：必填。
/// <c>PortNum</c>：新协议**必填**（> 0）；协议已注册时可省略，回退该协议的默认端口号。
/// </para>
///
/// <para>
/// ⚠️ 协议注册是**全局**的（<c>PortManager.CustomPorts</c>），且**进程级共享**：
/// 同一进程里读过的存档、其它 Action 注册过的同名协议都会让 <c>IsPortRegistered</c> 为真。
/// 因此这里**已注册就不重复注册**（重复注册会往全局表里塞第二条同协议记录）。
/// </para>
///
/// <para>
/// ⚠️ 持久化：全局注册表**不进存档**；但**每台机器的端口状态**会随存档保存
/// （`&lt;ports&gt;&lt;协议&gt;&lt;Original/&gt;&lt;Number/&gt;&lt;Display/&gt;`），
/// 读档时若全局表里没有该协议，Pathfinder 会**按存档数据现场补注册**。
/// 所以运行期加的端口在「该节点仍有此端口」时会随档恢复；
/// 若端口被移除/节点被删，协议本身不会自己回来 —— 需要再次 <c>PortAdd</c> 或内容里的 <c>&lt;PFPorts&gt;</c>。
/// </para>
/// </summary>
public class PortAddAction : KEAction
{
    [XMLStorage]
    public string TargetComp;

    [XMLStorage]
    public string DisplayName;

    [XMLStorage]
    public string Protocol;

    [XMLStorage]
    public int PortNum;

    public override void Trigger(OS os)
    {
        if (!TargetActionHelper.TryResolve(os, TargetComp, nameof(PortAddAction), out Computer target)) return;

        if (string.IsNullOrWhiteSpace(Protocol) || string.IsNullOrWhiteSpace(DisplayName))
        {
            os.write("PortAdd: Protocol and DisplayName are required.");
            return;
        }

        string displayNameForLog = DisplayName;
        bool registered = IsProtocolRegistered(Protocol);
        int portNum = PortNum;

        if (!registered)
        {
            // 新协议：必须给出真实端口号（注册表里的默认端口号会决定 PortRecord）
            if (portNum <= 0)
            {
                os.write($"PortAdd: PortNum is required when registering a new protocol '{Protocol}'.");
                return;
            }

            // 先注册协议再装端口：AddPort 在协议未注册时会自己补注册，但会顺手打一条
            // "not registered, please correct, this will crash in 6.0.0" 的噪音 —— 我们本来就打算注册。
            global::KernelExtensions.KernelExtensions.EnsurePortProtocolRegistered(Protocol, DisplayName, portNum);
        }
        else if (portNum <= 0)
        {
            // 已注册且没给端口号 → 用该协议的默认端口号
            try { portNum = PortManager.GetPortRecordFromProtocol(Protocol)?.DefaultPortNumber ?? -1; }
            catch { portNum = -1; }
            if (portNum <= 0)
            {
                os.write($"PortAdd: PortNum is required (protocol '{Protocol}' has no usable default port).");
                return;
            }
        }

        // 重复添加是安全的：Pathfinder 的 AddPort 内部按协议去重（已存在则直接返回）
        target.AddPort(Protocol, portNum, DisplayName);
        // 与原 PortControl 一致：加进来先处于「未破解」状态
        target.closePort(portNum, os.thisComputer.ip);
        os.write($"Added port {portNum} ({DisplayName}) to {target.idName}");
        TargetActionHelper.LogDone(nameof(PortAddAction), $"added port {portNum} ({displayNameForLog})", target);
    }

    /// <summary>协议是否已在全局表里（原版内置 + 模组注册 + 读档补注册的都算）。</summary>
    private static bool IsProtocolRegistered(string protocol)
    {
        try { return PortManager.IsPortRegistered(protocol); }
        catch { return false; }
    }
}
