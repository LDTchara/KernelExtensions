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
- 作用：把配置里的音乐字符串解析成 `MusicManager` 能加载的路径。纯文件名的查找顺序：
  ① 扩展根目录 → ② 扩展 `Music/` → ③ `Content/DLC/Music` → ④ 视为原版音乐名（`Content/Music`）。
- **含路径分隔符时**（如 `Music/Bit(Ending)`）：先检查扩展目录下是否真有该文件——
  有则按扩展内路径返回；**没有则原样交还原版**，所以带路径的写法也能指向原版曲。
- 返回值**保留调用方写的扩展名**（不主动剥离 `.ogg`）；省略 `.ogg` 时由 FNA 的 `SongReader.Normalize` 补全。

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