# 自定义 Action

KernelExtensions 共注册 **32 个**自定义 Action，可在任何动作文件中调用。本页分两部分：

- **一、与各系统相关的动作** —— 只给速查与去向，完整参数在各自的系统页
- **二、通用动作（系统未涵盖）** —— 完整参数与示例

所有路径均相对于扩展根目录。

---

## 一、与各系统相关的动作

| 系统 | 动作 | 用途 | 详情 |
|------|------|------|------|
| 自定义试炼 | `FailTrial` · `RestoreCustomTrialNodes` | 强制试炼失败 · 恢复被删节点 | [自定义试炼系统](./../systems/custom-trial.md) |
| 相位穿梭 | `PhaseSwiftInit` · `PhaseSwiftScene` · `PhaseSwiftMusic` · `PhaseSwiftStop` · `PhaseSwiftFadeOut` · `BlockNode` · `UnblockNode` | 启停、切场景、切音乐组、运行时黑名单节点 | [相位穿梭系统](./../systems/phase-swift.md) |
| VM 攻击 | `LaunchVMAttack` | 启动指定 VM 攻击 | [VM攻击系统](./../systems/vm-attack.md) |
| 飞机 Daemon | `AttackAircraft` · `UploadAircraftSysFile` · `ShowAircraftOverlay` · `HideAircraftOverlay` | 攻击 · 修复 · 高度计覆盖层 | [飞机Daemon系统](./../systems/aircraft.md) |
| 节点连接控制 | `LinkControlReset` · `LinkControlAdd` · `LinkControlRemove` | 运行时增删链接、恢复组织基线 | 见下方小节 |
| 定时器 | `ClockStart` · `ClockStop` | 启停自定义定时器 | [自定义定时器系统](./../systems/clock.md) |
| 节点图标 | `SetNodeIcon` | 设置节点图标 | [自定义节点图标系统](./../systems/node-icon.md) |
| 自定义结局 | `StartEnding` · `BreakHeart` | 触发结局 / 心碎序列 | [自定义结局系统](./../systems/custom-ending.md) |

### 节点连接控制（属性速查）

三者共享 **org 基线**：**开局时**对电脑现有链接做一次快照（内容 XML 的 `<dlink>` 即由此进入基线），
随存档落盘为 `<OrgLinks>`；`Add` / `Remove` 只改运行时 links，可用 `Reset` 还原。

| 动作 | 示例 |
|------|------|
| `LinkControlReset` | `<LinkControlReset SourceComp="playerComp" />` |
| `LinkControlAdd` | `<LinkControlAdd SourceComp="playerComp" TargetComp="jmail" />` |
| `LinkControlRemove` | `<LinkControlRemove SourceComp="playerComp" TargetComp="jmail" />` |

- 属性名**大小写不敏感**（`KEAction` 基类统一处理；仍推荐按 PascalCase 书写）
- 节点不存在、缺 `TargetComp` 等情况会输出错误日志并跳过，不会崩溃
- `<OrgLinks>` 只出现在存档里；内容侧声明初始链接请用原版 `<dlink>`

---

## 二、通用动作（系统未涵盖）

### 终端与节点

| 动作 | 描述 | 示例 |
|------|------|------|
| `TerminalWrite` | 向终端输出一行文本。 | `<TerminalWrite Text="Hello, World!" />` |
| `TerminalType` | 向终端逐字打印文本——**不自动换行**，从当前光标处追加（语义近 HackerScript 的 `write`；可用多条在同一行分段输出不同速度）。 | `<TerminalType Text="逐字显示的消息" CharDelay="0.04" />` |
| `TerminalFocus` | 播放终端聚焦特效（全屏变暗 + 边框扩展）。 | `<TerminalFocus Duration="5.0" BorderDuration="2.0" FadeInDuration="0.5" />` |
| `RenameNode` | 按节点 ID 重命名节点，修改即时生效并持久化到存档。 | `<RenameNode NodeID="dhs" NewName="秘密基地" />` |

### 画面与音效

