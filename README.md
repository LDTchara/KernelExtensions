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

#### ✅ 已实现

##### 🧪 自定义试炼程序（稳定运行）
- 基于 XML 配置的多阶段试炼，通过 Flag 系统选择试炼配置。
- **与原版 DLC 试炼的主要不同**：
  - 完全由外部 XML 驱动，无需修改代码即可改变试炼流程、特效和阶段设计。
  - 支持单阶段超时或失败后**重置当前阶段**（可配置），而非原版的回溯到第一关。
  - 节点摧毁特效、邮件爆炸、主题切换、内存缩减等均通过开关控制，并细化到持续时间、延迟等参数。
  - 各阶段可独立指定背景音乐、任务文件、描述文本（支持 `%` 短停顿和 `%%` 长停顿）。
  - 提供**阶段开始时执行动作** (`OnPhaseStart`) 与**动画全部完成后执行动作** (`OnAnimationComplete`)，以及全局超时、阶段失败等多个动作挂载点。
  - 内置**终端聚焦特效**（可配置），在阶段开始或试炼完成时全屏变暗并突出终端。
  - 试炼完成后可**自动连接到指定节点**（如 DHS 服务器），并控制是否停止音乐。
  - 动态内存缩减功能使程序窗口高度根据内部控件自动调整。
- 多语言支持（按钮、锁定提示、初始化文字、完成/失败提示）。
- 所有路径基于扩展根目录，安全且灵活。

##### 📟 自定义 Action（0.4.4）
- **`FailTrial`**：强制当前正在运行的 CustomTrialExe 试炼立即失败。
用法：
```xml
<FailTrial />或<FailTrial Delay="3.0" DelayHost="delayhost" />
```
- **`TerminalWriteAction`**：向终端输出一行文本，支持 `text` 属性，可配合 `Delay` 与 `DelayHost` 延迟输出（可省略）。
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
- **`RestoreCustomTrialNodesAction`**：恢复之前试炼中删除的节点，并以动画特效逐个显示。指定 `ConfigName` 即可对应恢复特定试炼的节点。
- 用法：
```xml
<RestoreCustomTrialNodes ConfigName="ExampleTrial" />
```

##### 🗃 节点删除持久化与恢复（0.4.0）
- 试炼中摧毁的节点会被记录并存入存档。扩展作者可通过 `RestoreCustomTrialNodes` 动作在任意时机（如任务完成后）以动画特效逐个恢复节点。

##### 🎨 主题切换配置（0.4.0）
- 支持在试炼开始时（旋转动画前）自动切换主题。可指定预设主题名称（如 `HacknetMint`）或自定义主题文件路径，并配置切换时的闪烁时长。

##### ⏱ 动态内存缩减（0.4.4）
- 进入破解阶段后，可配置延迟与持续时间，窗口高度自动缩减至合适大小（也可选择固定目标值）。UI 元素随尺寸平滑缩放。

##### 🔗 试炼完成转连（0.4.4）
- 全部阶段完成并输出描述文本后，可配置自动连接到指定节点（通过节点 ID 查找 IP），并可选是否在转连前停止音乐。

##### ⚙ 其他改进
- 计时条与标题自适应布局，无计时条时标题居中，有计时条时自动上移。
- 退出程序时界面元素平滑淡出。
- 锁定界面（无 Flag 时）显示本地化“试炼已锁定”及退出按钮，并绘制动态网格背景。
- 音乐路径智能解析，支持纯文件名、扩展目录、DLC 音乐、原版音乐。
- 修复大量错误（全局超时重复触发、阶段超时不重置、失败时多余终端信息等），稳定性大幅提升。

#### 🚧 计划中
- 可自定义的 Porthack 心脏 Daemon（包括 Porthack 后执行的 Action 配置）
- DLC 中可自定义的飞机 Daemon（除完全还原外还允许配置坠毁时长、维持飞行的固件名称等）
- DLC 中的 VMBootloaderTrap.dll / 虚拟机级别攻击（除完全还原外还可自定义被攻击后重启显示的文本和执行的 Action、植入 DLL 的名称等）

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

