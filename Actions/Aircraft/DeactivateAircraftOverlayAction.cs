using Hacknet;
using KernelExtensions.Modules;
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
            GlobalAircraftOverlayManager.IsOverlayActive = false;
            GlobalAircraftOverlayManager.CurrentFlightDaemon = null;

            os?.write("Aircraft overlay deactivated.");
        }
    }
}
