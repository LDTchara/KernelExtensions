# 自定义 Action

KernelExtensions 共注册 **46 个**自定义 Action，可在任何动作文件中调用。本页分三部分：

- **一、与各系统相关的动作** —— 只给速查与去向，完整参数在各自的系统页
- **二、通用动作（系统未涵盖）** —— 完整参数与示例
- **三、目标机原子动作** —— 端口 / 防火墙 / 代理的原子操作，完整参数与示例

!!! note "装了 SROS 时是 44 个"
    检测到 **SROS** 插件时，`ShowTitle` 与 `StartEnding` **不注册**（两者功能重叠，避免冲突），
    此类环境里可用的是 44 个。

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
| 目标机操作 | `PortOpen` · `PortClose` · `PortControl` · `PortFirewallAdd` · `PortFirewallSolve` · `PortFirewallReroll` · `PortFirewallConfigure` · `PortProxyAdd` · `PortProxySolve` · `PortProxyReset` · `ListPorts` | 目标机端口 / 防火墙 / 代理的原子操作 · 端口协议名诊断 | [PortHackV2 破解程序](./../systems/port-hack-v2.md) · 见第三部分 |

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

- `TerminalFocus` 的特效参数：`Duration`（全屏遮罩保持多久，秒，默认 `2.0`）、`BorderDuration`（边框扩散动画时长，默认 = `Duration`）、
  `FadeInDuration`（遮罩从不透明渐变到全黑的时长，默认 = `Duration`）、`DarkenAlpha`（遮罩最大不透明度 `0~1`，默认 `0.8`）、
  `ExpandAmount`（边框向外扩散的最大像素，默认 `200`）
- `BorderDuration` / `FadeInDuration` **负数 / `NaN` / `Infinity` / 不写 = 用 `Duration`**；
  两者短于 `Duration` 时，动画结束后边框消失、全屏遮罩仍保留到 `Duration` 结束

### 画面与音效

| 动作 | 描述 | 示例 |
|------|------|------|
| `PlaySound` | 播放扩展目录下的 WAV 音效文件。 | `<PlaySound Path="Sounds/beep.wav" Volume="1" Pitch="0" Pan="0" Delay="1.5" DelayHost="cheat"/>` |
| `FlashScreen` | UI 闪烁：按指定颜色闪烁并线性渐隐回当前主题默认色。 | `<FlashScreen Color="Red" Duration="2.0" />` |
| `SwitchToThemeKeepLayout` | 切换主题但**保持面板布局**不变（只改颜色）。 | `<SwitchToThemeKeepLayout ThemePathOrName="HacknetMint" FlickerInDuration="1.5" />` |

- `PlaySound` 的路径**相对扩展根目录**且**必须带 `.wav`**；`Volume` / `Pitch` 默认都是 `0.5`，
  `Pan` 是**声像**（`-1` 全左 ~ `1` 全右，默认 `0` 居中）；文件不存在或路径越界（`..` / 绝对路径）→ 记 `Warn` 并跳过
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
- **持久化**：改动落在**内存**里，下一次存档时随档写入；存档里没有该节点时回到默认值

!!! note "改动是「运行期临时」的"
    本动作只改**内存**中的显示设置，**不会立即写盘**；下一次存档时才会随存档落盘。
    所以：**不存档就退出 = 改动丢失**；存过档之后则会保留。
- **没有 KE-Config 段**：默认值写在代码里，需要时用本 Action 覆盖（扩展可以在自己的起始 Action 里设置）
- 两项都不写时不做事，也不会触发一次多余存档

---

### 临时主题色 `ChangeThemeColor`

运行时修改当前 OS 的**主题颜色字段**，但**不切换主题**——面板布局与背景保持原样。
适合剧情演出临时改配色（例如“系统入侵”时整屏转红），切换主题后即自动失效。

| 属性 | 必填 | 默认值 | 说明 |
|------|:----:|--------|------|
| `Fields` | ✅ | `""` | 逗号分隔的 `字段名:颜色值` 列表 |
| `Duration` | ❌ | `-1` | **动态色**的持续秒数；不写 / ≤ 0 / NaN / Infinity = 不限期（到玩家切主题为止）。到期后把触发前的颜色写回（只还原动态字段与 `topBarColor` / `highlightColor` / `moduleColorSolid`） |
| `Delay` / `DelayHost` | ❌ | — | Pathfinder 延迟动作 |

