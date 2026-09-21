# 自定义 Action

KernelExtensions 提供了一系列自定义 Action，可在任何动作文件中调用。所有路径均相对于扩展根目录。

## 通用动作

| 动作 | 描述 | 示例 |
|------|------|------|
| `PlaySound` | 播放扩展目录下的 WAV 音效文件。 | `<PlaySound Path="Sounds/beep.wav" Volume="1" Pitch="0" Delay="1.5" DelayHost="cheat"/>` |
| `TerminalWrite` | 向终端输出一行文本。 | `<TerminalWrite Text="Hello, World!" />` |
| `TerminalType` | 向终端逐字打印文本——**不自动换行**，从当前光标处追加（语义近 HackerScript 的 `write`；可用多条在同一行分段输出不同速度）。 | `<TerminalType Text="逐字显示的消息" CharDelay="0.04" />` |
| `TerminalFocus` | 播放终端聚焦特效（全屏变暗 + 边框扩展）。 | `<TerminalFocus Duration="5.0" BorderDuration="2.0" FadeInDuration="0.5" />` |
| `RenameNode` | 按节点 ID 重命名节点，修改即时生效并持久化到存档。 | `<RenameNode NodeID="dhs" NewName="秘密基地" />` |

## 试炼相关动作

| 动作 | 描述 | 示例 |
|------|------|------|
| `FailTrial` | 强制当前正在运行的 CustomTrialExe 试炼立即失败。 | `<FailTrial />` 或 `<FailTrial Delay="3.0" DelayHost="cheat" />` |
| `RestoreCustomTrialNodes` | 恢复之前试炼中删除的节点，并以动画特效逐个显示。 | `<RestoreCustomTrialNodes ConfigName="ExampleTrial" />` |

## 虚拟机攻击相关动作

| 动作 | 描述 | 示例 |
|------|------|------|
| `LaunchVMAttack` | 启动指定的 VM 攻击。 | `<LaunchVMAttack ConfigName="MyAttack" />` |

## 飞机 Daemon 相关动作

| 动作 | 描述 | 示例 |
|------|------|------|
| `AttackAircraft` | 攻击指定飞机，使其进入关键固件故障并坠落。 | `<AttackAircraft NodeID="dair_crash" FallDuration="60" />` |
| `UploadAircraftSysFile` | 向目标计算机写入合法的 `747FlightOps.dll` 以便修复飞机。 | `<UploadAircraftSysFile NodeID="dair_crash" />` |
| `ShowAircraftOverlay` | 激活指定飞机的全局高度计覆盖层。 | `<ShowAircraftOverlay NodeID="dair_crash" />` |
| `HideAircraftOverlay` | 关闭全局高度计覆盖层。 | `<HideAircraftOverlay />` |

---

## 节点连接控制相关动作

| 动作 | 描述 | 示例 |
|------|------|------|
| `LinkControlReset` | 把指定电脑的链接恢复为组织基线（丢弃运行时所有临时增删）。 | `<LinkControlReset SourceComp="playerComp" />` |
| `LinkControlAdd` | 运行时临时添加一条链接（不写入基线）。 | `<LinkControlAdd SourceComp="playerComp" TargetComp="jmail" />` |
| `LinkControlRemove` | 运行时临时删除一条链接（不写入基线）。 | `<LinkControlRemove SourceComp="playerComp" TargetComp="jmail" />` |

> 三者共享 org 基线：**开局时**对电脑现有链接做一次快照（内容 XML 的 `<dlink>` 即由此进入基线），
> 随存档落盘为 `<OrgLinks>`；`Add`/`Remove` 只改运行时 links，可用 `Reset` 还原。
> 属性名**大小写不敏感**（`KEAction` 基类统一处理；仍推荐按 PascalCase 书写）。
> 节点不存在、缺 `TargetComp` 等情况会输出错误日志并跳过，不会崩溃。
> 注：`<OrgLinks>` 只出现在存档里；内容侧声明初始链接请用原版 `<dlink>`。

---

## 其它动作

| 动作 | 描述 | 示例 |
|------|------|------|
| `FlashScreen` | UI 闪烁：按指定颜色闪烁并线性渐隐回当前主题默认色。 | `<FlashScreen Color="Red" Duration="2.0" />` |
| `SwitchToThemeKeepLayout` | 切换主题但**保持面板布局**不变（只改颜色）。 | `<SwitchToThemeKeepLayout ThemePathOrName="HacknetMint" FlickerInDuration="1.5" />` |
| `BreakHeart` | 显式触发目标节点 PorthackHeartDaemon 的心碎序列。 | `<BreakHeart NodeID="heart" OnComplete="Actions/HeartBroken" />` |
| `BlockNode` | 向当前（或指定）PhaseSwift 场景添加运行时黑名单节点。 | `<BlockNode NodeId="A" SceneIndex="0" />` |
| `UnblockNode` | 移除运行时黑名单节点。 | `<UnblockNode NodeId="A" />` |
| `PhaseSwiftInit` / `Scene` / `Music` / `Stop` / `FadeOut` | 相位穿梭系统的启动、切场景、切音乐组与退出。 | 见[相位穿梭系统](./../systems/phase-swift.md) |

- `FlashScreen` 的 `Color` 支持 Hex、数值 RGB、命名色与动态色（如 `LDTchara`）；`Duration` 默认 `2.0`（非正值 = 立即恢复默认色）；`PlaySound="true"` 可在闪烁同时播放警告音效。重复触发为**刷新**语义，不叠加。
- `SwitchToThemeKeepLayout` 只改颜色不动布局；需要连布局一起改请用原版 `SASwitchToTheme`。
- `BreakHeart` 除 `NodeID` 外的参数均为**覆盖项**：不写 = 用 daemon 自身配置；写 `NONE` 或留空 = 显式禁用（如 `Music="NONE"` 表示不切歌）。

---

## 延迟执行

大多数动作支持 `Delay` 和 `DelayHost` 属性用于延迟执行。  
- `Delay`：延迟的秒数。  
- `DelayHost`：提供延迟服务的主机 ID（需拥有 `FastActionHost` 守护进程）。  
若 `Delay` 为 0 或负数，动作立即执行。

---

## 另请参阅

- [首页](./../index.md) – 返回主索引
- [Actions (English)](./../../en/components/actions.md) – 英文版
- [相位穿梭系统（PhaseSwift）](./../systems/phase-swift.md)
- [自定义试炼系统](./../systems/custom-trial.md)  
- [VM攻击系统](./../systems/vm-attack.md)  
- [飞机Daemon系统](./../systems/aircraft.md)
- [自定义定时器系统（Clock）](./../systems/clock.md)
- [自定义标题横幅（ShowTitle）](./../systems/title-banner.md)
- [自定义全屏警告特效（ScreenBleed）](./../systems/screen-bleed.md)
- [自定义结局系统（StartEnding）](./../systems/custom-ending.md)