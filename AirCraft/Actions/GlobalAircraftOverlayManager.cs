using Hacknet;

namespace KernelExtensions.AirCraft.Actions
{
    /// <summary>
    /// 管理全局飞机高度计覆盖层的激活状态和目标飞行守护进程。
    /// </summary>
    public static class GlobalAircraftOverlayManager
    {
        /// <summary>当前是否显示覆盖层</summary>
        public static bool IsOverlayActive = false;

        /// <summary>当前被观察的 FlightDaemon（提供高度、速度等数据）</summary>
        public static Daemon.FlightDaemon CurrentFlightDaemon = null;
    }
}
