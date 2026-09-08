# 杂项

本页收录 **KernelExtensions** 中不属于自定义试炼、虚拟机攻击或飞机 Daemon 三大系统，但同样实用的自定义 Action、工具类、Harmony 补丁以及其它辅助功能。

---

## 主菜单水印

模组会在 Hacknet 主菜单左上角显示 **`+ KernelExtensions <版本号>`** 的动态彩虹流动文字。

- 位置在 ZeroDayToolKit 水印右侧，与其他模组水印不重叠。
- 颜色随时间平滑流动，无跳跃或回弹。
- 文字前缀 `+` 号，与社区其它模组风格一致。
- 版本号自动跟随 `KernelExtensions.ModVer`，无需手动更新。
- 扩展卸载时水印自动消失。

实现于 `MainMenuWatermarkPatch.cs`，利用 `FlowColorUtils` 生成颜色。

---

## 通用自定义 Action

以下 Action 并非专属于某个系统，可随时在动作文件中调用。详细用法及参数说明请参阅 **[自定义Action](./../components/actions.md)** 页面。

| 动作 | 功能简述 |
|------|----------|
| `PlaySound` | 播放扩展目录下的 WAV 音效文件。 |
| `TerminalWrite` | 向终端输出一行文本。 |
| `TerminalType` | 向终端逐字打印文本。 |
| `TerminalFocus` | 播放终端聚焦特效（全屏变暗 + 边框扩展）。 |
| `RenameNode` | 按节点 ID 重命名节点，修改即时生效并持久化到存档。 |

示例：

```xml
<PlaySound Path="Sounds/beep.wav" Volume="1" Pitch="0" Delay="1.5" DelayHost="cheat"/>
<TerminalWrite text="Hello, World!" />
<TerminalType text="逐字显示的消息" CharDelay="0.04" />
<TerminalFocus Duration="5.0" BorderDuration="2.0" FadeInDuration="0.5" />
<RenameNode NodeID="dhs" NewName="秘密基地" />
```

---

## 工具类

KernelExtensions 提供了一系列公开的工具类，供其它模组或扩展作者在代码中调用。详细说明请参阅 **[工具类](./../components/utility.md)** 页面。

| 工具类 | 用途 |
|--------|------|
| `ActionHelper` | 统一执行动作文件的静态方法。 |
| `MusicPathResolver` | 将配置中的音乐字符串解析为 `MusicManager` 可识别的路径。 |
| `SoundHelper` | 播放扩展内的 WAV 音效文件。 |
| `FlowColorUtils` | 提供基于时间流动的彩虹色计算，用于水印或其它动态颜色需求。 |

---

## Harmony 补丁

模组通过 Harmony 补丁对原版游戏做了若干增强，所有补丁均在加载时注入、卸载时自动移除。详细列表请参阅 **[Harmony补丁](./../components/harmony.md)** 页面。

主要补丁包括：
- `MainMenuWatermarkPatch`：主菜单彩虹水印。
- `OverlayPatches`：飞机高度计全局覆盖层绘制。
- `CrashModuleVMAttackPatch`：虚拟机攻击注入与错误消息替换。

---

## 已废弃或重定向的内容

以下组件原本属于杂项范畴，但现在已有专属页面，请直接访问对应页面：

- 自定义试炼相关动作：`FailTrial`、`RestoreCustomTrialNodes` → 见 **[自定义试炼系统](./../systems/custom-trial.md)**
- 虚拟机攻击动作：`LaunchVMAttack` → 见 **[VM攻击系统](./../systems/vm-attack.md)**
- 飞机相关动作：`AttackAircraft`、`UploadAircraftSysFile`、`ShowAircraftOverlay`、`HideAircraftOverlay` → 见 **[飞机Daemon系统](./../systems/aircraft.md)**

---

## 另请参阅

- [首页](./../index.md) – 返回主索引
- [Misc (English)](./../../en/guides/misc.md) – 英文版
- [自定义Action](./../components/actions.md) – 全部自定义动作详细参数
- [工具类](./../components/utility.md) – 工具类使用指南
- [Harmony补丁](./../components/harmony.md) – 补丁列表与技术细节