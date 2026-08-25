using Hacknet;
using KernelExtensions.Executables;
using KernelExtensions.Managers;
using KernelExtensions.Modules;
using Pathfinder.Action;
using Pathfinder.Util;
using Pathfinder.Util.XML;

namespace KernelExtensions.Actions.PhaseSwift
{
    /// <summary>
    /// 切换 PhaseSwift 场景。
    /// 触发音频交叉淡化、拓扑替换、可见性更新、主题切换、OnSwitch 动作。
    /// 音乐组切换请使用 PhaseSwiftMusic Action。
    ///
    /// 用法：
    ///   <PhaseSwiftScene TargetScene="1" />
    ///   <PhaseSwiftScene TargetScene="0" FadeDuration="2.0" Theme="HacknetMint" />
    ///
    /// 参数：
    ///   TargetScene   (int, 必填)    目标场景索引（从 0 开始）
    ///   FadeDuration  (float, 可选)  音乐渐变时长（秒），不填则用配置默认
    ///   Theme         (string, 可选) 覆盖主题（预设名或自定义路径）
    /// </summary>
    public class PhaseSwiftSceneAction : DelayablePathfinderAction
    {
        [XMLStorage] public int TargetScene;
        [XMLStorage] public float FadeDuration = -1f;
        [XMLStorage] public string Theme;

        public override void Trigger(OS os)
        {
            if (PhaseSwiftManager.IsInitialized)
            {
                PhaseSwiftManager.SwitchToScene(TargetScene, FadeDuration >= 0 ? FadeDuration : null, Theme);
                return;
            }
            var exe = PhaseSwiftExe.CurrentInstance;
            if (exe != null)
            {
                exe.SwitchToScene(TargetScene, FadeDuration >= 0 ? FadeDuration : null, Theme);
            }
        }

        public override void LoadFromXml(ElementInfo info)
        {
            base.LoadFromXml(info);
        }
    }
}
