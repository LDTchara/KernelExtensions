using Hacknet;
using KernelExtensions.Utilities;
using Pathfinder.Port;
using Pathfinder.Util;

namespace KernelExtensions.Actions.Misc
{
    /// <summary>
    /// 在目标机上**打开**一个或多个端口。
    ///
    /// XML 用法：
    ///   &lt;PortOpenAction Ports="ssh,ftp" /&gt;
    ///   &lt;PortOpenAction Ports="ssh" TargetComp="targetNode" /&gt;
    ///
    /// Ports      ：端口协议名，逗号或分号分隔（必填）。
    /// TargetComp ：目标机 idName；留空 / NONE = 当前连接的节点。
    ///
    /// 端口不在目标机上时逐个 Warn 并跳过，不影响同一次调用里的其它端口。
    /// </summary>
    public class PortOpenAction : KEAction
    {
        [XMLStorage] public string Ports;
        [XMLStorage] public string TargetComp;

        public override void Trigger(OS os)
        {
            if (!TargetActionHelper.TryResolve(os, TargetComp, nameof(PortOpenAction), out Computer target)) return;

            List<string> protocols = TargetActionHelper.ParseProtocolList(Ports);
            if (protocols.Count == 0)
            {
                KELog.Warn($"[{nameof(PortOpenAction)}] no <Ports> given; skipped.");
                return;
            }

            foreach (string p in protocols)
            {
                if (target.GetPortState(p) == null)
                {
                    KELog.Warn($"[{nameof(PortOpenAction)}] port '{p}' is not present on '{target.idName}'; skipped.");
                    continue;
                }
                target.openPort(p, os.thisComputer.ip);
            }

            TargetActionHelper.LogDone(nameof(PortOpenAction), $"opened [{string.Join(", ", protocols)}]", target);
        }
    }
}