| 动作 | 描述 | 示例 |
|------|------|------|
| `PlaySound` | 播放扩展目录下的 WAV 音效文件。 | `<PlaySound Path="Sounds/beep.wav" Volume="1" Pitch="0" Delay="1.5" DelayHost="cheat"/>` |
| `FlashScreen` | UI 闪烁：按指定颜色闪烁并线性渐隐回当前主题默认色。 | `<FlashScreen Color="Red" Duration="2.0" />` |
| `SwitchToThemeKeepLayout` | 切换主题但**保持面板布局**不变（只改颜色）。 | `<SwitchToThemeKeepLayout ThemePathOrName="HacknetMint" FlickerInDuration="1.5" />` |

- `FlashScreen` 的 `Color` 支持动态色关键字（如 `LDTchara`）；`Duration` 默认 `2.0`（非正值 = 立即恢复默认色）；`PlaySound="true"` 可在闪烁同时播放警告音效。重复触发为**刷新**语义，不叠加。
- `SwitchToThemeKeepLayout` 只改颜色不动布局；需要连布局一起改请用原版 `SASwitchToTheme`。

### 标题横幅 `ShowTitle`

重制自原版 `IncomingConnectionOverlay`（「本机被外部连接」覆盖层）：在屏幕中央弹出一条警示横幅，
显示标题与多行正文，上下带主题色斜条纹，左侧可配图标。适合剧情提示、系统告警、章节转场。

```xml
<ShowTitle Title="WARNING" Preset="warning" Duration="6" Icon="default">
已离开 LDTchara VPN
预计 60 秒后将被追踪
请尽快回到 VPN
</ShowTitle>
```

正文写在开闭标签之间，**换行符即分行**。首尾空行与各行公共缩进会自动去除，因此可以自由排版
（XML 缩进不会进入显示内容）。

| 属性 | 必填 | 默认值 | 说明 |
|------|:----:|--------|------|
| `Title` | ❌ | 空 | 横幅标题（⚠️ **仅支持 ASCII**，见下） |
| `Preset` | ❌ | `info` | 强调色预设：`info` = 主题高亮基色（`defaultHighlightColor`）；`warning` = 主题警告色（`warningColor`） |
| `Duration` | ❌ | `5` | 横幅显示秒数 |
| `AccentColor` | ❌ | 空 | CustomColor 覆盖强调色；`NONE` / 空 = 用 `Preset` 的主题色 |
| `Icon` | ❌ | 空 | 空 / `NONE` = **不显示**；`default` = 内置默认图标；其他 = 相对扩展根路径 |
| `IconTint` | ❌ | 空 | 空 = **自动**（`default` 染色 / 自定义原色）；`true` / `false` = 强制；其他值 = 自动 + 警告 |

不写 `AccentColor` 时强调色取自**当前游戏主题**（玩家切换主题时横幅配色自动跟随）。
颜色属性的完整解析规则见[自定义动态色系统](./../systems/custom-color.md)。

<!-- ke:9.50 -->
!!! warning "Hex 与命名色暂不可用（已知问题）"
    `AccentColor` 当前的解析链**不含 Hex**（`#RRGGBB` 会静默回退到 `Preset` 主题色），
    也不含 XNA 命名色表（如 `Red`）。二者会在**颜色解析统一**中一并补齐；
    在那之前请使用 **CustomColor 预设或动态色**。

**图标规则**：

| `Icon` | `IconTint` | 结果 |
|---|---|---|
| 不写 / 空 / `NONE` | — | **不显示图标** |
| `default` | 不写 | 内置图标，**染色**（跟随强调色） |
| `default` | `false` | 内置图标，原色 |
| 有效路径 | 不写 | 该图标，**原色**（不染色） |
| 有效路径 | `true` | 该图标，**染色** |
| 无效路径 / 加载失败 | 任意 | **回退内置图标**（染色）+ `KELog.Warn` |

- 内置图标来自模组内嵌资源，**不会写入你的扩展目录**
- `IconTint` 写其他值（如 `yes`）→ 按**自动**规则处理，并记一条 `KELog.Warn`

