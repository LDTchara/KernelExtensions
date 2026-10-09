using Hacknet;
using KernelExtensions.Configs;
using KernelExtensions.Daemons;
using KernelExtensions.Utilities;
using Pathfinder.Action;
using Pathfinder.Util;

namespace KernelExtensions.Actions.Aircraft.Debug
{
    /// <summary>
    /// 【调试】把所有 FlightDaemon 的运行时状态打到玩家终端 —— **不连接目标节点也能观测**。
    /// <para>
    /// 仅在 <c>KE-Config.xml</c> 的 <c>&lt;Debug&gt;true</c> 时注册（见主入口），
    /// 因此不会成为事实上的公开 API；正式内容里写了也不生效。
    /// </para>
    /// <example>
    /// <code>
    /// &lt;FlightDaemonStatus /&gt;
    /// &lt;FlightDaemonStatus NodeId="aircraft" /&gt;   &lt;!-- 只列一个（匹配 idName 或 ip）--&gt;
    /// </code>
    /// </example>
    /// </summary>
    public class FlightDaemonStatusAction : KEAction
    {
        /// <summary>可选：只输出该节点（匹配 <c>idName</c> 或 <c>ip</c>）。</summary>
        [XMLStorage]
        public string NodeId;

        public override void Trigger(OS os)
        {
            if (!ConfigLoader.Debug)
            {
                os.write("[FlightDaemon] FlightDaemonStatus is debug-only; enable <Debug>true in KE-Config.xml.");
                return;
            }

            var map = FlightDaemon.CompToDaemons;
            if (map == null || map.Count == 0)
            {
                os.write("[FlightDaemon] no daemon registered.");
                return;
            }

            int shown = 0;
            foreach (var kv in map)
            {
                var comp = kv.Key;
                var d = kv.Value;
                if (d == null) continue;

                string idName = comp?.idName ?? "?";
                string ip = comp?.ip ?? "?";
                if (!string.IsNullOrWhiteSpace(NodeId)
                    && !string.Equals(idName, NodeId, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(ip, NodeId, StringComparison.OrdinalIgnoreCase))
                    continue;

                shown++;
                os.write($"-- FlightDaemon {idName} ({ip}) --");
                os.write($"   altitude {d.CurrentAltitude:F1}   height(H) {d.H:F1}");
                os.write($"   airspeed {d.currentAirspeed:F1}   rateOfClimb {d.rateOfClimb:F4}");
                os.write($"   progress {d.FlightProgress:F2}/6   fallingFor {d.timeFallingFor:F1}s");
                os.write($"   crashed {d.IsCrashed}   rescued {d.hasBeenRescued}   pilotAlerted {d.PilotAlerted}");
                os.write($"   criticalFirmware {d.IsInCriticalFirmwareFailure}   reloading {d.IsReloadingFirmware} ({d.firmwareReloadProgress:F2})");
                os.write($"   subscribed {d.IsSubscribedForUpdates}");
                os.write($"   mapOrigin ({d.mapOrigin.X:F4},{d.mapOrigin.Y:F4})   mapDest ({d.mapDestV.X:F4},{d.mapDestV.Y:F4})");
            }

            if (shown == 0)
                os.write($"[FlightDaemon] no daemon matched '{NodeId}'.");
        }
    }
}
