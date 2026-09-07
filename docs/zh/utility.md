# 工具类

KernelExtensions 提供了一组静态工具类，供扩展开发者在代码中使用。

## ActionHelper

- 路径：`KernelExtensions.Utility.ActionHelper`
- 方法：`ExecuteActionFile(OS os, string actionFilePath, string extensionRoot)`
- 作用：加载并执行一个动作文件，统一了 `CustomTrialExe` 和 VM 攻击系统的动作执行逻辑。

## MusicPathResolver

- 路径：`KernelExtensions.Utility.MusicPathResolver`
- 方法：`ResolveMusicPath(string musicPath, string extensionRoot)`
- 作用：将配置中的音乐字符串解析为 `MusicManager.transitionToSong` 能识别的路径。支持纯文件名、扩展目录、DLC 音乐和原版音乐。

## SoundHelper

- 路径：`KernelExtensions.Utility.SoundHelper`
- 方法：`PlaySound(OS os, string soundPath, float volume, float pitch, float pan)`
- 作用：播放扩展目录内的 WAV 音效文件。

## FlowColorUtils

- 路径：`KernelExtensions.Utility.FlowColorUtils`
- 方法：`GetFlowingRainbowColor(float position, float baseTime)`、`HslToRgbSimple(double h, double s, double l)`
- 作用：提供基于时间流动的彩虹色计算，用于主菜单水印及其他需要动态彩虹色的场景。

---

## 另请参阅

- [首页](./index.md) – 返回主索引
- [Utility Classes (English)](./../en/utility.md) – 英文版