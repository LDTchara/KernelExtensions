using Hacknet;
using KernelExtensions.Utilities;
using Pathfinder.Util;

namespace KernelExtensions.Actions.Misc
{
    /// <summary>
    /// **解开**目标机的防火墙（<c>firewall.solved = true</c>），可带延迟。
    ///
    /// XML 用法：
    ///   &lt;PortFirewallSolveAction /&gt;
    ///   &lt;PortFirewallSolveAction Delay="12" /&gt;
    ///
    /// TargetComp ：目标机 idName；留空 / NONE = 当前连接的节点。
    /// Delay      ：**基类提供**的"延迟多久才执行本动作"（继承自 DelayablePathfinderAction），
    ///              留空 = 立即执行；不在这里重复声明，否则会遮蔽基类字段。
    /// </summary>
    public class PortFirewallSolveAction : KEAction
    {
        [XMLStorage] public string TargetComp;

        public override void Trigger(OS os)
        {
            if (!TargetActionHelper.TryResolve(os, TargetComp, nameof(PortFirewallSolveAction), out Computer target)) return;

            if (target.firewall == null)
            {
                KELog.Warn($"[{nameof(PortFirewallSolveAction)}] '{target.idName}' has no firewall; skipped.");
                return;
            }

            // 延迟交给基类（Delay 属性）处理，这里一律立即解
            PortHackV2Effects.solveFirewall(os, target.idName, 0f);
            TargetActionHelper.LogDone(nameof(PortFirewallSolveAction), "firewall solved", target);
        }
    }
}
