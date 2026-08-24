using System;
using Hacknet;
using Hacknet.Extensions;
using KernelExtensions.Modules;
using KernelExtensions.Utility;
using Pathfinder.Action;
using Pathfinder.Util;

namespace KernelExtensions.Actions
{
    /// <summary>
    /// 停止一个正在运行的 Clock 定时器。
    ///
    /// 用法（二选一，同时提供时优先 ClockID）：
    ///   &lt;ClockStop ClockID="traceFlash" /&gt;                — 按 ID 停止（推荐）
    ///   &lt;ClockStop Filepath="Clocks/traceFlash.xml" /&gt;    — 按路径停止（便利通道）
    ///
    /// 手动停止不触发 OnComplete（OnComplete 只在 Times/Duration 耗尽自动停止时执行）。
    /// 停止未知 ID/路径 → 静默忽略。
    /// </summary>
    public class ClockStopAction : DelayablePathfinderAction
    {
        [XMLStorage] public string ClockID;
        [XMLStorage] public string Filepath;

        public override void Trigger(OS os)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(ClockID))
                {
                    ClockManager.StopByID(os, ClockID);
                    return;
                }
                if (!string.IsNullOrWhiteSpace(Filepath))
                {
                    string extRoot = ExtensionLoader.ActiveExtensionInfo?.FolderPath?.Replace('\\', '/');
                    ClockManager.StopByPath(os, Filepath, extRoot);
                    return;
                }
                os.write("[ClockStop] requires ClockID or Filepath");
            }
            catch (Exception ex)
            {
                KELog.Error("[ClockStop] Trigger failed: " + ex.Message);
            }
        }
    }
}