#### 一、如何设置 Flag 来选择试炼配置

试炼由原版的 Flag 系统控制。你需要在任务或 Action 中使用 `RunFunction` 来添加 Flag。示例：

```xml
<Instantly>
  <RunFunction FunctionName="addFlags:CustomTrial_MyTrial"/>
</Instantly>
```

这会将 `CustomTrial_MyTrial` Flag 添加到当前存档。之后玩家运行 `CustomTrial` 时，程序就会加载 `MyTrial.xml`。

如果需要移除 Flag（例如试炼完成后），可以使用：
```xml
<Instantly>
  <RunFunction FunctionName="removeFlags:CustomTrial_MyTrial"/>
</Instantly>
```

#### 二、编写动作文件（用于阶段开始/完成/失败时执行）

在试炼配置的 `<OnPhaseStart file="..."/>`、`<OnComplete file="..."/>` 或 `<OnFail file="..."/>` 等中，可以指定一个 Action 的 XML 文件。

**关于 `Delay` 属性的重要说明**：
- 大多数动作（如 `RunFunction`、`SwitchToTheme`、`ChangeAlertIcon` 等）如果设置了 `Delay > 0`，**必须同时指定 `DelayHost`**（一个拥有 `FastActionHost` 守护进程的节点，例如 `playerComp` 或任意已存在的服务器）。
- 如果 `Delay <= 0`，动作会立即执行。
- **特例**：`AddIRCMessage` 支持 `Delay` 属性且不需要 `DelayHost`，它会依赖目标 IRC/DHS 节点自身实现延迟。此外，`AddIRCMessage` 的 `Delay` 可以为负数，表示消息的发布时间回溯（即显示为较早发出的消息）。

**示例**（包含正负 `Delay` 和 `DelayHost` 的使用）：

```xml
<ConditionalActions>
  <OnConnect target="TMSDHS">
    <!-- AddIRCMessage 不需要 DelayHost，负数 Delay 使消息显示为过去 -->
    <AddIRCMessage Author="LDTchara" TargetComp="TMSDHS" Delay="-7">我废了挺大劲才解除那个弱智追踪</AddIRCMessage>
    <AddIRCMessage Author="LDTchara" TargetComp="TMSDHS" Delay="-6">感觉EnTech的人想把咱们一锅端了</AddIRCMessage>
    <!-- 需要 DelayHost 的动作 -->
    <ChangeAlertIcon Target="TMSDHS" Type="irchub" DelayHost="Cheat" Delay="1.0"/>
    <AddIRCMessage Author="BruhSolar" TargetComp="TMSDHS" Delay="13">@#PLAYERNAME# 你终于来了，我们等你很久了</AddIRCMessage>
    <AddIRCMessage Author="BruhSolar" TargetComp="TMSDHS" Delay="20">我们现在正在进行一些任务</AddIRCMessage>
    <AddConditionalActions Filepath="Actions/TMS1.xml" DelayHost="TMSDHS" Delay="22"/>
    <!-- 注意：Delay 相同的动作执行顺序不确定，建议避免相等 -->
  </OnConnect>
</ConditionalActions>
```

更多可用动作（如 `SwitchToTheme`、`LoadMission` 等）请参考 Hacknet 官方示例扩展 `IntroExtension` 或社区中文 wiki https://github.com/FBIKdot/Hacknet-Extension-Tutorial/blob/main/Content/Actions.md

#### 三、配置文件结构（`TrialConfig.xml`）
*此部分已移除，详见仓库内`XMLExample/ExampleTrial.xml`*

