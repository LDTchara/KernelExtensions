using Hacknet;
using Pathfinder.Action;
using Pathfinder.Util.XML;

namespace KernelExtensions.Actions.CustomTrial
{
    /// <summary>
    /// 强制当前正在运行的 CustomTrialExe 试炼立即失败。
    /// 用法：<FailTrial />或<FailTrial Delay="3.0" DelayHost="delayhost" />
    /// 支持 Delay 和 DelayHost 属性进行延迟执行。
    /// </summary>
    public class FailTrialAction : DelayablePathfinderAction
    {
        public override void Trigger(OS os)
        {
            // 获取当前活动的试炼实例
            var trial = Executables.CustomTrialExe.CurrentInstance;
            if (trial != null && !trial.isExiting)
            {
                trial.ForceFail();
            }
        }
    }
}