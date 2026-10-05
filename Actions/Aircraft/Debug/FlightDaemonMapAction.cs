using Hacknet;
using KernelExtensions.Configs;
using KernelExtensions.Daemons;
using KernelExtensions.Utilities;
using Microsoft.Xna.Framework;
using Pathfinder.Action;
using Pathfinder.Util;

namespace KernelExtensions.Actions.Aircraft.Debug
{
    /// <summary>
    /// 【调试】随时改 FlightDaemon 的航线起终点（归一化坐标），现场调参看效果。
    /// <para>
    /// 只改**运行时值**（<c>mapOrigin</c> / <c>mapDestV</c>）—— 下一次 <c>loadInit</c>
    /// 会被节点 XML 的配置覆盖，所以调好之后要把数值写回节点 XML 的
    /// <c>MapOriginX/Y</c> 与 <c>MapDestX/Y</c>。
    /// </para>
    /// <para>仅在 <c>&lt;Debug&gt;true</c> 时注册。每个坐标不写 = 不改（逐项独立）。</para>
    /// <example>
    /// <code>
    /// &lt;FlightDaemonMap OriginX="0.2" OriginY="0.7" DestX="0.8" DestY="0.3" /&gt;
    /// &lt;FlightDaemonMap NodeId="aircraft" OriginX="0.1" /&gt;
    /// </code>
    /// </example>
    /// </summary>
    public class FlightDaemonMapAction : KEAction
    {
        /// <summary>可选：只改该节点（匹配 <c>idName</c> 或 <c>ip</c>）；不写 = 全部 daemon。</summary>
        [XMLStorage]
        public string NodeId;

        private const float Unset = -1f;

        [XMLStorage] public float OriginX = Unset;
        [XMLStorage] public float OriginY = Unset;
        [XMLStorage] public float DestX = Unset;
        [XMLStorage] public float DestY = Unset;

        public override void Trigger(OS os)
        {
            if (!ConfigLoader.Debug)
            {
                os.write("[FlightDaemon] FlightDaemonMap is debug-only; enable <Debug>true in KE-Config.xml.");
                return;
            }

            var map = FlightDaemon.CompToDaemons;
            if (map == null || map.Count == 0)
            {
                os.write("[FlightDaemon] no daemon registered.");
                return;
            }

            int changed = 0;
            foreach (var kv in map)
            {
                var comp = kv.Key;
                var d = kv.Value;
                if (d == null) continue;

                if (!string.IsNullOrWhiteSpace(NodeId)
                    && !string.Equals(comp?.idName, NodeId, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(comp?.ip, NodeId, StringComparison.OrdinalIgnoreCase))
                    continue;

                // 逐项独立：任一项没写（保持 -1）就不动那一项
                float ox = OriginX >= 0f ? MathHelper.Clamp(OriginX, 0f, 1f) : d.mapOrigin.X;
                float oy = OriginY >= 0f ? MathHelper.Clamp(OriginY, 0f, 1f) : d.mapOrigin.Y;
                float dx = DestX >= 0f ? MathHelper.Clamp(DestX, 0f, 1f) : d.mapDestV.X;
                float dy = DestY >= 0f ? MathHelper.Clamp(DestY, 0f, 1f) : d.mapDestV.Y;

                d.mapOrigin = new Vector2(ox, oy);
                d.mapDestV = new Vector2(dx, dy);
                changed++;

                os.write($"[FlightDaemon] {comp?.idName} mapOrigin=({ox:F4},{oy:F4}) mapDest=({dx:F4},{dy:F4})");
            }

            if (changed == 0)
                os.write($"[FlightDaemon] no daemon matched '{NodeId}'.");
        }
    }
}