**说明**：
- 所有 `file` 属性中的路径均为**相对于扩展根目录的相对路径**。例如 `Docs/MyTrialDesc.txt` 会被解析为 `Extensions/YourExtension/Docs/MyTrialDesc.txt`。
- `DescriptionText` 可以是文件路径或内嵌文本。文件中可使用 `%`（短停顿 ~0.5秒）和 `%%`（长停顿 ~2秒）。
- `MissionFile` 指向一个有效的 Hacknet 任务 XML 文件。编写任务时需将`<mission>` 的 `activeCheck`和`<nextMission>` 的 `IsSilent` 属性设置为 `true`。
```xml
<mission id="test" activeCheck="true" shouldIgnoreSenderVerification="false">
...
<nextMission IsSilent="true"></nextMission>
<!-- 请勿填写nextMission元素的内容-->
<!-- Matt 你为什么要把控制当前任务的 IsSilent 参数塞到加载下一个任务的元素里，这太 TM 反直觉了还很诡异 -->
```
- `Timeout` > 0 时，该阶段必须在指定秒数内完成，否则触发失败（可配置重置）。
- `EnableNodeDestruction`：开启闪烁特效时，节点摧毁间隔根据初始可见节点数动态计算（与原版一致）。
- 自定义颜色支持：颜色名称（如 `Red`）、十六进制（如 `#FF0000`）或者填一个人的名之类的。若省略或留空则使用当前主题的高亮色。
- **内存缩减**：`RamReductionDuration` 设为 0 或省略可完全禁用此功能。
- **主题切换**：如果 `ThemeToSwitch` 未设置或留空，则不切换主题。预设主题名称参见 `OSTheme` 枚举（如 `HacknetBlue`、`HacknetMint` 等）。
- **节点摧毁后等待**：`PostDestructionDelay` 设为 0 或省略则立即进入邮件爆炸阶段。
- **终端聚焦**：可通过 `EnablePhaseStartFocus` 和 `EnableTrialCompleteFocus` 独立控制阶段开始和试炼完成时的聚焦特效。
- **转连**：`ConnectTarget` 填写节点 ID，完成后自动执行 `connect <ip>`。若留空则不转连。

#### 四、注意事项

- **Flag 命名**：必须以 `CustomTrial_` 开头，后面跟配置文件名（不含 `.xml`）。例如 `CustomTrial_MyTrial` → 加载 `MyTrial.xml`。
- **动作文件中的延迟**：大多数动作需要 `DelayHost` 才能延迟执行。`Delay <= 0` 时立即执行。负 `Delay` 仅对 `AddIRCMessage` 有意义（消息回溯）。
- **特效开关**：如果不希望使用 UI 闪烁或邮件爆炸，请在配置中将对应开关设为 `false`。
- **多语言**：按钮文本和锁定提示会根据游戏语言自动显示，无需额外配置。（后续会增加语言文件支持）
- **路径要求**：本模组**必须**作为扩展的一部分运行（即 `ExtensionLoader.ActiveExtensionInfo` 不为空），所有文件路径均基于扩展根目录。不支持将模组作为全局插件使用。

### 已知限制 / 未实现功能

| 功能 | 状态 | 说明 |
|------|------|------|
| Porthack 心脏 Daemon | ❌ 未开始 | 计划后续版本添加 |
| DLC 飞机 Daemon 自定义 | ❌ 未开始 | 计划后续版本添加 |
| DLC VM 攻击 自定义 | ❌ 未开始 | 计划后续版本添加 |

### 备注

- 这是我的第一个 C# 项目和 GitHub 仓库，会有相当多的缺点，欢迎指出不足之处，我会尽力改进。
- 欢迎后续贡献代码或提出建议。
- 代码由 AI 辅助生成，可能包含错误或不符合最佳实践，请自行测试后使用。
- 如果遇到 Bug，请提交 Issue 并附上日志文件（`BepInEx/LogOutput.log`）和你的配置文件。

### ❤️ 特别感谢（不分先后）

