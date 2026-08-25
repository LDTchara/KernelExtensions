using Hacknet;
using KernelExtensions.Managers;
using KernelExtensions.Modules;
using Pathfinder.Action;
using Pathfinder.Util;

namespace KernelExtensions.Actions.PhaseSwift
{
    /// <summary>
    /// 初始化并启动 PhaseSwiftManager。
    /// 合并了 Initialize（加载配置）和 Start（启动音频 + 应用场景）两个步骤。
    ///
    /// 用法：<PhaseSwiftInit ConfigName="MyConfig" />
    ///
    /// 参数：
    ///   ConfigName  (string, 可选) 配置文件名（不含路径和扩展名），默认 "Default"
    ///   配置文件位于 ExtensionRoot/PhaseSwift/{ConfigName}.xml
    /// </summary>

    public class PhaseSwiftInitAction : DelayablePathfinderAction
    {
        [XMLStorage] public string ConfigName = "Default";

        public override void Trigger(OS os)
        {
            if (string.IsNullOrEmpty(ConfigName)) ConfigName = "Default";
            PhaseSwiftManager.Initialize(os, ConfigName);
            PhaseSwiftManager.Start();
            // 添加 Exe flag 以便下次启动时 Exe 自动检测到此配置
            string flag = "PhaseSwift_" + ConfigName;
            if (!os.Flags.HasFlag(flag))
            {
                os.Flags.AddFlag(flag);
                os.threadedSaveExecute(true);
            }
        }
    }
}