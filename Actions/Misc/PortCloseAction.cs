using Hacknet;
using KernelExtensions.Utilities;
using Pathfinder.Port;
using Pathfinder.Util;

namespace KernelExtensions.Actions.Misc
{
    /// <summary>
    /// 在目标机上**关闭**一个或多个端口。
    ///
    /// XML 用法：
    ///   &lt;PortCloseAction Ports="ssh,ftp" /&gt;
    ///
    /// Ports      ：端口协议名，逗号或分号分隔（必填）。
    /// TargetComp ：目标机 idName；留空 / NONE = 当前连接的节点。
    ///
    /// 端口不在目标机上时逐个 Warn 并跳过。
    /// </summary>
    public class PortCloseAction : KEAction
    {
        [XMLStorage] public string Ports;
        [XMLStorage] public string TargetComp;

        public override void Trigger(OS os)
        {
            if (!TargetActionHelper.TryResolve(os, TargetComp, nameof(PortCloseAction), out Computer target)) return;

            List<string> protocols = TargetActionHelper.ParseProtocolList(Ports);
            if (protocols.Count == 0)
            {
                KELog.WarnT($"[{nameof(PortCloseAction)}] no <Ports> given; skipped.");
                return;
            }

            foreach (string p in protocols)
            {
                if (target.GetPortState(p) == null)
                {
                    KELog.WarnT($"[{nameof(PortCloseAction)}] port '{p}' is not present on '{target.idName}'; skipped.");
                    continue;
                }
                target.closePort(p, os.thisComputer.ip);
            }

            TargetActionHelper.LogDone(nameof(PortCloseAction), $"closed [{string.Join(", ", protocols)}]", target);
        }
    }
}