- **April_Crystal**：在开发过程中提出了大量宝贵建议，如相对路径支持、动态内存调整等，对模组的完善贡献巨大。
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

#### ✅ Implemented

##### 🧪 Custom Trial Program (Stable)
- Multi‑phase trial driven by XML configuration, selected via Flags.
- **Key differences from the vanilla DLC trial**:
  - Entirely data‑driven; no code changes required to alter flow, effects, or phase design.
  - Supports **per‑phase reset on failure** (configurable), instead of always restarting from the first phase.
  - Node destruction, mail explosion, theme switching, RAM reduction, etc. are togglable and finely parameterized.
  - Each phase can have its own music, mission file, description text (with `%` short pause and `%%` long pause).
  - Provides action hooks: `OnStart` (right after clicking begin), `OnAnimationComplete` (after all animations), `OnPhaseStart`, and failure/timeout hooks.
  - Built‑in **terminal focus effect** (configurable) that darkens the screen and highlights the terminal when a phase starts or the trial completes.
  - After completion, can **auto‑connect to a specified node** (e.g., DHS server), with optional music stop.
  - Dynamic RAM reduction adjusts window height based on visible UI controls.
- Multi‑language support (buttons, lock message, initializing text, complete/failure labels).
- All paths are relative to the extension root for safety and flexibility.

##### 📟 Custom Actions (0.4.4)
- **`TerminalWriteAction`**: Outputs a line of text to the terminal. Supports `text` attribute, with optional `Delay` and `DelayHost`.
  ```xml
  <TerminalWrite text="A delayed message" Delay="1.5" DelayHost="delayhost"/>
  ```
- **`TerminalTypeAction`**: Types text character‑by‑character to the terminal, mimicking the vanilla `TextWriterTimed`. Supports `Delay` and `DelayHost` like other actions.
  - `CharDelay`: delay between each character (seconds), default 0.04.
  ```xml
  <TerminalType text="Some text to type out" CharDelay="0.04" Delay="1.5" DelayHost="delayhost"/>
  ```
- **`TerminalFocusAction`**: Plays a terminal focus effect (full‑screen darken mask + expanding border). Customizable parameters:
  - `Duration`: total time the mask stays visible, default 2.0 seconds.
  - `BorderDuration`: how long the border expansion animation lasts, defaults to `Duration`.
  - `FadeInDuration`: how long the mask takes to fade from transparent to full dark, defaults to `Duration`.
  - `DarkenAlpha`: maximum opacity of the mask (0~1), default 0.8.
  - `ExpandAmount`: maximum pixel expansion of the border, default 200.
  ```xml
  <TerminalFocus Duration="5.0" BorderDuration="2.0" FadeInDuration="0.5" DarkenAlpha="0.8" ExpandAmount="200" />
  ```
- **`RestoreCustomTrialNodesAction`**: Restores previously deleted nodes from a trial with animated effects. Specify `ConfigName` to restore nodes for a particular trial.
  ```xml
  <RestoreCustomTrialNodes ConfigName="ExampleTrial" />
  ```

##### 🗃 Persistent Node Deletion & Restoration (0.4.0)
- Nodes destroyed during the trial are recorded and saved. Authors can restore them anytime using the `RestoreCustomTrialNodes` action.

##### 🎨 Theme Switching (0.4.0)
- Automatically switch the game theme at trial start (before spin animation). Supports preset themes or custom theme file paths, with configurable flicker duration.

##### ⏱ Dynamic RAM Reduction (0.4.4)
- After entering the cracking phase, window height smoothly reduces to fit the content (or a fixed target). UI scales smoothly with the size change.

##### 🔗 Post‑Trial Auto‑Connect (0.4.4)
- After all phases and outro text, can automatically connect to a specified node (by node ID). Optionally stops music before connecting.

