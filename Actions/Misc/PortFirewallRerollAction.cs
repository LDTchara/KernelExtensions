using Hacknet;
using KernelExtensions.Utilities;
using Pathfinder.Util;

namespace KernelExtensions.Actions.Misc
{
    /// <summary>
    /// **重掷**目标机防火墙的解（随机生成一份新的），可选顺带把已解状态清掉。
    ///
    /// XML 用法：
    ///   &lt;PortFirewallRerollAction Length="12" AdditionalDelay="0.3" /&gt;
    ///   &lt;PortFirewallRerollAction Length="12" Reset="true" /&gt;
    ///
    /// Length          ：新解的长度；留空 = 12。
    /// AdditionalDelay ：解出后额外延迟（秒）；留空 = 0。
    /// Reset           ：是否把 <c>solved</c> 重置为 false（并清空分析进度）；留空 = true。
    /// TargetComp      ：目标机 idName；留空 / NONE = 当前连接的节点。
    ///
    /// 与 <see cref="PortFirewallAddAction"/> 的区别：那个是"放一份新防火墙"，
    /// 这个是"防火墙还在，但解法被换掉"（剧情上常见于对手反制）。
    /// </summary>
    public class PortFirewallRerollAction : KEAction
    {
        [XMLStorage] public string Length;
        [XMLStorage] public string AdditionalDelay;
        [XMLStorage] public string Reset;
        [XMLStorage] public string TargetComp;

        public override void Trigger(OS os)
        {
            if (!TargetActionHelper.TryResolve(os, TargetComp, nameof(PortFirewallRerollAction), out Computer target)) return;

            if (target.firewall == null)
            {
                KELog.Warn($"[{nameof(PortFirewallRerollAction)}] '{target.idName}' has no firewall; skipped.");
                return;
            }

            float len = ConfigValue.ParseInRange(Length, 12f, 0f, float.MaxValue, out bool lenOk);
            if (!lenOk)
                KELog.Warn($"[{nameof(PortFirewallRerollAction)}] invalid <Length> '{Length}'; using 12.");

            float delay = ConfigValue.ParseInRange(AdditionalDelay, 0f, 0f, float.MaxValue, out bool dOk);
            if (!dOk)
                KELog.Warn($"[{nameof(PortFirewallRerollAction)}] invalid <AdditionalDelay> '{AdditionalDelay}'; using 0.");

            bool reset = TargetActionHelper.ParseBool(Reset, true);

            PortHackV2Effects.ReRandomFirewallSolution(os, target.idName, (int)len, delay, reset);
            TargetActionHelper.LogDone(nameof(PortFirewallRerollAction),
                $"firewall solution rerolled (length {(int)len}, reset={reset})", target);
        }
    }
}
