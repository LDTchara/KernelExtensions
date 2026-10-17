using Hacknet;
using KernelExtensions.Utilities;
using Pathfinder.Util;

namespace KernelExtensions.Actions.Misc
{
    /// <summary>
    /// 给目标机**加上代理**（会设置 <c>proxyActive = true</c>）。
    ///
    /// XML 用法：
    ///   &lt;PortProxyAddAction Time="10" /&gt;
    ///
    /// Time       ：代理的过载/破解所需时间（原版 <c>addProxy</c> 的 tick 数）；留空 = 10。
    /// TargetComp ：目标机 idName；留空 / NONE = 当前连接的节点。
    /// </summary>
    public class PortProxyAddAction : KEAction
    {
        [XMLStorage] public string Time;
        [XMLStorage] public string TargetComp;

        public override void Trigger(OS os)
        {
            if (!TargetActionHelper.TryResolve(os, TargetComp, nameof(PortProxyAddAction), out Computer target)) return;

            float t = ConfigValue.ParseInRange(Time, 10f, 0f, float.MaxValue, out bool ok);
            if (!ok)
                KELog.WarnT($"[{nameof(PortProxyAddAction)}] invalid <Time> '{Time}'; using 10.");

            PortHackV2Effects.addProxy(os, target.idName, (int)t);
            TargetActionHelper.LogDone(nameof(PortProxyAddAction), $"proxy added (time {(int)t})", target);
        }
    }
}