颜色值分**静态**与**动态**两类，**同一条 `Fields` 里可混用**：

| 类别 | 写法 | 行为 |
|------|------|------|
| **静态** | XNA 命名色（如 `Red`，大小写不敏感）、`#RGB`、`#RRGGBB`、`#AARRGGBB`、`R,G,B[,A]` | 只**在触发时写一次**，之后按"切主题才失效"的既有语义保持 |
| **动态** | `LDTchara[:速度[:透明度[:饱和度[:明度]]]]`、`Rainbow`（`LDTchara` 的别名）、`Gradient:#起始色:#结束色:秒数`、CC 预设名`[:速度倍率[:透明度]]` | **逐帧重算**；需靠 `Duration` 收尾 |

写 `NONE` / 空 = 该字段保持不变。字段名须是主题的颜色字段，例如 `defaultHighlightColor`、
`warningColor`、`moduleColorSolidDefault`、`terminalTextColor` 等。

```xml
<ChangeThemeColor Fields="defaultHighlightColor:Red,warningColor:#FF0000" />
<!-- 只改高亮色与警告色，其余颜色保持当前主题 -->

<ChangeThemeColor Fields="defaultHighlightColor:Rainbow,warningColor:#FF0000" Duration="10" />
<!-- 高亮色走彩虹、逐帧重算，10 秒后还原；警告色仍是静态的红色 -->
```

- **未知字段名或无法解析的颜色** → 该键值对跳过并 `KELog.Warn`，其余字段照常生效
- **撤销**：只改当前 OS 的内存值，不写存档、不改已加载的主题对象；玩家**切换主题**后
  OS 字段被主题重新写入，改动自然失效
- **动态字段必须显式收尾** —— 它们逐帧回写，`Duration` 到期时才会还原触发前的原值
- **要连布局一起改**请用 `SwitchToThemeKeepLayout`

---

## 三、目标机原子动作（端口 / 防火墙 / 代理）

这一组动作都作用在**目标机**上，**每个只做一件事**（开端口、关端口、加防火墙、换解……），
由作者自由组合成动作序列——既能在 PortHackV2 的动作点里用，也能在任何其它系统的动作文件里用。

- **端口**：`PortOpen` · `PortClose`（开关端口）· `PortControl`（增删端口）
- **防火墙**：`PortFirewallAdd` · `PortFirewallSolve` · `PortFirewallReroll` · `PortFirewallConfigure`
- **代理**：`PortProxyAdd` · `PortProxySolve` · `PortProxyReset`
- **诊断**：`ListPorts`（列出全部已注册的端口协议，写配置时用来查协议名）

**共同约定**

| 项 | 说明 |
|------|------|
| `TargetComp` | 目标机 `idName`；**留空 / `NONE` = 当前连接的节点**（一个都没连时作用在玩家自己那台上） |
| `Delay` / `DelayHost` | 由 `KEAction` 基类提供，本组每个动作都能用（见页末「延迟执行」） |
| 目标机不存在 | 记一条 `KELog.Warn` 并**跳过本次动作**——不崩，也不会「看起来跑过了」 |
| 端口列表 | 逗号或分号分隔都行（`ssh,ftp` / `ssh;ftp`），自动去空、去重 |
| 权限 | **不做任何权限检查**：它们像剧情脚本一样直接改目标机状态。要「玩家破解成功后才生效」，请挂到动作点上 |
| 动作文件路径 | 动作点里引用的文件路径**相对扩展根目录**，且**大小写必须与磁盘上的实际文件名一致**（Linux 区分大小写）——见[扩展作者指南](./../guides/author-guide.md) |

### 端口

Pathfinder 下每个端口有两件事：**在不在机器上**、**开没开**。增删端口归 `PortControl`，
开关端口归 `PortOpen` / `PortClose`——**不要拿开关端口去当增删用**。

#### `PortOpen` —— 打开端口

把一个或多个**已经在目标机上**的端口打开。协议名先用 `ListPorts` 查。

