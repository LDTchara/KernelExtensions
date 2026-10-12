using Hacknet;
using KernelExtensions.Utilities;
using Pathfinder.Util;

namespace KernelExtensions.Actions.Misc
{
    /// <summary>
    /// **调整现有防火墙的配置**：解的长度、额外延迟、解的内容，可选重置已解状态。
    ///
    /// XML 用法：
    ///   &lt;PortFirewallConfigureAction Length="20" AdditionalDelay="0.2" Solution="@random" Reset="true" /&gt;
    ///
    /// Length          ：解的长度；留空 = 保持原值。
    /// AdditionalDelay ：解出后额外延迟（秒）；留空 = 保持原值。
    /// Solution        ：解的内容；写 <c>@random</c> = 随机生成；留空 = 保持原值。
    /// Reset           ：是否清掉已解状态与分析进度；留空 = false。
    /// TargetComp      ：目标机 idName；留空 / NONE = 当前连接的节点。
    ///
    /// 目标机**没有**防火墙时按"添加一份"处理（对齐底层实现的既有语义）。
    /// </summary>
    public class PortFirewallConfigureAction : KEAction
    {
        [XMLStorage] public string Length;
        [XMLStorage] public string AdditionalDelay;
        [XMLStorage] public string Solution;
        [XMLStorage] public string Reset;
        [XMLStorage] public string TargetComp;

        public override void Trigger(OS os)
        {
            if (!TargetActionHelper.TryResolve(os, TargetComp, nameof(PortFirewallConfigureAction), out Computer target)) return;

            // Length 留空 = 保持原值：用现有防火墙的解长度打底
            float fallbackLen = target.firewall?.solutionLength ?? 0;
            float len = ConfigValue.ParseInRange(Length, fallbackLen, 0f, float.MaxValue, out bool lenOk);
            if (!lenOk)
                KELog.Warn($"[{nameof(PortFirewallConfigureAction)}] invalid <Length> '{Length}'; keeping the current length.");

            float fallbackDelay = target.firewall?.additionalDelay ?? 0f;
            float delay = ConfigValue.ParseInRange(AdditionalDelay, fallbackDelay, 0f, float.MaxValue, out bool dOk);
            if (!dOk)
                KELog.Warn($"[{nameof(PortFirewallConfigureAction)}] invalid <AdditionalDelay> '{AdditionalDelay}'; keeping the current delay.");

            // Solution 留空 = 保持原值
            string solution = ConfigValue.IsNone(Solution) ? (target.firewall?.solution ?? "@random") : Solution.Trim();
            bool reset = TargetActionHelper.ParseBool(Reset, false);

            PortHackV2Effects.SetFirewallConfig(os, target.idName, (int)len, delay, solution, reset);
            TargetActionHelper.LogDone(nameof(PortFirewallConfigureAction),
                $"firewall configured (length {(int)len}, reset={reset})", target);
        }
    }
}