!!! warning "标题只支持 ASCII 字符"
    标题使用游戏的**标题字体**（`Kremlin`）。官方**没有为它提供任何语言的本地化版本**，
    因此标题中的非 ASCII 字符会显示为 `?`。**正文不受此限制**——正文使用官方本地化字体，
    会随游戏语言正常显示中文、日文等。触发时若检测到标题含非 ASCII 字符，会在日志（`KELog.Warn`）中提示。
    需要显示非 ASCII 文本时，请把它放进**正文**。

### 全屏警告 `StartScreenBleedEffectWCC`

**写法与原版 `StartScreenBleedEffect` 完全相同**，只额外支持两个颜色参数。适合系统告警、紧急事件、剧情转折。

| 属性 | 必填 | 默认值 | 说明 |
|------|:----:|--------|------|
| `AlertTitle` | ❌ | `EMERGENCY` | 顶部警告标题文字 |
| `TotalDurationSeconds` | ❌ | `200` | 效果持续总秒数 |
| **`BackgroundColor`** | ❌ | 深红（`120,0,0`） | 全屏背景色 |
| **`TextBackgroundColor`** | ❌ | 半透明暗红（`105,0,0,200`） | 文字底色 |
| `CompleteAction` | ❌ | 空（不执行） | 效果结束时加载执行的 ConditionalActions 文件；`NONE` / 空 = 不执行 |
| `Delay` / `DelayHost` | ❌ | — | Pathfinder 延迟动作 |

元素体中的文本按行解析，**最多取 3 行**（不足自动补空行，多余行忽略）。

```xml
<StartScreenBleedEffectWCC
    AlertTitle="WARNING"
    TotalDurationSeconds="5.0"
    BackgroundColor="#FF2020"
    TextBackgroundColor="#880000"
    CompleteAction="Actions/end.xml">
    Line 1 text
    Line 2 text
    Line 3 text
</StartScreenBleedEffectWCC>
```

- **颜色取值**：走 CustomColor 解析链（动态色 / 预设 / 数值 RGB / Hex / XNA 命名色），
  完整规则见[自定义动态色系统](./../systems/custom-color.md)
- **中止**：用**原版** Action 即可 —— `<CancelScreenBleedEffect />`。
  KE 通过补丁同步停止自身效果，不会出现原版与自定义效果叠加
- WCC = **W**ith **C**ustom **C**olor

### RAM 显示 `RamDisplay`

调整操作界面中 **RAM 模块**的文字 `USED RAM: x / y mb`。
`Multiplier` 统一缩放 `x` 与 `y`，`Unit` 替换单位串——**只改文字**，
RAM 条的长度仍按真实占用比例绘制。

适合剧情里伪造机器规格（例如让一台小机器“看起来”内存很大），或统一叙事口径。

!!! note "与 ZeroDayToolKit 的关系"
    与 **ZeroDayToolKit** 的 `SASetRAM` **叠加生效**：后者负责设置 RAM 数值，
    本动作在它设置的值上再按倍率与单位显示——两者不冲突，顺序是先设值、后显示。

| 属性 | 必填 | 默认值 | 说明 |
|------|:----:|--------|------|
| `Multiplier` | ❌ | `1` | `x` 与 `y` 的统一倍率；**保留小数**（最多两位、去尾零） |
| `Unit` | ❌ | `mb` | 单位串，替换 `mb`；`NONE` / 空 = 回退默认 |
| `Delay` / `DelayHost` | ❌ | — | Pathfinder 延迟动作 |

```xml
<RamDisplay Multiplier="0.5" Unit="GB" />
<!-- 真实 1024mb → 显示为 512GB -->
```

- **只写显式给出的属性**：不写的项**保持当前值**（可以只改倍率、或只改单位）
- **复位**：显式写 `Multiplier="1" Unit="mb"`
- **持久化**：改动会立即存档，读档后保持同一显示；存档里没有这个节点时回到默认值

!!! warning "会触发一次立即存档"
    本动作每次实际改动都会 `threadedSaveExecute`，也就是**立即写盘一次**。
    在关键剧情区段（或玩家不希望被打断时）连续调用它，会产生额外的存档写入。
- **没有 KE-Config 段**：默认值写在代码里，需要时用本 Action 覆盖（扩展可以在自己的起始 Action 里设置）
- 两项都不写时不做事，也不会触发一次多余存档

---

## Action 名冲突与回退