| 属性 | 必填 | 默认值 | 说明 |
|------|:----:|--------|------|
| `Ports` | ✅ | 空 | 端口协议名，逗号或分号分隔；`NONE` / 空 = 记 Warn 并跳过 |
| `TargetComp` | ❌ | 空 | 目标机 `idName`；留空 / `NONE` = 当前连接的节点 |
| `Delay` / `DelayHost` | ❌ | — | Pathfinder 延迟动作 |

```xml
<PortOpen Ports="ssh,ftp" />
<PortOpen Ports="ssh" TargetComp="targetNode" />
```

- 「打开」在 Pathfinder 下就是**把该端口标记为已破解**（原版 `openPort` 被改写为设置这个标志，
  原版 `isPortOpen` 读的也是它）——效果等价于玩家已经把这个端口打下来了
- **端口不在目标机上** → **逐个** Warn 跳过，同一次调用里的其它端口照常处理
- 端口本身不存在时请先用 `PortControl` 装上

#### `PortClose` —— 关闭端口

把一个或多个**已经在目标机上**的端口关掉。

| 属性 | 必填 | 默认值 | 说明 |
|------|:----:|--------|------|
| `Ports` | ✅ | 空 | 端口协议名，逗号或分号分隔；`NONE` / 空 = 记 Warn 并跳过 |
| `TargetComp` | ❌ | 空 | 目标机 `idName`；留空 / `NONE` = 当前连接的节点 |
| `Delay` / `DelayHost` | ❌ | — | Pathfinder 延迟动作 |

```xml
<PortClose Ports="ftp" TargetComp="targetNode" />
```

- 只是把端口**关掉**，**不删端口**——要「这台机器上不再有这个端口」用 `PortControl Switch="remove"`
- 端口不在目标机上 → 逐个 Warn 跳过

#### `PortControl` —— 增删端口

给目标机**注册并装上**一个端口，或从目标机上**移除**一个端口。

| 属性 | 必填 | 默认值 | 说明 |
|------|:----:|--------|------|
| `TargetID` | ✅ | 空 | 目标机 `idName`。⚠️ **本动作的属性名是 `TargetID`**，与其它目标机动作的 `TargetComp` 不同；取不到机器 → 终端提示并跳过 |
| `Protocol` | ✅ / ❌ | 空 | 端口协议名。`Switch="remove"` 时可省略——此时按 `PortNum` 从端口注册表反查 |
| `DisplayName` | 添加时 ✅ | 空 | 端口显示名（终端与界面上显示的名字） |
| `PortNum` | ❌ | `0` | 端口号；`0` = 不给默认端口号。移除时用于反查协议 |
| `Switch` | ❌ | `add` | `add` = 添加（默认）/ `remove` = 移除；其它值 → 终端提示并跳过 |
| `Delay` / `DelayHost` | ❌ | — | Pathfinder 延迟动作 |

```xml
<!-- 给目标机加一个 ssh 端口（默认端口号 22） -->
<PortControl TargetID="targetNode" Protocol="ssh" DisplayName="SSH" PortNum="22" />

<!-- 再把它移除；下面两条等价 -->
<PortControl TargetID="targetNode" Protocol="ssh" Switch="remove" />
<PortControl TargetID="targetNode" PortNum="22" Switch="remove" />
```

- 添加时**先注册协议**（写进全局端口注册表：协议名 + 显示名 + 默认端口号），再给目标机装上端口；
  协议注册一次后**所有机器**都能用这个名字
- 装完端口是**关闭**状态：玩家仍然要自己破解 / 打开，不会白送
- **重复添加是安全的**（Pathfinder 按协议去重，已存在就直接返回）
- 移除按**协议名**进行，不要求该协议在注册表里；省略 `Protocol` 时按端口号反查（原版内置与模组注册的端口都覆盖）
- 移除一台机器上不存在的端口 → 终端提示 `not found`，不算错误

### 防火墙

防火墙的解由**目标机自己**持有：`PortFirewallAdd` 放一份、`PortFirewallSolve` 解开、
`PortFirewallReroll` 换掉解法、`PortFirewallConfigure` 调参数。

#### `PortFirewallAdd` —— 放一份新防火墙

给目标机**放一份新的防火墙**（会替换掉已有的那一份），放完处于**未解**状态。