##### ⚙ Other Improvements
- Timer bars and titles auto‑layout: titles center when no timers are present, move up when timers appear.
- Smooth fade‑out of all UI elements when exiting.
- Locked screen (no Flag) shows localized "Trial Locked" text and an exit button, with animated grid background.
- Smart music path resolution: plain filenames, extension directory, DLC music, vanilla music.
- Many bug fixes (repeated global timeout, phase timeout not resetting, extraneous failure messages, etc.) greatly improving stability.

#### 🚧 Planned
- Customizable Porthack heart daemon (including post‑Porthack actions)
- Customizable aircraft daemon from the DLC (crash duration, firmware name, etc.)
- VMBootloaderTrap.dll / VM‑level attack customization (reboot text, actions, DLL name)

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
   - If no such Flag is set, the program window will show "Trial Locked" and cannot start.

4. **Launch in game**  
   After ensuring `CustomTrial.exe` is present in the player's `bin/` folder, type `CustomTrial` in the terminal to start the trial program.

### Guide for Extension Authors (Mission Creators)

#### 1. Setting the Flag to Choose a Trial

The trial is controlled by the vanilla Flag system. Use `RunFunction` in your actions to add the Flag:

```xml
<Instantly>
  <RunFunction FunctionName="addFlags:CustomTrial_MyTrial"/>
</Instantly>
```

This adds the `CustomTrial_MyTrial` flag to the current save. When the player runs `CustomTrial`, the program will load `MyTrial.xml`.

To remove the flag (e.g., after trial completion):
```xml
<Instantly>
  <RunFunction FunctionName="removeFlags:CustomTrial_MyTrial"/>
</Instantly>
```

#### 2. Writing Action Files (for phase start/complete/failure)

In your trial config's `<OnPhaseStart file="..."/>`, `<OnComplete file="..."/>`, `<OnFail file="..."/>`, etc., specify an action XML file.

**Important notes about the `Delay` attribute**:
- For most actions (e.g., `RunFunction`, `SwitchToTheme`, `ChangeAlertIcon`), if `Delay > 0` is set, you **must** also specify a `DelayHost` (a node that has a `FastActionHost` daemon, such as `playerComp` or any existing server).
- If `Delay <= 0`, the action executes immediately.
- **Exception**: `AddIRCMessage` supports the `Delay` attribute without needing a `DelayHost`; it relies on the target IRC/DHS node itself. Also, `AddIRCMessage` accepts negative `Delay` values, which make the message appear as if it was sent in the past (backdated).

**Example** (including positive/negative `Delay` and `DelayHost` usage):

```xml
<ConditionalActions>
  <OnConnect target="TMSDHS">
    <!-- AddIRCMessage does not need DelayHost; negative Delay backdates the message -->
    <AddIRCMessage Author="LDTchara" TargetComp="TMSDHS" Delay="-7">I worked hard to escape that stupid trace</AddIRCMessage>
    <AddIRCMessage Author="LDTchara" TargetComp="TMSDHS" Delay="-6">I think EnTech wants to wipe us all out</AddIRCMessage>
    <!-- Actions that require DelayHost -->
    <ChangeAlertIcon Target="TMSDHS" Type="irchub" DelayHost="Cheat" Delay="1.0"/>
    <AddIRCMessage Author="BruhSolar" TargetComp="TMSDHS" Delay="13">@#PLAYERNAME# you're finally here, we've been waiting for you</AddIRCMessage>
    <AddIRCMessage Author="BruhSolar" TargetComp="TMSDHS" Delay="20">We're currently working on some tasks</AddIRCMessage>
    <AddConditionalActions Filepath="Actions/TMS1.xml" DelayHost="TMSDHS" Delay="22"/>
    <!-- Note: actions with identical Delay values have undefined execution order; avoid if possible -->
  </OnConnect>
</ConditionalActions>
```

For more available actions (e.g., `SwitchToTheme`, `LoadMission`), refer to the official Hacknet example extension `IntroExtension`.

#### 3. Configuration File Structure (`TrialConfig.xml`)
*This section has been removed; see the repository file `XMLExample/ExampleTrial.xml` for a complete example.*

