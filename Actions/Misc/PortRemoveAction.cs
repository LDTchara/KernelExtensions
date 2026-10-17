using Hacknet;
using Pathfinder.Port;
using Pathfinder.Util;

namespace KernelExtensions.Actions.Misc;

/// <summary>
/// 从目标机**移除**一个端口。
///
/// <para>
/// 这是原来 <c>PortControl</c>（<c>Switch=remove</c> 分支）拆出来的一半。
/// 只动**这台机器**的端口表（<c>comp.RemovePort</c>），**不注销**全局协议注册 ——
/// 协议是全局共享的，注销会影响同进程里其它机器/其它存档。
/// </para>
///
/// <para>XML 用法：</para>
/// <code>
/// &lt;PortRemove TargetComp="targetNode" Protocol="ssh" /&gt;
/// &lt;PortRemove Protocol="ssh" /&gt;                      （省略 TargetComp = 当前所连节点）
/// &lt;PortRemove PortNum="22" /&gt;                        （不给 Protocol 时按端口号反查）
/// </code>
///
/// <para>
/// <c>TargetComp</c>：目标机 idName；**留空 / NONE = 当前连接的节点**。
/// <c>Protocol</c>：要移除的协议名；省略时用 <c>PortNum</c> 在端口注册表里反查。
/// <c>PortNum</c>：端口号，仅用于反查协议（不参与移除逻辑）。
/// </para>
/// </summary>
public class PortRemoveAction : KEAction
{
    [XMLStorage]
    public string TargetComp;

    [XMLStorage]
    public string Protocol;

    [XMLStorage]
    public int PortNum;

    public override void Trigger(OS os)
    {
        if (!TargetActionHelper.TryResolve(os, TargetComp, nameof(PortRemoveAction), out Computer target)) return;

        // 未给协议名时按端口号反查（覆盖原版/自定义端口）
        string protocol = Protocol;
        if (string.IsNullOrEmpty(protocol) && PortNum > 0)
        {
            try { protocol = PortManager.GetPortRecordFromNumber(PortNum)?.Protocol; }
            catch { protocol = null; }
        }

        if (string.IsNullOrEmpty(protocol))
        {
            os.write($"PortRemove: cannot resolve protocol (Protocol empty and PortNum {PortNum} unknown) on {target.idName}");
            return;
        }

        if (target.RemovePort(protocol))
        {
            os.write($"Removed port ({protocol}) from {target.idName}");
            TargetActionHelper.LogDone(nameof(PortRemoveAction), $"removed port '{protocol}'", target);
        }
        else
        {
            os.write($"Port {protocol} not found on {target.idName}");
        }
    }
}