| 属性 | 必填 | 默认值 | 说明 |
|------|:----:|--------|------|
| `Solution` | ✅ | 空 | 防火墙的**解**：写 `@random` = **随机生成**，写别的字符串 = 就用这个字面量解；`NONE` / 空 = 记 Warn 并跳过 |
| `Length` | ❌ | `Solution` 的长度 | **随机解**的长度，只在 `Solution="@random"` 时有意义：不写 = 用 `Solution` 本身的长度（`@random` 是 7 位），写 `0` = 原版默认的 6 位；`Solution` 是具体字符串时本项无作用 |
| `AdditionalDelay` | ❌ | `0` | 玩家分析防火墙时**每行**的额外停顿（秒），越大越慢 |
| `TargetComp` | ❌ | 空 | 目标机 `idName`；留空 / `NONE` = 当前连接的节点 |
| `Delay` / `DelayHost` | ❌ | — | Pathfinder 延迟动作 |

```xml
<!-- 12 位随机解：玩家得推出来 -->
<PortFirewallAdd Solution="@random" Length="12" AdditionalDelay="0.3" />

<!-- 解固定为 ABCD：玩家输 ABCD 就通过 -->
<PortFirewallAdd Solution="ABCD" TargetComp="targetNode" />
```

- 会**替换**目标机已有的防火墙，不是叠加；放完一定是**未解**状态
- 解的长度与界面一致：写入时按**最终解**同步 `solutionLength`，不会出现「要输 N 位、解只有 M 位」的无解情形
- 要「防火墙还在、只换解法」用 `PortFirewallReroll`；要「改现有防火墙的参数」用 `PortFirewallConfigure`；
  要「直接解开」用 `PortFirewallSolve`

!!! tip "`Length` 只管随机解"
    `Solution="@random"` 时按 `Length` 生成随机解；`Solution` 写具体字符串时，解就是你写的那串，
    `Length` 不参与——两种情况都会把显示长度同步成解的实际长度，所以防火墙始终可解。

#### `PortFirewallSolve` —— 解开防火墙

把目标机的防火墙**直接标记为已解**，可选延迟。

| 属性 | 必填 | 默认值 | 说明 |
|------|:----:|--------|------|
| `TargetComp` | ❌ | 空 | 目标机 `idName`；留空 / `NONE` = 当前连接的节点 |
| `Delay` / `DelayHost` | ❌ | — | 延迟多少秒**之后**才解开；留空 = 立即 |

```xml
<PortFirewallSolve />

<!-- 破解成功 12 秒后对手"才反应过来"防火墙被破了 -->
<PortFirewallSolve Delay="12" />
```

- 目标机**没有防火墙** → Warn 跳过，不会凭空造一份
- 只把防火墙标记为**已解**，**不清分析进度**；要连进度一起复位用 `PortFirewallReroll` / `PortFirewallConfigure Reset="true"`
- 延迟走基类 `Delay`；本动作**没有** `AdditionalDelay`（那个是「玩家分析时每行多停多久」）

#### `PortFirewallReroll` —— 换掉解法

**防火墙还在，解法被换掉**（剧情上常见于对手反制）。

| 属性 | 必填 | 默认值 | 说明 |
|------|:----:|--------|------|
| `Length` | ❌ | `12` | 新解的长度；`0` = 原版默认的 6 位；负数 / 非数字 = 记 `Warn` 后用 `12` |
| `AdditionalDelay` | ❌ | `0` | 分析输出的额外延迟（秒） |
| `Reset` | ❌ | `true` | 是否清掉已解状态与分析进度 |
| `TargetComp` | ❌ | 空 | 目标机 `idName`；留空 / `NONE` = 当前连接的节点 |
| `Delay` / `DelayHost` | ❌ | — | Pathfinder 延迟动作 |

```xml
<!-- 换成一份新的 12 位随机解，并把"已解"状态清掉 -->
<PortFirewallReroll Length="12" AdditionalDelay="0.3" />
```

- 与 `PortFirewallAdd` 的区别：那个是**放一份新的**防火墙，这个是**把现有防火墙的解法换掉**
- 目标机**没有防火墙** → Warn 跳过
- 换解会**新建一份防火墙**，所以结果一定是「未解 + 分析进度归零」；`Reset="false"` 也拿不回已解状态