**Notes**:
- All `file` attributes use **paths relative to the extension root**. For example, `Docs/MyTrialDesc.txt` will be resolved to `Extensions/YourExtension/Docs/MyTrialDesc.txt`.
- `DescriptionText` can be a file path or inline text. Supports `%` (short pause ~0.5s) and `%%` (long pause ~2s).
- `MissionFile` must point to a valid Hacknet mission XML. When writing missions, set `activeCheck="true"` on `<mission>` and `IsSilent="true"` on `<nextMission>`.
  ```xml
  <mission id="test" activeCheck="true" shouldIgnoreSenderVerification="false">
  ...
  <nextMission IsSilent="true"></nextMission>
  <!-- Do not put content inside the nextMission element -->
  <!-- It's bizarre that the IsSilent parameter for the current mission is placed in the element that loads the next mission, but that's how it works. -->
  ```
- `Timeout` > 0 forces the phase to be completed within that many seconds; otherwise, failure is triggered (reset configurable).
- `EnableNodeDestruction`: When flickering is enabled, node removal interval is dynamically calculated based on the initial visible node count (same as vanilla).
- Custom colors support: color names (e.g., `Red`), hex (e.g., `#FF0000`), or someone's name (a secret easter egg). Omit or leave empty to use the current theme's highlight color.
- **RAM reduction**: Set `RamReductionDuration` to 0 or omit to disable this feature.
- **Theme switching**: If `ThemeToSwitch` is omitted or left empty, no theme switching occurs. Preset theme names correspond to the `OSTheme` enum (e.g., `HacknetBlue`, `HacknetMint`).
- **Post‑destruction delay**: Set `PostDestructionDelay` to 0 or omit to proceed immediately to the mail explosion.
- **Terminal focus**: Use `EnablePhaseStartFocus` and `EnableTrialCompleteFocus` to independently control the focus effect at phase start and trial completion.
- **Auto‑connect**: `ConnectTarget` takes a node ID; after completion an automatic `connect <ip>` is performed. Leave empty to disable.

#### 4. Important Notes

- **Flag naming**: Must start with `CustomTrial_`, followed by the config file name (without `.xml`). E.g., `CustomTrial_MyTrial` → loads `MyTrial.xml`.
- **Delay in actions**: Most actions require a `DelayHost` to be delayed; `AddIRCMessage` is an exception. `Delay <= 0` executes immediately. Negative `Delay` only has special meaning for `AddIRCMessage` (backdated messages).
- **Effects toggle**: Set `EnableFlickering` and `EnableMailIconDestroy` to `false` if you don't want those effects.
- **Multi‑language**: Button text and lock messages are automatically localized based on the game's language. (Language file support may be added later.)
- **Path requirement**: This mod **must** run as part of an extension (i.e., `ExtensionLoader.ActiveExtensionInfo` must not be null). All file paths are resolved relative to the extension root. Global plugin mode is not supported.

### Known Limitations / Unimplemented Features

| Feature | Status | Notes |
|---------|--------|-------|
| Porthack heart daemon | ❌ Not started | Planned for future versions |
| DLC aircraft daemon customization | ❌ Not started | Planned for future versions |
| DLC VM attack customization | ❌ Not started | Planned for future versions |

### Notes

- This is my first C# project and GitHub repository. There may be many shortcomings. Feedback and improvements are welcome.
- Contributions and suggestions are welcome.
- Code is AI‑assisted and may contain errors or not follow best practices. Please test thoroughly before using.
- If you encounter bugs, please submit an issue with your log file (`BepInEx/LogOutput.log`) and your configuration files.

### ❤️ Special Thanks (in no particular order)

- **April_Crystal**: for invaluable suggestions, such as relative path support, dynamic RAM adjustments, and countless other design improvements.
- **The members of the HN扩展小屋**: for testing and providing feedback throughout development.