using Hacknet;
using KernelExtensions.Utilities;
using Pathfinder.Util;

namespace KernelExtensions.Actions.Misc
{
    /// <summary>
    /// **重置**目标机的代理：过载进度清零、代理重新生效，并结束正在跑的端口破解进程。
    ///
    /// XML 用法：
    ///   &lt;PortProxyResetAction /&gt;
    ///
    /// TargetComp ：目标机 idName；留空 / NONE = 当前连接的节点。
    ///
    /// 与 <see cref="PortProxySolveAction"/> 的区别：那个是"代理被解掉"（<c>proxyActive=false</c>），
    /// 这个是"代理还在、但进度归零"（对手把玩家的过载进度打回去了）。
    /// </summary>>
    public class PortProxyResetAction : KEAction
    {
        [XMLStorage] public string TargetComp;

        public override void Trigger(OS os)
        {
            if (!TargetActionHelper.TryResolve(os, TargetComp, nameof(PortProxyResetAction), out Computer target)) return;

            if (!target.hasProxy)
            {
                KELog.Warn($"[{nameof(PortProxyResetAction)}] '{target.idName}' has no proxy; skipped.");
                return;
            }

            PortHackV2Effects.ResetProxy(os, target.idName);
            TargetActionHelper.LogDone(nameof(PortProxyResetAction), "proxy overload progress reset", target);
        }
    }
}
