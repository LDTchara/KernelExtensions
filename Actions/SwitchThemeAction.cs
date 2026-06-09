using Hacknet;
using KernelExtensions.Patches;
using Pathfinder.Action;
using Pathfinder.Util;
using Pathfinder.Util.XML;
using System;

namespace KernelExtensions.Actions
{
    /// <summary>
    /// 切换主题，不改变面板布局。
    /// 用法与 SASwitchToTheme 相同，但 ChangeLayout 固定为 false。
    ///
    /// 示例：
    /// <code>
    /// <!-- 基本用法 -->
    /// <SwitchToThemeKeepLayout ThemePathOrName="HacknetMint" />
    ///
    /// <!-- 指定闪烁时长 -->
    /// <SwitchToThemeKeepLayout ThemePathOrName="HacknetMint" FlickerInDuration="1.5" />
    ///
    /// <!-- 自定义主题 -->
    /// <SwitchToThemeKeepLayout ThemePathOrName="Themes/MyTheme.xml" FlickerInDuration="2" />
    ///
    /// <!-- 延迟执行 -->
    /// <SwitchToThemeKeepLayout ThemePathOrName="HacknetPurple" Delay="2" DelayHost="cheat" />
    /// </code>
    ///
    /// 只改变颜色，不改变布局。如需连布局一起改，请用原版 SASwitchToTheme。
    ///
    /// 参数：
    ///   ThemePathOrName   (string, 必填) 主题名（HacknetMint 等）或自定义主题文件路径
    ///   FlickerInDuration (float, 可选)  闪烁时长（秒），默认 2
    ///   Delay             (float, 可选)  延迟执行秒数
    ///   DelayHost         (string, 可选) 延迟宿主节点 ID
    /// </summary>
    public class SwitchThemeAction : DelayablePathfinderAction
    {
        [XMLStorage] public string ThemePathOrName;
        [XMLStorage] public float FlickerInDuration = 2f;

        public override void Trigger(OS os)
        {
            if (string.IsNullOrEmpty(ThemePathOrName)) return;

            OSTheme ostheme = OSTheme.Custom;
            if (!Enum.TryParse<OSTheme>(ThemePathOrName, true, out ostheme))
                ostheme = OSTheme.Custom;

            PhaseSwiftLayoutPatch.SkipLayoutChange = true;
            os.EffectsUpdater.StartThemeSwitch(
                FlickerInDuration,
                ostheme,
                os,
                (ostheme == OSTheme.Custom) ? ThemePathOrName : null
            );
            // 闪烁结束后恢复标志
            os.delayer.Post(ActionDelayer.Wait(FlickerInDuration + 0.15f), () =>
            {
                PhaseSwiftLayoutPatch.SkipLayoutChange = false;
            });
        }

        public override void LoadFromXml(ElementInfo info)
        {
            base.LoadFromXml(info);
        }
    }
}
