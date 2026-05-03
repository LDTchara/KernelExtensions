using Hacknet;
using Pathfinder.Action;

namespace KernelExtensions.AirCraft.Actions
{
    /// <summary>
    /// 关闭全局高度计覆盖层。
    /// </summary>
    public class HideAircraftOverlay : PathfinderAction
    {
        public override void Trigger(object os_obj)
        {
            GlobalAircraftOverlayManager.IsOverlayActive = false;
            GlobalAircraftOverlayManager.CurrentFlightDaemon = null;

            OS os = os_obj as OS;
            os?.write("Aircraft overlay deactivated.");
        }
    }
}
