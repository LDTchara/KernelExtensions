using Hacknet;
using KernelExtensions.Executables;
using KernelExtensions.Managers;
using KernelExtensions.Modules;
using Pathfinder.Action;
using Pathfinder.Util;

namespace KernelExtensions.Actions.PhaseSwift
{
    /// <summary>
    /// 停止 PhaseSwiftManager 并标记 Exe 完成。
    /// 清理音频 → 恢复原始拓扑 → 根据 FinishMode 处理节点可见性。
    ///
    /// 可选参数：
    ///   FinishMode  (string, 可选) 完成后的节点处理方式，优先级大于配置文件中的模式。
    ///     不填 → 使用 Config 中的 FinishMode 设置。
    ///     "none"    — 全隐藏：隐藏所有 PS 受控节点。
    ///     "full"    — 全保留：所有场景的节点保持可见。
    ///     "scene_N" — 保留场景 N 的节点可见（如 scene_2）。
    ///
    /// 用法：<PhaseSwiftStop />
    ///       <PhaseSwiftStop FinishMode="full" />
    /// </summary>
    public class PhaseSwiftStopAction : DelayablePathfinderAction
    {
        /// <summary>完成后的节点处理模式。none=全隐 full=全留 scene_N=留场景N。不填则用Config设置。</summary>
        [XMLStorage]
        public string FinishMode = null;

        public override void Trigger(OS os)
        {
            string mode = FinishMode ?? PhaseSwiftManager.Config?.FinishMode ?? "none";
            // 结束剧情：移除 PhaseSwift_{ConfigName} flag，防止读档后 AutoRestore 误恢复 PS。
            // 只在这里清 —— 其他 Stop 调用（exe 被杀/扩展卸载）是清理而非剧情结束，
            // 清理它们会破坏"杀 exe 后读档剧情继续"的语义。
            string psFlag = os.Flags.GetFlagStartingWith("PhaseSwift_");
            if (!string.IsNullOrEmpty(psFlag))
                os.Flags.RemoveFlag(psFlag);
            PhaseSwiftManager.Stop(mode);
            // 让 Exe 进入 Completing 状态，显示 3 秒完成文本后自动退出
            if (PhaseSwiftExe.CurrentInstance != null && !PhaseSwiftExe.CurrentInstance.isExiting)
            {
                PhaseSwiftExe.CurrentInstance.IsComplete = true;
            }
        }
    }
}
