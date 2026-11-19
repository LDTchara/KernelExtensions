using Hacknet;
using KernelExtensions.Utilities;
using Pathfinder.Util;

namespace KernelExtensions.Actions.PortHackV2
{
    /// <summary>
    /// **解除**目标机的代理（<c>proxyActive = false</c>），可带延迟；
    /// 同时把正在跑的 <c>PortHackExe</c> 标记为待移除（对齐原版"代理没了、破解进程随之结束"）。
    ///
    /// XML 用法：
    ///   &lt;ProxySolve /&gt;
    ///   &lt;ProxySolve Delay="11" /&gt;
    ///
    /// TargetComp ：目标机 idName；留空 / NONE = 当前连接的节点。
    /// Delay      ：**基类提供**的"延迟多久才执行本动作"，留空 = 立即执行。
    ///
    /// ⚠️ 若只想"把代理的过载进度清零、让它重新跑"，用 <see cref="ProxyResetAction"/>。
    /// </summary>
    public class ProxySolveAction : KEAction
    {
        [XMLStorage] public string TargetComp;

        public override void Trigger(OS os)
        {
            if (!TargetActionHelper.TryResolve(os, TargetComp, nameof(ProxySolveAction), out Computer target)) return;

            if (!target.hasProxy)
            {
                KELog.WarnT($"[{nameof(ProxySolveAction)}] '{target.idName}' has no proxy; skipped.");
                return;
            }

            // 延迟交给基类（Delay 属性）处理，这里一律立即解
            PortHackV2Effects.solveProxy(os, target.idName, 0f);
            TargetActionHelper.LogDone(nameof(ProxySolveAction), "proxy solved", target);
        }
    }
}
