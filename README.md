# KernelExtensions

> **快速导航**： [中文版本](#中文版本) | [English Version](#english-version)

## 中文版本

# KernelExtensions

> **注意**：本模组代码由 AI 辅助生成，尚未经过充分测试。可能存在 Bug 或不兼容情况，请谨慎使用并欢迎反馈。

## 前置条件

在开始使用本模组前，请确保已安装：
- Hacknet 的 DLC **Labyrinths**
- **Pathfinder** 框架（5.3.4 或更高版本）

---

**KernelExtensions** 是一个使用 **Pathfinder API** 的 Hacknet 模组，旨在扩展游戏中硬编码的机制。

### 功能列表

#### 自定义试炼程序（稳定运行）
- 基于 XML 配置的多阶段试炼，通过 Flag 系统选择试炼配置。
- **与原版 DLC 试炼的主要不同**：
  - 完全由外部 XML 驱动，无需修改代码即可改变试炼流程、特效和阶段设计。
  - 支持单阶段超时或失败后**重置当前阶段**（可配置），而非原版的回溯到第一关。
  - 节点摧毁特效、邮件爆炸、主题切换、内存缩减等均通过开关控制，并细化到持续时间、延迟等参数。
  - 各阶段可独立指定背景音乐、任务文件、描述文本（支持 `%` 短停顿和 `%%` 长停顿）。
  - 提供**阶段开始时执行动作** (`OnPhaseStart`) 与**动画全部完成后执行动作** (`OnAnimationComplete`)，以及全局超时、阶段失败等多个动作挂载点。
  - 内置**终端聚焦特效**（可配置），在阶段开始或试炼完成时全屏变暗并突出终端。
  - 试炼完成后可**自动连接到指定节点**（通过节点 ID 查找 IP），并控制是否停止音乐。
  - 动态内存缩减功能使程序窗口高度根据内部控件自动调整（也可选择线性缩减到固定目标值84，但因可能会造成视觉错误故不建议）。
- 部分细节：
  - 试炼中摧毁的节点会被记录并存入存档。扩展作者可通过 `RestoreCustomTrialNodes` 动作在任意时机（如任务完成后）以动画特效逐个恢复节点。
  - 支持在试炼开始时（旋转动画前）自动切换主题。可指定预设主题名称（如 `HacknetMint`）或自定义主题文件路径，并配置切换时的闪烁时长。
- 多语言支持（按钮、锁定提示、初始化文字、完成/失败提示）。
- 所有路径基于扩展根目录，安全且灵活。

#### 可配置虚拟机攻击系统（VM Attack）

- 不要为原版的半成品systakeover哀悼了，迎面走来的是全新的 **虚拟机攻击系统**！可通过自定义action `<LaunchVMAttack ConfigName="MyAttack"/>` 触发自定义崩溃攻击。
- 攻击配置文件存放于扩展根目录 `VMATK/` 下，完全由 XML 驱动。
- **三种恢复模式**：
  - `FileDeletion`：玩家必须手动删除指定文件后才能恢复（强制退出游戏）。
  - `FileExists`：玩家必须创建指定文件后才能恢复（可游戏内即时验证）。
  - `Password`：玩家必须输入正确密码才能恢复（游戏内交互）。
- **攻击流程**：触发后生成虚假文件、添加 Flag 并保存，随后播放色散闪光特效，延迟执行 `crash`，进入原版蓝屏→黑屏→启动日志→第 50 行错误注入→15 秒错误状态→自定义恢复界面。
- **恢复界面**：
  - 系统日志阶段：可配置多个文本文件，以等宽字体逐行快速回显（模拟原版 BootFailSequence）。
  - 引导文本阶段：每行自动带 `> ` 前缀，支持 `||Px.x||`（停顿）、`||Sx.x||`（变速）、`||SR||`（恢复默认速度）行内控制指令；可启用“已读跳过”，再次进入直接显示全文。
  - 交互阶段：文件模式下显示按钮（复制帮助文件、打开终端、退出游戏），密码模式下显示密码输入框和提交按钮，支持错误提示多语言翻译。
- **攻击解除**：文件删除/存在模式下，玩家重启游戏后若文件条件满足，自动播放成功音乐并执行一次黑屏重启恢复正常。
- **多语言支持**：密码匹配/不匹配提示、帮助按钮文本均已支持 10 种语言。
- **配置要点**：可自定义错误消息、系统日志文件列表及间隔、引导文本、帮助文件、虚假文件路径与大小、检测文件路径及正则匹配、成功音乐等。

#### 📟 自定义 Action

注：Action内出现的所有路径均为相对于扩展根目录的路径。

##### 与主要功能相关的 Action

- **`FailTrial`**：强制当前正在运行的 CustomTrialExe 试炼立即失败。
用法：
```xml
<FailTrial />
<!-- 或者 --->
<FailTrial Delay="3.0" DelayHost="delayhost" />
```
- **`RestoreCustomTrialNodesAction`**：恢复之前试炼中删除的节点，并以动画特效逐个显示。指定 `ConfigName` 即可对应恢复特定试炼的节点。
- 用法：
```xml
<RestoreCustomTrialNodes ConfigName="ExampleTrial" />
```
- **`LaunchVMAttackAction`**：启动指定的VM攻击。 `ConfigName` 字段对应特定的配置名。
- 用法：
```xml
<LaunchVMAttack ConfigName="MyAttack" />
```

##### 其他功能型 Action

- **`PlaySoundAction`**：播放一个音效，`Path` 属性为必填项且必须指向一个wav文件，其他属性（`Volume`, `Pitch`, `Pan`）为可选项，可配合 `Delay` 与 `DelayHost` 延迟输出（可省略）。
用法：
```xml
<PlaySound Path="Sounds/beep.wav" Volume="1" Pitch="0" Delay="1.5" DelayHost="delayhost"/>
```
- **`TerminalWriteAction`**：向终端输出一行文本，支持 `text` 属性，支持 `Delay` 和 `DelayHost` 属性（同上）。
用法：
```xml
<TerminalWrite text="延迟消息" Delay="1.5" DelayHost="delayhost"/>
```
- **`TerminalTypeAction`**：向终端逐字输出文本，支持 `Delay` 和 `DelayHost` 属性（同上）。
  - CharDelay：每个字符输出间隔（秒），默认 0.04（与原版 TextWriterTimed 一致）。
用法：
```xml
<TerminalType text="消息内容" CharDelay="0.04" Delay="1.5" DelayHost="delayhost"/>
```
- **`TerminalFocusAction`**：播放终端聚焦特效（全屏变暗 + 终端边框扩散），可独立设置边框动画时长、遮罩淡入时长等参数，支持延迟执行。
  - Duration: 遮罩保持的总时长（秒），默认 2.0。在此之后遮罩消失。
  - BorderDuration: 边框扩散动画的时长（秒），默认等于 Duration。若短于 Duration，则动画结束后边框消失但遮罩保留。
  - FadeInDuration: 遮罩从不透明渐变到完全变黑所需时长（秒），默认与 Duration 相同。
  - DarkenAlpha: 遮罩的最大透明度（0~1），默认 0.8。
  - ExpandAmount: 边框向外扩展的最大像素量，默认 200。
- 用法：
```xml
<TerminalFocus Duration="5.0" BorderDuration="2.0" FadeInDuration="0.5" DarkenAlpha="0.8" ExpandAmount="200" />
```

#### ⚙ 其他改进
- 计时条与标题自适应布局，无计时条时标题居中，有计时条时自动上移。
- 退出程序时界面元素平滑淡出。
- 锁定界面（无 Flag 时）显示本地化“试炼已锁定”及退出按钮，并绘制动态网格背景。
- 音乐路径智能解析，支持纯文件名、扩展目录、DLC 音乐、原版音乐。
- 修复大量错误（全局超时重复触发、阶段超时不重置、失败时多余终端信息等），稳定性大幅提升。
- 新增 `ActionHelper` 公共类，统一动作文件执行逻辑，`CustomTrialExe` 与 VM 系统共用，提高稳定性。
- 新增 `MusicPathResolver` 公共类，统一扩展内音乐文件的路径解析（修复扩展文件夹名与扩展名不一致时无法播放音乐的问题）。
- 新增 `SoundHelper` 公共类，支持播放扩展内的 WAV 音效。

#### 🚧 计划中
- 可自定义的 Porthack 心脏 Daemon（包括 Porthack 后执行的 Action 配置）
- （已完成，待测试）DLC 中可自定义的飞机 Daemon（除完全还原外还允许配置坠毁时长、维持飞行的固件名称等）

### 安装与使用

1. **安装模组**  
   将编译好的 `KernelExtensions.dll` 放入扩展的 `Plugins` 文件夹中（例如 `Extensions/YourExtension/Plugins/`）。**本模组必须作为扩展的一部分运行**，不支持全局插件模式。

2. **准备配置文件**  
   在扩展根目录下创建 `Trial` 文件夹，将试炼配置 XML 文件放入其中（例如 `Trial/MyTrial.xml`）。  
   所有其他相关文件（动作文件、描述文本文件、任务文件）也应放在扩展目录下的适当位置（路径相对于扩展根目录）。

3. **选择要运行的试炼（通过 Flag）**  
   - **重要**：Flag 只能通过 Action 添加/移除，不能使用控制台指令直接操作。
   - 在任务或 Action 文件中，使用 `<RunFunction FunctionName="addFlags:CustomTrial_MyTrial"/>` 来设置 Flag。
   - 运行 `CustomTrial` 后，程序会自动查找以 `CustomTrial_` 开头的 Flag，取后面的部分作为配置文件名（如 `CustomTrial_MyTrial` → 加载 `MyTrial.xml`）。
   - 如果没有设置任何此类 Flag，程序窗口会显示“试炼已锁定”文字，无法开始。

4. **在游戏中启动**  
   确保玩家节点 `bin/` 目录下存在 `CustomTrial.exe` 后，终端输入 `CustomTrial` 即可启动试炼程序。

### 给扩展作者（任务制作者）的指南

#### 一、系统概述

KernelExtensions 目前提供两大可配置系统，均通过 Flag 机制触发，由 XML 配置文件驱动：
- **自定义试炼（CustomTrial）**：多阶段任务挑战，包含特效、计时器、音乐、节点摧毁等。
- **VM 攻击（VM Attack）**：模拟虚拟机崩溃攻击，强制玩家与真实文件系统交互以解除锁定。

两个系统的配置文件均存放于扩展根目录下的专用文件夹（`Trial/` 与 `VMATK/`），所有路径均相对于扩展根目录。

#### 二、如何设置 Flag 来选择配置

**通用规则**：两种系统都通过原版 Flag 来控制。你需要在任务或 Action 中使用 `RunFunction` 来添加 Flag。

**试炼系统**：
```xml
<Instantly>
  <RunFunction FunctionName="addFlags:CustomTrial_MyTrial"/>
</Instantly>
```
Flag 以 `CustomTrial_` 开头，后面跟配置文件名（不含 `.xml`）。运行 `CustomTrial` 程序时，会自动查找并加载对应的 `Trial/<名称>.xml`，试炼成功后会自动删除对应的配置Flag。

**VM 攻击系统**：
```xml
<Instantly>
  <LaunchVMAttack ConfigName="MyAttack" />
```
直接使用自定义 Action `LaunchVMAttack`，传入配置文件名（不含 `.xml`）。程序将自动加载 `VMATK/<ConfigName>.xml` 并触发攻击。

VM 攻击的 Flag 由系统自动管理，无需手动移除。

#### 三、编写动作文件

在试炼配置的 `<OnPhaseStart>`、`<OnComplete>`、`<OnFail>` 等节点中，可以指定一个 Action 的 XML 文件来执行自定义逻辑。VM 攻击也支持在引导文本开始播放时同步执行动作（通过配置 `<ActionOnGuideTextStart>` 指定）。

**关于 `Delay` 属性的重要说明**：
- 大多数动作（如 `RunFunction`、`SwitchToTheme`、`ChangeAlertIcon` 等）如果设置了 `Delay > 0`，**必须同时指定 `DelayHost`**（一个拥有 `FastActionHost` 守护进程的节点，例如 `playerComp` 或任意已存在的服务器）。
- 如果 `Delay <= 0`，动作会立即执行。
- **特例**：`AddIRCMessage` 支持 `Delay` 属性且不需要 `DelayHost`，它会依赖目标 IRC/DHS 节点自身实现延迟。`Delay` 可以为负数，使消息显示为过去的时间。

**示例**：
```xml
<ConditionalActions>
  <OnConnect target="TMSDHS">
    <AddIRCMessage Author="LDTchara" TargetComp="TMSDHS" Delay="-7">我废了挺大劲才解除那个弱智追踪</AddIRCMessage>
    <AddIRCMessage Author="BruhSolar" TargetComp="TMSDHS" Delay="13">@#PLAYERNAME# 你终于来了</AddIRCMessage>
    <ChangeAlertIcon Target="TMSDHS" Type="irchub" DelayHost="Cheat" Delay="1.0"/>
    <AddConditionalActions Filepath="Actions/TMS1.xml" DelayHost="TMSDHS" Delay="22"/>
  </OnConnect>
</ConditionalActions>
```

更多可用动作请参考 Hacknet 官方示例扩展 `IntroExtension` 或[社区中文 Wiki](https://github.com/FBIKdot/Hacknet-Extension-Tutorial/blob/main/Content/Actions.md)。

#### 四、配置文件结构

**试炼配置**：详见仓库内 `XMLExample/ExampleTrial.xml`。

**VM 攻击配置**：详见仓库内 `XMLExample/MyAttack.xml`。主要可配置项包括：
- **恢复模式**：`FileDeletion`（删除文件）、`FileExists`（创建文件）、`Password`（输入密码）
- **系统日志**：多个文本文件路径，在恢复界面以等宽字体快速回显（模拟系统崩溃日志）
- **引导文本**：支持 `||Px.x||` 停顿、`||Sx.x||` 变速、`||SR||` 恢复速度
- **已读标记**：开启后，首次阅读完毕，再次进入直接显示全文
- **帮助文件**：点击按钮时复制到存档目录并打开
- **虚假文件**：指定要生成的文件路径、大小或源文件
- **成功音乐**：攻击解除后播放

**通用说明**：
- 所有 `file` 属性中的路径均为**相对于扩展根目录的相对路径**。
- `DescriptionText` 或 `GuideText` 可以是文件路径或内嵌文本，支持 `%` 和 `%%` 停顿。
- `MissionFile` 指向标准 Hacknet 任务文件。自定义试炼配置中编写任务时需设置 `<mission activeCheck="true">` 和 `<nextMission IsSilent="true">`。
```xml
<mission id="test" activeCheck="true" shouldIgnoreSenderVerification="false">
...
<nextMission IsSilent="true"></nextMission>
<!-- 请勿填写nextMission元素的内容-->
<!-- Matt 你为什么要把控制当前任务的 IsSilent 参数塞到加载下一个任务的元素里，这太 TM 反直觉了还很诡异 -->
```
- 自定义颜色支持：颜色名称（如 `Red`）、十六进制（如 `#FF0000`）或某个人的名字。省略则使用当前主题色。

#### 五、注意事项

- **Flag 命名**：试炼必须以 `CustomTrial_` 开头；VM 攻击的 Flag 由系统自动管理，无需手动设置。
- **动作文件延迟**：大多数动作需要 `DelayHost` 才能延迟执行。`Delay <= 0` 时立即执行。负 `Delay` 仅对 `AddIRCMessage` 有意义。
- **特效开关**：试炼中的 UI 闪烁、邮件爆炸等可通过配置独立开关；VM 攻击无额外特效开关。
- **多语言**：大部分 UI 文本会根据游戏语言自动切换，无需额外配置。（后续会增加语言文件支持）
- **路径要求**：本模组**必须**作为扩展的一部分运行，所有文件路径均基于扩展根目录。不支持全局插件模式。

### 已知限制 / 未实现功能

| 功能 | 状态 | 说明 |
|------|------|------|
| Porthack 心脏 Daemon | ❌ 未开始 | 计划后续版本添加 |
| DLC 飞机 Daemon 自定义 | ⚠ 待验证 | 功能已由 April_Crystal 实现，待整合和测试 |

### 备注

- 这是我的第一个 C# 项目和 GitHub 仓库，会有相当多的缺点，欢迎指出不足之处，我会尽力改进。
- 欢迎后续贡献代码或提出建议。
- 代码由 AI 辅助生成，可能包含错误或不符合最佳实践，请自行测试后使用。
- 如果遇到 Bug，请提交 Issue 并附上日志文件（`BepInEx/LogOutput.log`）和你的配置文件。

### ❤️ 特别感谢（不分先后）

- **April_Crystal**：制作了飞机Daemon相关部分，在开发过程中提出了大量宝贵建议，对模组的完善贡献巨大。
- **HN 扩展小屋的各位朋友**：积极测试、反馈问题，为模组的稳定性提供了重要帮助。

---

## English Version

# KernelExtensions

> **Note**: The code of this mod is AI‑assisted and has not been thoroughly tested. Bugs or incompatibilities may exist. Use with caution and feedback is welcome.

## Prerequisites

Before using this mod, please ensure you have installed:
- Hacknet DLC **Labyrinths**
- **Pathfinder** framework (5.3.4 or higher)

---

**KernelExtensions** is a Hacknet mod using the **Pathfinder API**, designed to extend hardcoded mechanics in the game.

### Feature List

#### Custom Trial Program (Stable)
- Multi‑phase trial driven by XML configuration, selected via Flags.
- **Key differences from the vanilla DLC trial**:
  - Entirely data‑driven; no code changes required to alter flow, effects, or phase design.
  - Supports **per‑phase reset on failure** (configurable), instead of always restarting from the first phase.
  - Node destruction, mail explosion, theme switching, RAM reduction, etc. are togglable and finely parameterized.
  - Each phase can have its own music, mission file, description text (with `%` short pause and `%%` long pause).
  - Provides action hooks: `OnStart` (right after clicking begin), `OnAnimationComplete` (after all animations), `OnPhaseStart`, and failure/timeout hooks.
  - Built‑in **terminal focus effect** (configurable) that darkens the screen and highlights the terminal when a phase starts or the trial completes.
  - After completion, can **auto‑connect to a specified node** (by node ID), with optional music stop.
  - Dynamic RAM reduction adjusts window height based on visible UI controls (can also linearly reduce to a fixed target value of 84, but this is not recommended as it may cause visual glitches).
- Additional details:
  - Nodes destroyed during the trial are recorded and saved. Authors can restore them anytime using the `RestoreCustomTrialNodes` action with animated effects.
  - Supports automatically switching the theme at trial start (before spin animation). Preset theme names (e.g., `HacknetMint`) or custom theme file paths can be specified, along with the flicker duration.
- Multi‑language support (buttons, lock message, initializing text, complete/failure labels).
- All paths are relative to the extension root for safety and flexibility.

#### Configurable VM Attack System (VM Attack)

- Don't mourn the original half‑baked `systakeover` — here comes the brand‑new **VM Attack System**! Trigger a custom crash attack via the custom action `<LaunchVMAttack ConfigName="MyAttack"/>`.
- Attack configuration files are stored under `VMATK/` in the extension root, fully XML‑driven.
- **Three recovery modes**:
  - `FileDeletion`: the player must manually delete the specified file to recover (forces game exit).
  - `FileExists`: the player must create the specified file to recover (can be verified in‑game instantly).
  - `Password`: the player must enter the correct password to recover (in‑game interaction).
- **Attack flow**: after triggering, fake files are generated, a Flag is added and saved, followed by a chromatic flash effect, a delayed `crash`, then the vanilla blue screen → black screen → boot log → error injection at line 50 → 15 seconds error state → custom recovery screen.
- **Recovery screen**:
  - System log phase: multiple configurable text files are quickly echoed line‑by‑line in a monospace font (simulating the vanilla BootFailSequence).
  - Guide text phase: each line is automatically prefixed with `> `, with support for inline control markers: `||Px.x||` (pause), `||Sx.x||` (speed change), `||SR||` (reset to default speed). A "read skip" option can be enabled so that on re‑entry all text is displayed instantly.
  - Interaction phase: in file mode a button is shown (copy help file, open terminal, exit game); in password mode a password input field and submit button are shown, with multi‑language error prompts.
- **Attack removal**: in file deletion/existence modes, after the player restarts the game, if the file condition is satisfied, success music is automatically played and a single black‑screen reboot restores normality.
- **Multi‑language support**: password match/mismatch prompts and help button text are available in 10 languages.
- **Configuration highlights**: customizable error message, system log file list and intervals, guide text, help file, fake file paths and sizes, check file path and regex matching, success music, etc.

#### 📟 Custom Actions

Note: All paths appearing in Actions are relative to the extension root.

##### Actions Related to Core Features

- **`FailTrial`**: Forces the currently running CustomTrialExe trial to fail immediately.
Usage:
```xml
<FailTrial />
<!-- or -->
<FailTrial Delay="3.0" DelayHost="delayhost" />
```
- **`RestoreCustomTrialNodesAction`**: Restores nodes deleted during a previous trial with animated effects. Specify `ConfigName` to restore nodes for a specific trial.
Usage:
```xml
<RestoreCustomTrialNodes ConfigName="ExampleTrial" />
```
- **`LaunchVMAttackAction`**: Launches a specified VM attack. The `ConfigName` field identifies the configuration name.
Usage:
```xml
<LaunchVMAttack ConfigName="MyAttack" />
```

##### Other Functional Actions

- **`PlaySoundAction`**: Plays a sound effect. The `Path` attribute is required and must point to a WAV file. Other attributes (`Volume`, `Pitch`, `Pan`) are optional. Supports `Delay` and `DelayHost` (can be omitted).
Usage:
```xml
<PlaySound Path="Sounds/beep.wav" Volume="1" Pitch="0" Delay="1.5" DelayHost="delayhost"/>
```
- **`TerminalWriteAction`**: Outputs a line of text to the terminal. Supports the `text` attribute, as well as `Delay` and `DelayHost` (same as above).
Usage:
```xml
<TerminalWrite text="A delayed message" Delay="1.5" DelayHost="delayhost"/>
```
- **`TerminalTypeAction`**: Types text character‑by‑character into the terminal. Supports `Delay` and `DelayHost` (same as above).
  - `CharDelay`: interval between each character in seconds, default 0.04 (consistent with vanilla TextWriterTimed).
Usage:
```xml
<TerminalType text="Some text to type out" CharDelay="0.04" Delay="1.5" DelayHost="delayhost"/>
```
- **`TerminalFocusAction`**: Plays a terminal focus effect (full‑screen darken mask + expanding terminal border). Parameters for border animation duration, mask fade‑in time, etc. can be set independently. Supports delayed execution.
  - `Duration`: total time the mask stays visible in seconds, default 2.0. The mask disappears after this.
  - `BorderDuration`: duration of the border expansion animation in seconds, defaults to `Duration`. If shorter than `Duration`, the border disappears after the animation but the mask remains.
  - `FadeInDuration`: time for the mask to go from transparent to fully dark in seconds, defaults to `Duration`.
  - `DarkenAlpha`: maximum opacity of the mask (0~1), default 0.8.
  - `ExpandAmount`: maximum pixel expansion of the border, default 200.
Usage:
```xml
<TerminalFocus Duration="5.0" BorderDuration="2.0" FadeInDuration="0.5" DarkenAlpha="0.8" ExpandAmount="200" />
```

#### ⚙ Other Improvements
- Timer bars and titles auto‑layout: titles center when no timers are present, move up when timers appear.
- Smooth fade‑out of all UI elements when exiting.
- Locked screen (no Flag) shows localized "Trial Locked" text and an exit button, with animated grid background.
- Smart music path resolution: plain filenames, extension directory, DLC music, vanilla music.
- Many bug fixes (repeated global timeout, phase timeout not resetting, extraneous failure messages, etc.) greatly improving stability.
- New `ActionHelper` utility class standardizes action file execution logic, shared between `CustomTrialExe` and the VM system for improved stability.
- New `MusicPathResolver` utility class unifies music file path resolution for extensions (fixes the issue where music could not be played when the extension folder name differed from the extension name).
- New `SoundHelper` utility class supports playing WAV sound effects from within the extension.

#### 🚧 Planned
- Customizable Porthack heart daemon (including post‑Porthack action configuration)
- (Completed, pending testing) Customizable aircraft daemon from the DLC (fully recreatable, also allows configuring crash duration, firmware name for staying airborne, etc.)

### Installation & Usage

1. **Install the mod**  
   Place the compiled `KernelExtensions.dll` into your extension's `Plugins` folder (e.g., `Extensions/YourExtension/Plugins/`). **This mod must run as part of an extension**; global plugin mode is not supported.

2. **Prepare configuration files**  
   Create a `Trial` folder in your extension root. Place your trial configuration XML file there (e.g., `Trial/MyTrial.xml`).  
   All other related files (action files, description text files, mission files) should also be placed under your extension root using relative paths.

3. **Select which trial to run (via Flag)**  
   - **Important**: Flags can only be added/removed via Actions, not via console commands.
   - In your mission or action files, use `<RunFunction FunctionName="addFlags:CustomTrial_MyTrial"/>` to set the Flag.
   - When the player runs `CustomTrial`, the program will look for a Flag starting with `CustomTrial_` and use the suffix as the config name (e.g., `CustomTrial_MyTrial` → loads `MyTrial.xml`).
   - If no such Flag is set, the program window will show "Trial Locked" text and cannot start.

4. **Launch in game**  
   After ensuring `CustomTrial.exe` is present in the player's `bin/` folder, type `CustomTrial` in the terminal to start the trial program.

### Guide for Extension Authors (Mission Creators)

#### I. System Overview

KernelExtensions currently provides two major configurable systems, both triggered via the Flag mechanism and driven by XML configuration files:
- **Custom Trial (CustomTrial)**: multi‑phase mission challenges with effects, timers, music, node destruction, etc.
- **VM Attack (VM Attack)**: simulates a virtual machine crash attack, forcing the player to interact with the real file system to remove the lock.

Configuration files for both systems are stored in dedicated folders under the extension root (`Trial/` and `VMATK/`). All paths are relative to the extension root.

#### II. How to Set Flags to Choose a Configuration

**General rule**: both systems are controlled by vanilla Flags. You need to use `RunFunction` in tasks or Actions to add the Flag.

**Trial system**:
```xml
<Instantly>
  <RunFunction FunctionName="addFlags:CustomTrial_MyTrial"/>
</Instantly>
```
Flags must start with `CustomTrial_`, followed by the config file name (without `.xml`). When the `CustomTrial` program runs, it automatically finds and loads the corresponding `Trial/<name>.xml`. The config Flag is automatically removed after the trial succeeds.

**VM Attack system**:
```xml
<Instantly>
  <LaunchVMAttack ConfigName="MyAttack" />
```
Use the custom action `LaunchVMAttack` directly, passing the config file name (without `.xml`). The program will automatically load `VMATK/<ConfigName>.xml` and trigger the attack.

The Flag for VM attacks is managed automatically by the system and does not need to be removed manually.

#### III. Writing Action Files

In trial config nodes like `<OnPhaseStart>`, `<OnComplete>`, `<OnFail>`, etc., you can specify an Action XML file to execute custom logic. The VM attack also supports executing an action synchronously when the guide text starts playing (specified via `<ActionOnGuideTextStart>`).

**Important notes about the `Delay` attribute**:
- For most actions (e.g., `RunFunction`, `SwitchToTheme`, `ChangeAlertIcon`), if `Delay > 0` is set, you **must** also specify a `DelayHost` (a node that has a `FastActionHost` daemon, such as `playerComp` or any existing server).
- If `Delay <= 0`, the action executes immediately.
- **Exception**: `AddIRCMessage` supports the `Delay` attribute without needing a `DelayHost`; it relies on the target IRC/DHS node itself. `Delay` can be negative, making the message appear as if sent in the past.

**Example**:
```xml
<ConditionalActions>
  <OnConnect target="TMSDHS">
    <AddIRCMessage Author="LDTchara" TargetComp="TMSDHS" Delay="-7">I worked hard to escape that stupid trace</AddIRCMessage>
    <AddIRCMessage Author="BruhSolar" TargetComp="TMSDHS" Delay="13">@#PLAYERNAME# you're finally here</AddIRCMessage>
    <ChangeAlertIcon Target="TMSDHS" Type="irchub" DelayHost="Cheat" Delay="1.0"/>
    <AddConditionalActions Filepath="Actions/TMS1.xml" DelayHost="TMSDHS" Delay="22"/>
  </OnConnect>
</ConditionalActions>
```

For more available actions, refer to the official Hacknet example extension `IntroExtension`.

#### IV. Configuration File Structure

**Trial configuration**: see the repository file `XMLExample/ExampleTrial.xml` for details.

**VM Attack configuration**: see the repository file `XMLExample/MyAttack.xml` for details. Key configurable items include:
- **Recovery mode**: `FileDeletion` (delete file), `FileExists` (create file), `Password` (enter password)
- **System logs**: multiple text file paths, quickly echoed in monospace font on the recovery screen (simulating system crash logs)
- **Guide text**: supports `||Px.x||` pauses, `||Sx.x||` speed changes, `||SR||` speed reset
- **Read‑skip marker**: when enabled, after the first full read the text is displayed instantly on re‑entry
- **Help file**: copied to the save directory and opened when the button is clicked
- **Fake files**: specify file paths, sizes, or source files to generate
- **Success music**: played after the attack is removed

**General notes**:
- All paths in `file` attributes are **relative to the extension root**.
- `DescriptionText` or `GuideText` can be a file path or inline text, supporting `%` and `%%` pauses.
- `MissionFile` points to a standard Hacknet mission file. When writing missions for custom trial configs, set `<mission activeCheck="true">` and `<nextMission IsSilent="true">`.
```xml
<mission id="test" activeCheck="true" shouldIgnoreSenderVerification="false">
...
<nextMission IsSilent="true"></nextMission>
<!-- Do not put content inside the nextMission element -->
<!-- Matt, why did you put the IsSilent parameter that controls the current mission into the element that loads the next mission? This is incredibly counterintuitive and bizarre. -->
```
- Custom color support: color names (e.g., `Red`), hex (e.g., `#FF0000`), or a certain person's name. Omit to use the current theme color.

#### V. Important Notes

- **Flag naming**: Trials must start with `CustomTrial_`; VM attack Flags are managed automatically by the system and do not need to be set manually.
- **Action file delay**: most actions require a `DelayHost` for delayed execution. `Delay <= 0` executes immediately. Negative `Delay` is only meaningful for `AddIRCMessage`.
- **Effect toggles**: UI flickering, mail explosion, etc. in trials can be independently switched via config; VM attacks have no additional effect toggles.
- **Multi‑language**: most UI text switches automatically based on the game language. No additional configuration is needed. (Language file support may be added later.)
- **Path requirement**: this mod **must** run as part of an extension. All file paths are resolved relative to the extension root. Global plugin mode is not supported.

### Known Limitations / Unimplemented Features

| Feature | Status | Notes |
|---------|--------|-------|
| Porthack heart daemon | ❌ Not started | Planned for future versions |
| DLC aircraft daemon customization | ⚠ Awaiting verification | Feature implemented by April_Crystal, pending integration and testing |

### Notes

- This is my first C# project and GitHub repository. There may be many shortcomings. Feedback and improvements are welcome.
- Contributions and suggestions are welcome.
- Code is AI‑assisted and may contain errors or not follow best practices. Please test thoroughly before using.
- If you encounter bugs, please submit an issue with your log file (`BepInEx/LogOutput.log`) and your configuration files.

### ❤️ Special Thanks (in no particular order)

- **April_Crystal**: created the aircraft Daemon component, provided invaluable suggestions throughout development, and contributed immensely to the mod's refinement.
- **The members of HN扩展小屋**: for actively testing and providing feedback, greatly aiding the mod's stability.