# 工具类

KernelExtensions 提供了一组静态工具类，供**模组作者**在代码中调用。

!!! info "本页写给谁看"
    这些是 **C# 代码级 API**——面向的是写插件/模组的人。
    **纯扩展作者通常用不到本页**：扩展侧的一切都通过 XML 完成（动作、配置、文件布局），
    不需要写代码。只有当扩展作者自己做了私有插件时，才会同时扮起模组作者的角色。

## ActionHelper

- 路径：`KernelExtensions.Utilities.ActionHelper`
- 方法：`ExecuteActionFile(OS os, string actionFilePath, string extensionRoot)`
- 作用：加载并执行一个动作文件，统一了 `CustomTrialExe` 和 VM 攻击系统的动作执行逻辑。

## MusicPathResolver

- 路径：`KernelExtensions.Utilities.MusicPathResolver`
- 方法：`ResolveMusicPath(string musicPath, string extensionRoot)`
- 作用：将配置中的音乐字符串解析为 `MusicManager.transitionToSong` 能识别的路径。支持纯文件名、扩展目录、DLC 音乐和原版音乐。

## SoundHelper

- 路径：`KernelExtensions.Utilities.SoundHelper`
- 方法：`PlaySound(OS os, string soundPath, float volume, float pitch, float pan)`
- 作用：播放扩展目录内的 WAV 音效文件。

## FlowColorHelper

- 路径：`KernelExtensions.Utilities.FlowColorHelper`
- 方法：`GetFlowingRainbowColor(float position, float baseTime)`、`HslToRgbSimple(double h, double s, double l)`
- 作用：提供基于时间流动的彩虹色计算，用于主菜单水印及其他需要动态彩虹色的场景。

---

## 另请参阅

- [首页](./../index.md) – 返回主索引
- [Utility Classes (English)](./../../en/components/utility.md) – 英文版