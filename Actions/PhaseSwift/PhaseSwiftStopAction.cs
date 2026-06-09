using Hacknet;
using KernelExtensions.Executables;
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
            PhaseSwiftManager.Stop(mode);
            // 由于主动 Stop 会清空音频和场景状态，Exe 不再需要显示 Complete 动画
            if (PhaseSwiftExe.CurrentInstance != null && !PhaseSwiftExe.CurrentInstance.isExiting)
            {
                PhaseSwiftExe.CurrentInstance.isExiting = true;
                PhaseSwiftExe.CurrentInstance.Result = Pathfinder.Executable.CompletionResult.Success;
            }
        }
    }
}
