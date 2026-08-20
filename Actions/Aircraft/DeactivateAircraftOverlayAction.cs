using Hacknet;
using KernelExtensions.Modules;
using KernelExtensions.Utility;
using Pathfinder.Action;

namespace KernelExtensions.Actions.Aircraft
{
    /// <summary>
    /// 关闭全局高度计覆盖层。
    /// </summary>
    public class HideAircraftOverlay : DelayablePathfinderAction
    {
        public override void Trigger(OS os)
        {
            var fd = GlobalAircraftOverlayManager.CurrentFlightDaemon;
            GlobalAircraftOverlayManager.IsOverlayActive = false;
            GlobalAircraftOverlayManager.CurrentFlightDaemon = null;

            // 覆盖层关闭：若 daemon 空闲则停止持续更新（坠机/固件重载中不打断）
            fd?.UnsubscribeIfIdle();

            // 成功状态写入 KELog（开发日志），不刷玩家终端
            KELog.Info("Aircraft overlay deactivated.");
        }
    }
}