第三方模组可能占用 Action 名（实例：`Stuxnet.Audio` 占用 `PlaySound`）。
Pathfinder 的 `RegisterAction` 在重名时会抛异常并**中断整个插件加载**，
因此 KE 的**全部 32 个 Action** 都注册**两个名字**：原名，以及 `KE` + 原名。

| 原名 | 同时注册的别名 |
|------|---------------|
| `FailTrial` | `KEFailTrial` |
| `RestoreCustomTrialNodes` | `KERestoreCustomTrialNodes` |
| `LaunchVMAttack` | `KELaunchVMAttack` |
| `PhaseSwiftInit` | `KEPhaseSwiftInit` |
| `PhaseSwiftScene` | `KEPhaseSwiftScene` |
| `PhaseSwiftMusic` | `KEPhaseSwiftMusic` |
| `PhaseSwiftStop` | `KEPhaseSwiftStop` |
| `PhaseSwiftFadeOut` | `KEPhaseSwiftFadeOut` |
| `BlockNode` | `KEBlockNode` |
| `UnblockNode` | `KEUnblockNode` |
| `TerminalFocus` | `KETerminalFocus` |
| `TerminalWrite` | `KETerminalWrite` |
| `TerminalType` | `KETerminalType` |
| `RenameNode` | `KERenameNode` |
| `SetNodeIcon` | `KESetNodeIcon` |
| `SwitchToThemeKeepLayout` | `KESwitchToThemeKeepLayout` |
| `PlaySound` | `KEPlaySound` |
| `FlashScreen` | `KEFlashScreen` |
| `StartScreenBleedEffectWCC` | `KEStartScreenBleedEffectWCC` |
| `ClockStart` | `KEClockStart` |
| `ClockStop` | `KEClockStop` |
| `AttackAircraft` | `KEAttackAircraft` |
| `UploadAircraftSysFile` | `KEUploadAircraftSysFile` |
| `ShowAircraftOverlay` | `KEShowAircraftOverlay` |
| `HideAircraftOverlay` | `KEHideAircraftOverlay` |
| `BreakHeart` | `KEBreakHeart` |
| `LinkControlReset` | `KELinkControlReset` |
| `LinkControlAdd` | `KELinkControlAdd` |
| `LinkControlRemove` | `KELinkControlRemove` |
| `ShowTitle` | `KEShowTitle` |
| `StartEnding` | `KEStartEnding` |
| `RamDisplay` | `KERamDisplay` |

**规则**：

- 两个名字指向**同一个 Action**，写哪个都执行同一实现
- **无冲突**：两个名字都能用
- **某个名字被第三方占用**：另一个仍然可用，KE 日志补一条 `Warn`
- **两个都被占**（极罕见）：该 Action 不注册，日志记 `Error`

!!! tip "为什么一律双名"
    - **规则统一**：不必再记「哪些 Action 有 KE 前缀版本」—— **全都有**
    - **写 `KE` 前缀名在任何环境下都可用**（包括无冲突环境）
    - **抗未来的名字抢占**：扩展发布时无冲突，不代表玩家环境里永远无冲突；
      用 `KE` 前缀名写的扩展**不会**因为将来某个模组占用原名而改变行为
    - **便于排查**：遇到「未知动作」报错时，换成 `KE` 前缀名即可区分「没注册」还是「被抢占」

    两种名字**不要同时写** —— 它们指向同一个 Action，会执行两次。

完整机制（为什么只能靠异常兜底、`Compat/` 架构）见[与第三方模组兼容](./mod-compat.md)。

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
- [自定义动态色系统](./../systems/custom-color.md) – 颜色取值规则
- [与第三方模组兼容](./mod-compat.md) – Action 名冲突的完整机制
- [相位穿梭系统（PhaseSwift）](./../systems/phase-swift.md)
- [自定义试炼系统](./../systems/custom-trial.md)
- [VM攻击系统](./../systems/vm-attack.md)
- [飞机Daemon系统](./../systems/aircraft.md)
- [自定义定时器系统（Clock）](./../systems/clock.md)
- [自定义节点图标系统](./../systems/node-icon.md)
- [自定义结局系统（StartEnding）](./../systems/custom-ending.md)
