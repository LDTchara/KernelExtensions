using Hacknet;
using Hacknet.Extensions;
using KernelExtensions.Managers;
using KernelExtensions.Utilities;
using Pathfinder.Action;
using Pathfinder.Util;

namespace KernelExtensions.Actions
{
    /// <summary>
    /// 启动一个 Clock 定时器（剧情资产，分散 XML 文件）。
    ///
    /// 用法：
    ///   &lt;ClockStart Filepath="Clocks/traceFlash.xml" /&gt;
    ///
    /// Clock 文件结构（详见 ClockManager）：
    ///   &lt;Clock ID="traceFlash" Interval="5.0" Times="3" Duration="60" OnComplete="Clocks/done.xml"&gt;
    ///       &lt;Actions&gt;...&lt;/Actions&gt;
    ///   &lt;/Clock&gt;
    /// </summary>
    public class ClockStartAction : DelayablePathfinderAction
    {
        [XMLStorage] public string Filepath;

        public override void Trigger(OS os)
        {
            try
            {
                string extRoot = ExtensionLoader.ActiveExtensionInfo?.FolderPath?.Replace('\\', '/');
                ClockManager.Start(os, Filepath, extRoot);
            }
            catch (Exception ex)
            {
                KELog.Error("[ClockStart] Trigger failed: " + ex.Message);
            }
        }
    }
}
