using Hacknet;
using KernelExtensions.Utilities;
using Pathfinder.Util;

namespace KernelExtensions.Actions.Misc
{
    /// <summary>
    /// 在目标机上**添加/设置**防火墙，并按需生成随机解。
    ///
    /// XML 用法：
    ///   &lt;PortFirewallAddAction Solution="ABCD" /&gt;
    ///   &lt;PortFirewallAddAction Solution="@random" Length="12" AdditionalDelay="0.1" /&gt;
    ///
    /// Solution        ：解的内容。写 <c>@random</c> = 随机生成。
    /// Length          ：随机解的长度；留空 = 解本身的长度。仅 <c>@random</c> 时有意义。
    /// AdditionalDelay ：解出后额外延迟（秒）；留空 = 0。
    /// TargetComp      ：目标机 idName；留空 / NONE = 当前连接的节点。
    ///
    /// ⚠️ 这是"加防火墙、且未解"状态：写完 <c>solved = false</c>。
    /// </summary>
    public class PortFirewallAddAction : KEAction
    {
        [XMLStorage] public string Solution;
        [XMLStorage] public string Length;
        [XMLStorage] public string AdditionalDelay;
        [XMLStorage] public string TargetComp;

        public override void Trigger(OS os)
        {
            if (!TargetActionHelper.TryResolve(os, TargetComp, nameof(PortFirewallAddAction), out Computer target)) return;

            if (ConfigValue.IsNone(Solution))
            {
                KELog.Warn($"[{nameof(PortFirewallAddAction)}] no <Solution> given; skipped.");
                return;
            }

            // 长度：留空 = 用解本身的长度（对齐效果实现的语义）
            float rawLen = ConfigValue.ParseInRange(Length, Solution.Length, 0f, float.MaxValue, out bool lenOk);
            if (!lenOk)
                KELog.Warn($"[{nameof(PortFirewallAddAction)}] invalid <Length> '{Length}'; using the solution length.");

            float delay = ConfigValue.ParseInRange(AdditionalDelay, 0f, 0f, float.MaxValue, out bool dOk);
            if (!dOk)
                KELog.Warn($"[{nameof(PortFirewallAddAction)}] invalid <AdditionalDelay> '{AdditionalDelay}'; using 0.");

            PortHackV2Effects.addFirewall(os, target.idName, (int)rawLen, delay, Solution.Trim());
            TargetActionHelper.LogDone(nameof(PortFirewallAddAction), $"firewall set (solution length {(int)rawLen})", target);
        }
    }
}