#### `PortFirewallConfigure` —— 改现有防火墙的参数

调整目标机**现有**防火墙的解长度 / 分析延迟 / 解的内容，可选把已解状态与分析进度复位。

| 属性 | 必填 | 默认值 | 说明 |
|------|:----:|--------|------|
| `Length` | ❌ | 现有解的长度 | 解的长度；留空 = 保持原值。⚠️ 写**具体解**时它要与解一致，见下 |
| `AdditionalDelay` | ❌ | 现有值 | 分析输出的额外延迟（秒）；留空 = 保持原值 |
| `Solution` | ❌ | 现有解 | 解的内容：留空 = **保持现有解**（目标机没有防火墙时 = 随机生成一份新的，此时与显式写 `@random` 等价）；写 `@random` = **按 `Length` 重新随机**；写别的字符串 = 用这个字面量解 |
| `Reset` | ❌ | `false` | `true` = 把已解状态与分析进度清零 |
| `TargetComp` | ❌ | 空 | 目标机 `idName`；留空 / `NONE` = 当前连接的节点 |
| `Delay` / `DelayHost` | ❌ | — | Pathfinder 延迟动作 |

```xml
<!-- 只把分析每行的延迟调大，其余参数保持原样 -->
<PortFirewallConfigure AdditionalDelay="0.5" />

<!-- 换成 12 位随机解，并把进度清零 -->
<PortFirewallConfigure Length="12" Solution="@random" Reset="true" />
```

- 留空的项**保持原值**，不会被打回默认——想复位就别写它
- 目标机**没有**防火墙 → 按「添加一份」处理（规则同 `PortFirewallAdd`）
- ⚠️ `Length` 与**写死的解**：只有随机解会按 `Length` 生成；`Solution` 写具体字符串时请同时给出**同长度**的
  `Length`（留空会沿用现有长度，长度不一致就会造出「要输 N 位、解只有 M 位」的**无解**防火墙）。
  只想调难度（分析延迟）就别写 `Length`

### 代理

代理生效时玩家要先把它**过载掉**才能继续；`PortProxyAdd` 加、`PortProxySolve` 解、
`PortProxyReset` 只把过载进度打回。

#### `PortProxyAdd` —— 给目标机加代理

| 属性 | 必填 | 默认值 | 说明 |
|------|:----:|--------|------|
| `Time` | ❌ | `10` | 代理的过载 / 破解所需时间（原版 `addProxy` 的 tick 数）。⚠️ **必须 > 0**：写 `0` / 负数时原版不做事（机器不会被标记为"有代理"），只剩代理生效标志 |
| `TargetComp` | ❌ | 空 | 目标机 `idName`；留空 / `NONE` = 当前连接的节点 |
| `Delay` / `DelayHost` | ❌ | — | Pathfinder 延迟动作 |

```xml
<PortProxyAdd Time="10" />
<PortProxyAdd Time="30" TargetComp="targetNode" />
```

- 加完代理是**生效**状态（`proxyActive = true`）：玩家得先过载掉它
- 目标机本来就有代理 → 直接覆盖时间与状态，不会叠加成两道

#### `PortProxySolve` —— 解除代理

| 属性 | 必填 | 默认值 | 说明 |
|------|:----:|--------|------|
| `TargetComp` | ❌ | 空 | 目标机 `idName`；留空 / `NONE` = 当前连接的节点 |
| `Delay` / `DelayHost` | ❌ | — | 延迟多少秒**之后**才解除；留空 = 立即 |

```xml
<PortProxySolve />
<PortProxySolve Delay="11" />
```

- 解除的同时把正在跑的 `PortHackExe` 标记为结束（对齐原版「代理没了、破解进程随之结束」）
- 目标机**没有代理** → Warn 跳过
- 只想过载进度清零、代理**仍然生效** → 用 `PortProxyReset`

#### `PortProxyReset` —— 过载进度归零

把玩家已经推掉的**过载进度**打回起点，**代理仍然生效**（剧情上「对手把进度打回去了」）。

| 属性 | 必填 | 默认值 | 说明 |
|------|:----:|--------|------|
| `TargetComp` | ❌ | 空 | 目标机 `idName`；留空 / `NONE` = 当前连接的节点 |
| `Delay` / `DelayHost` | ❌ | — | Pathfinder 延迟动作 |

```xml
<PortProxyReset />
```

- 与 `PortProxySolve` 的区别：后者是「代理被解掉」（`proxyActive = false`），本动作**保留代理**
- 目标机**没有代理** → Warn 跳过

### 诊断 `ListPorts`

**作者工具**：把当前**所有已注册的端口协议**（模组注册的 + 原版内置的）打到玩家终端，
用来查「该往配置里填哪个协议名、默认端口号是多少」。Pathfinder 没有公开的端口枚举接口，本动作补上了这一环。

| 属性 | 必填 | 默认值 | 说明 |
|------|:----:|--------|------|
| `Filter` | ❌ | 空 | 只列**协议名或显示名包含**该子串的条目（大小写不敏感） |
| `Max` | ❌ | `40` | 一次最多列几条；`0` / 负数 = 不限 |
| `ToLog` | ❌ | `false` | `true` = 同时写一条 `KELog.Info`（便于在日志里留档） |
| `Delay` / `DelayHost` | ❌ | — | Pathfinder 延迟动作 |

```xml
<ListPorts />
<ListPorts Filter="ssh" />
```

输出形如：

```
Registered ports: 12 (3 from mods, 9 built-in)
[22] ssh - SSH (vanilla)
[no default port] myport - My Protocol (mod)
```

- 每行 = `[默认端口号] 协议名 - 显示名 (vanilla|mod)`；没有默认端口号的协议显式标注 `no default port`
- 排序：先按显示名、再按协议名，输出稳定便于对照
- `Filter` 一条都没命中 → 终端提示 `No port matches filter '...'`
- 一条都列不出来（通常是读不到 Pathfinder 的端口注册表）→ 记 `KELog.Error` 并提示上报
- 它**只读**，不改任何状态；放在起始动作里跑一次即可

### 组合示例

「玩家破解成功后，目标机反制」：

```xml
<PortClose Ports="ftp" />
<PortFirewallReroll Length="12" AdditionalDelay="0.3" />
<PortProxyReset />
```

这些动作**不做权限检查**，所以"什么时候生效"完全由你挂的动作点决定——挂在破解成功的动作点上，
就是"破解成功后反制"；挂在 `<OnStart>` 里，就是开局就摆好。

---

## Action 名冲突与回退

第三方模组可能占用 Action 名（实例：`Stuxnet.Audio` 占用 `PlaySound`）。
Pathfinder 的 `RegisterAction` 在重名时会抛异常并**中断整个插件加载**，
因此 KE 的**全部 46 个 Action** 都注册**两个名字**：原名，以及 `KE` + 原名。

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
| `ListPorts` | `KEListPorts` |
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
| `FlightDaemonStatus` | `KEFlightDaemonStatus` |
| `FlightDaemonMap` | `KEFlightDaemonMap` |
| `BreakHeart` | `KEBreakHeart` |
| `LinkControlReset` | `KELinkControlReset` |
| `LinkControlAdd` | `KELinkControlAdd` |
| `LinkControlRemove` | `KELinkControlRemove` |
| `ShowTitle` | `KEShowTitle` |
| `StartEnding` | `KEStartEnding` |
| `RamDisplay` | `KERamDisplay` |
| `ChangeThemeColor` | `KEChangeThemeColor` |
| `PortControl` | `KEPortControl` |
| `PortOpen` | `KEPortOpen` |
| `PortClose` | `KEPortClose` |
| `PortFirewallAdd` | `KEPortFirewallAdd` |
| `PortFirewallSolve` | `KEPortFirewallSolve` |
| `PortFirewallReroll` | `KEPortFirewallReroll` |
| `PortFirewallConfigure` | `KEPortFirewallConfigure` |
| `PortProxyAdd` | `KEPortProxyAdd` |
| `PortProxySolve` | `KEPortProxySolve` |
| `PortProxyReset` | `KEPortProxyReset` |

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
- [PortHackV2 破解程序](./../systems/port-hack-v2.md) – 目标机破解流程与其动作点
