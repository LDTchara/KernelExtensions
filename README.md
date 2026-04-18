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

#### ✅ 已实现（实验性）
- **自定义试炼程序** (`CustomTrialExe`)
  - 基于 XML 配置的多阶段试炼
  - 基于 Flag 系统来配置使用的试炼
  - 可自定义 SpinUp 动画时长、启动音乐、试炼音乐
  - 每个阶段可独立配置：
    - 标题、副标题（显示于程序窗口中央）
    - 描述文本（支持文件或内嵌，支持 `%` 短停顿和 `%%` 长停顿）
    - 任务文件、超时时间、阶段音乐
    - **阶段开始时执行的动作** (`OnPhaseStart`)
  - 逐字打印并支持停顿效果
  - 超时或失败后重置当前阶段，并支持执行自定义动作文件
  - 可选特效：UI 闪烁（含节点随机消失，消失间隔根据节点数动态调整）、邮件图标爆炸
  - 多语言支持（根据游戏语言自动切换）
  - 若未设置任何 `CustomTrial_` Flag，程序窗口显示“试炼已锁定”文字，不显示开始按钮
  - 支持自定义程序背景、全局/阶段进度条、旋转动画颜色（支持颜色名称、十六进制或神秘彩蛋）
  - 动态内存缩减（0.3.5）  
    进入破解阶段后，可配置延迟与持续时间，平滑降低 `ramCost`（190 → 88），同时 UI 元素按比例缩放，模拟资源释放过程。
  - 节点删除持久化与恢复（0.4.0）  
    试炼中摧毁的节点会被记录并存入存档。扩展作者可通过 `RestoreCustomTrialNodes` 动作在任意时机（如任务完成后）以动画特效逐个恢复节点。
  - 主题切换配置（0.4.0）  
    支持在试炼开始时（旋转动画前）自动切换主题。可指定预设主题名称（如 `HacknetMint`）或自定义主题文件路径，并配置切换时的闪烁时长。
  - 节点摧毁后等待时间（0.4.0）  
    新增 `PostDestructionDelay` 配置项，可在节点摧毁完成后、邮件图标爆炸前插入可自定义的等待时间。
  - 动画完成后执行动作（0.4.0）  
    原 `OnStart` 更名为 `OnAnimationComplete`，执行时机移至所有动画（旋转 + 节点摧毁 + 等待 + 邮件爆炸）结束后。

#### 🚧 计划中
- 可自定义的 Porthack 心脏 Daemon（包括 Porthack 后执行的 Action 配置）
- DLC 中可自定义的飞机 Daemon（除完全还原外还允许配置坠毁时长、维持飞行的固件名称等）
- DLC 中的 VMBootloaderTrap.dll / 虚拟机级别攻击（除完全还原外还可自定义被攻击后重启显示的文本和执行的 Action、植入 DLL 的名称等）

> ⚠️ 当前版本仅实现了自定义试炼程序的核心逻辑，且未经过完整测试。Porthack 心脏和飞机 Daemon 尚在计划阶段。

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

在试炼配置的 `<OnPhaseStart file="..."/>`、`<OnComplete file="..."/>` 或 `<OnFail file="..."/>` 中，可以指定一个 Action 的 XML 文件。

**关于 `Delay` 属性的重要说明**：
- 大多数动作（如 `RunFunction`、`SwitchToTheme`、`ChangeAlertIcon` 等）如果设置了 `Delay > 0`，**必须同时指定 `DelayHost`**（一个拥有 `FastActionHost` 守护进程的节点，例如 `playerComp` 或任意已存在的服务器）。
- 如果 `Delay <= 0`，动作会立即执行，不需要 `DelayHost`。
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

更多可用动作（如 `SwitchToTheme`、`LoadMission` 等）请参考 Hacknet 官方示例扩展 `IntroExtension` 或社区中文 wiki（https://github.com/FBIKdot/Hacknet-Extension-Tutorial/blob/main/Content/Actions.md）。

#### 三、配置文件结构（`TrialConfig.xml`）

```xml
<TrialConfig>
  <!-- 全局配置 -->
  <SpinUpDuration>5.0</SpinUpDuration>                 <!-- 旋转动画时长（秒） -->
  <EnableFlickering>false</EnableFlickering>           <!-- 是否启用 UI 闪烁+节点消失特效 -->
  <EnableMailIconDestroy>false</EnableMailIconDestroy> <!-- 是否启用邮件图标爆炸 -->
  <FlickeringDuration>8.0</FlickeringDuration>         <!-- 闪烁特效持续时间 -->
  <MailIconDestroyDuration>4.5</MailIconDestroyDuration> <!-- 邮件爆炸持续时间 -->
  <EnableNodeDestruction>true</EnableNodeDestruction>   <!-- 是否允许摧毁节点（仅在 EnableFlickering 为 true 时生效） -->
  <StartMusic>Music/Ambient/dark_drone_008</StartMusic> <!-- 程序启动音乐 -->
  <TrialStartMusic>DLC/Music/snidelyWhiplash</TrialStartMusic> <!-- 点击开始后音乐 -->

  <!-- 可选：自定义颜色（不设置则使用当前主题的高亮色） -->
  <BackgroundColor>#1a1a2e</BackgroundColor>           <!-- 程序窗口背景色 -->
  <GlobalTimerColor>#ffaa44</GlobalTimerColor>         <!-- 全局进度条颜色 -->
  <PhaseTimerColor>#00ffcc</PhaseTimerColor>           <!-- 阶段进度条颜色 -->
  <SpinUpColor>#ffaa44</SpinUpColor>                   <!-- 旋转动画颜色 -->

  <!-- 内存缩减配置（0.3.5） -->
  <RamReductionDelay>5.0</RamReductionDelay>           <!-- 进入破解阶段后延迟秒数开始缩减 -->
  <RamReductionDuration>3.0</RamReductionDuration>     <!-- 缩减过程持续时间（秒），设为 0 禁用 -->

  <!-- 主题切换配置（0.4.0） -->
  <ThemeToSwitch>HacknetMint</ThemeToSwitch>           <!-- 要切换的主题（预设名称或自定义主题文件路径） -->
  <ThemeFlickerDuration>2.0</ThemeFlickerDuration>     <!-- 主题切换时的闪烁时长（秒） -->

  <!-- 节点摧毁后等待时间（0.4.0） -->
  <PostDestructionDelay>1.5</PostDestructionDelay>     <!-- 摧毁完成后、邮件爆炸前的等待秒数，0 表示不等待 -->

  <!-- 全局计时器（可选） -->
  <GlobalTimeout>300</GlobalTimeout>                   <!-- 整个试炼总时限（秒），0=无限制 -->
  <EnableGlobalTimer>true</EnableGlobalTimer>          <!-- 是否显示全局倒计时条 -->
  <OnGlobalFail file="Actions/GlobalFail.xml" />       <!-- 全局超时失败动作 -->

  <!-- 阶段计时器显示开关 -->
  <EnablePhaseTimer>true</EnablePhaseTimer>            <!-- 是否显示阶段倒计时条 -->

  <!-- 所有动画完成后执行的动作（0.4.0，取代原来的 OnStart） -->
  <OnAnimationComplete file="Actions/TrialStart.xml" />

  <Phases>
    <Phase id="0">
      <Title>第一阶段标题</Title>                       <!-- 显示在窗口中央 -->
      <Subtitle>副标题</Subtitle>                       <!-- 显示在标题下方 -->
      <DescriptionText>Docs/MyTrialDesc.txt</DescriptionText>   <!-- 支持 % 和 %% 停顿 -->
      <MissionFile>Missions/MyMission.xml</MissionFile>         <!-- 任务文件路径 -->
      <Timeout>120</Timeout>                                    <!-- 阶段超时（秒） -->
      <Music>Music/Ambient/AmbientDrone_Clipped</Music>         <!-- 本阶段音乐 -->
      <OnPhaseStart file="Actions/Phase0Start.xml" />           <!-- 阶段开始时执行 -->
      <OnComplete file="Actions/Phase0Complete.xml" />          <!-- 阶段完成时执行 -->
      <OnFail file="Actions/Phase0Fail.xml" />                  <!-- 阶段失败时执行 -->
      <EnableResetOnFail>true</EnableResetOnFail>               <!-- 失败后重置当前阶段 -->
    </Phase>
    <!-- 可添加更多 Phase -->
  </Phases>

  <OnComplete file="Actions/AllComplete.xml" />                 <!-- 全部阶段完成后执行 -->
</TrialConfig>
```

**说明**：
- 所有 `file` 属性中的路径均为**相对于扩展根目录的相对路径**。例如 `Docs/MyTrialDesc.txt` 会被解析为 `Extensions/YourExtension/Docs/MyTrialDesc.txt`。
- `DescriptionText` 可以是文件路径或内嵌文本。文件中可使用 `%`（短停顿 ~0.5秒）和 `%%`（长停顿 ~2秒）。
- `MissionFile` 指向一个有效的 Hacknet 任务 XML 文件。建议编写任务时将 `IsSilent` 属性设置为 `true`。
```xml
<nextMission IsSilent="true">Missions/NextMission.xml</nextMission>
<!-- Matt你为什么要把控制当前任务的IsSilent参数塞到加载下一个任务的元素里，这太TM反直觉了还很诡异 -->
<!-- 此句吐槽是根据社区中文wiki的信息而写的，对于IsSilent参数实际控制哪个任务本人未测试，这一行会在本人测试后或收集到确认的相关信息后删除 -->
```
- `Timeout` > 0 时，该阶段必须在指定秒数内完成，否则触发失败（可配置重置）。
- `EnableNodeDestruction`：开启闪烁特效时，节点摧毁间隔根据初始可见节点数动态计算（与原版一致）。
- 自定义颜色支持：颜色名称（如 `Red`）、十六进制（如 `#FF0000`）或者填一个人的名字之类的。若省略或留空则使用当前主题的高亮色。
- **内存缩减**：`RamReductionDuration` 设为 0 或省略可完全禁用此功能。
- **主题切换**：如果 `ThemeToSwitch` 未设置或留空，则不切换主题。预设主题名称参见 `OSTheme` 枚举（如 `HacknetBlue`、`HacknetMint` 等）。
- **节点摧毁后等待**：`PostDestructionDelay` 设为 0 或省略则立即进入邮件爆炸阶段。

#### 四、注意事项

- **Flag 命名**：必须以 `CustomTrial_` 开头，后面跟配置文件名（不含 `.xml`）。例如 `CustomTrial_MyTrial` → 加载 `MyTrial.xml`。
- **动作文件中的延迟**：大多数动作需要 `DelayHost` 才能延迟执行，`AddIRCMessage` 除外。`Delay <= 0` 时立即执行。负 `Delay` 仅对 `AddIRCMessage` 有意义（消息回溯）。
- **特效开关**：如果不希望使用 UI 闪烁或邮件爆炸，请在配置中将对应开关设为 `false`。
- **多语言**：按钮文本和锁定提示会根据游戏语言自动显示，无需额外配置。
- **路径要求**：本模组**必须**作为扩展的一部分运行（即 `ExtensionLoader.ActiveExtensionInfo` 不为空），所有文件路径均基于扩展根目录。不支持将模组作为全局插件使用。

### 已知限制 / 未实现功能

| 功能 | 状态 | 说明 |
|------|------|------|
| Porthack 心脏 Daemon | ❌ 未开始 | 计划后续版本添加 |
| DLC 飞机 Daemon 自定义 | ❌ 未开始 | 计划后续版本添加 |
| DLC VM攻击 自定义 | ❌ 未开始 | 计划后续版本添加 |
| 独立计时器样式自定义 | 💀 毙掉了 | 被进程内计时器替代 |

### 备注

- 这是我的第一个 C# 项目和 GitHub 仓库，会有相当多的缺点，欢迎指出不足之处，我会尽力改进。
- 欢迎后续贡献代码或提出建议。
- 代码由 AI 辅助生成，可能包含错误或不符合最佳实践，请自行测试后使用。
- 如果遇到 Bug，请提交 Issue 并附上日志文件（`BepInEx/LogOutput.log`）和你的配置文件。

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

#### ✅ Implemented (Experimental)
- **Custom Trial Program** (`CustomTrialExe`)
  - XML‑based multi‑phase trial configuration
  - Based on the Flag system to configure which trial is used.
  - Customizable SpinUp animation duration, start music, trial music
  - Per‑phase configuration:
    - Title, subtitle (displayed only in the program window center)
    - Description text (file or inline, supports `%` short pause and `%%` long pause)
    - Mission file, timeout, phase music
    - **Action executed when the phase starts** (`OnPhaseStart`)
  - Character‑by‑character printing with pause support
  - Timeout or failure resets the current phase and supports custom action files
  - Optional effects: UI flickering (with random node removal, interval dynamically adjusted based on visible node count), mail icon explosion
  - Multi‑language Support (auto‑detects game language)
  - If no `CustomTrial_` flag is set, the program window displays "Trial Locked" text and no start button
  - Customizable colors for background, global/phase timer bars, 和 spin animation (supports color names, hex, or secret easter egg)
  - Dynamic RAM reduction (0.3.5)  
    After entering the cracking phase, you can configure a delay and duration to smoothly reduce `ramCost` (190 → 88) while UI elements scale proportionally, simulating resource release.
  - Persistent node deletion & restoration (0.4.0)  
    Nodes destroyed during the trial are recorded and saved with the save file. Authors can use the `RestoreCustomTrialNodes` action to restore them with visual effects at any time.
  - Configurable theme switching (0.4.0)  
    Automatically switch the game theme at trial start (before the spin animation). Supports preset theme names (e.g., `HacknetMint`) or custom theme file paths, plus a flicker duration.
  - Post‑destruction delay (0.4.0)  
    New `PostDestructionDelay` setting adds a configurable wait time between node destruction and the mail icon explosion.
  - Action after all animations (0.4.0)  
    `OnStart` has been renamed to `OnAnimationComplete` and now executes after all animations (spin + node destruction + wait + mail explosion).

#### 🚧 Planned
- Customizable Porthack heart daemon (including actions after Porthacking)
- Customizable aircraft daemon from the DLC (full restore plus configurable crash duration, firmware name for persistent flight, etc.)
- VM‑level attack / VMBootloaderTrap.dll from the DLC (full restore plus configurable reboot text, actions to execute, implanted DLL name, etc.)

> ⚠️ Currently only the custom trial program core logic is implemented and not fully tested. Porthack heart and aircraft daemon are still in the planning stage.

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

In your trial config's `<OnPhaseStart file="..."/>`, `<OnComplete file="..."/>` or `<OnFail file="..."/>`, specify an action XML file.

**Important notes about the `Delay` attribute**:
- For most actions (e.g., `RunFunction`, `SwitchToTheme`, `ChangeAlertIcon`), if `Delay > 0` is set, you **must** also specify a `DelayHost` (a node that has a `FastActionHost` daemon, such as `playerComp` or any existing server).
- If `Delay <= 0`, the action executes immediately and does not require a `DelayHost`.
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

```xml
<TrialConfig>
  <!-- Global settings -->
  <SpinUpDuration>5.0</SpinUpDuration>                 <!-- Spin animation duration (seconds) -->
  <EnableFlickering>false</EnableFlickering>           <!-- Enable UI flickering + node removal -->
  <EnableMailIconDestroy>false</EnableMailIconDestroy> <!-- Enable mail icon explosion -->
  <FlickeringDuration>8.0</FlickeringDuration>         <!-- Flickering effect duration -->
  <MailIconDestroyDuration>4.5</MailIconDestroyDuration> <!-- Mail explosion duration -->
  <EnableNodeDestruction>true</EnableNodeDestruction>   <!-- Allow node destruction (only when EnableFlickering is true) -->
  <StartMusic>Music/Ambient/dark_drone_008</StartMusic> <!-- Music before clicking start -->
  <TrialStartMusic>DLC/Music/snidelyWhiplash</TrialStartMusic> <!-- Music after clicking start -->

  <!-- Optional custom colors (omit or leave empty to use current theme's highlight color) -->
  <BackgroundColor>#1a1a2e</BackgroundColor>           <!-- Program window background -->
  <GlobalTimerColor>#ffaa44</GlobalTimerColor>         <!-- Global timer bar color -->
  <PhaseTimerColor>#00ffcc</PhaseTimerColor>           <!-- Phase timer bar color -->
  <SpinUpColor>#ffaa44</SpinUpColor>                   <!-- Spin animation color -->

  <!-- RAM reduction settings (0.3.5) -->
  <RamReductionDelay>5.0</RamReductionDelay>           <!-- Delay before starting RAM reduction after cracking phase -->
  <RamReductionDuration>3.0</RamReductionDuration>     <!-- Duration of RAM reduction (seconds); set 0 to disable -->

  <!-- Theme switching settings (0.4.0) -->
  <ThemeToSwitch>HacknetMint</ThemeToSwitch>           <!-- Theme to switch to (preset name or custom theme file path) -->
  <ThemeFlickerDuration>2.0</ThemeFlickerDuration>     <!-- Flicker duration when switching theme (seconds) -->

  <!-- Post‑destruction delay (0.4.0) -->
  <PostDestructionDelay>1.5</PostDestructionDelay>     <!-- Wait time (seconds) after node destruction before mail explosion; 0 = no wait -->

  <!-- Global timer (optional) -->
  <GlobalTimeout>300</GlobalTimeout>                   <!-- Total trial time limit (seconds), 0 = no limit -->
  <EnableGlobalTimer>true</EnableGlobalTimer>          <!-- Show global countdown bar -->
  <OnGlobalFail file="Actions/GlobalFail.xml" />       <!-- Action on global timeout -->

  <!-- Phase timer display switch -->
  <EnablePhaseTimer>true</EnablePhaseTimer>            <!-- Show phase countdown bar -->

  <!-- Action executed after all animations (0.4.0, replaces OnStart) -->
  <OnAnimationComplete file="Actions/TrialStart.xml" />

  <Phases>
    <Phase id="0">
      <Title>Phase 1 Title</Title>                     <!-- Displayed in window center -->
      <Subtitle>Subtitle</Subtitle>                    <!-- Displayed below title -->
      <DescriptionText>Docs/MyTrialDesc.txt</DescriptionText>   <!-- Supports % and %% pauses -->
      <MissionFile>Missions/MyMission.xml</MissionFile>         <!-- Mission file path -->
      <Timeout>120</Timeout>                                    <!-- Phase timeout (seconds) -->
      <Music>Music/Ambient/AmbientDrone_Clipped</Music>         <!-- Phase music -->
      <OnPhaseStart file="Actions/Phase0Start.xml" />           <!-- Executed when phase starts -->
      <OnComplete file="Actions/Phase0Complete.xml" />          <!-- Executed on phase completion -->
      <OnFail file="Actions/Phase0Fail.xml" />                  <!-- Executed on phase failure -->
      <EnableResetOnFail>true</EnableResetOnFail>               <!-- Reset phase on failure -->
    </Phase>
    <!-- More phases can be added -->
  </Phases>

  <OnComplete file="Actions/AllComplete.xml" />                 <!-- Action after all phases -->
</TrialConfig>
```

**Notes**:
- All `file` attributes use **paths relative to the extension root**. For example, `Docs/MyTrialDesc.txt` will be resolved to `Extensions/YourExtension/Docs/MyTrialDesc.txt`.
- `DescriptionText` can be a file path or inline text. Supports `%` (short pause ~0.5s) and `%%` (long pause ~2s).
- `MissionFile` must point to a valid Hacknet mission XML. It is recommended to set `<nextMission IsSilent="true">` to avoid unwanted mission switch messages.
```xml
<nextMission IsSilent="true">Missions/NextMission.xml</nextMission>
<!-- Why does Matt put the IsSilent parameter that controls the current mission into the element that loads the next mission? It's fucking counterintuitive and weird. -->
<!-- This complaint is based on information from the community Chinese wiki. Which mission the IsSilent parameter actually controls has not been tested by me. This line will be removed after I test it or receive confirmed information. -->
```
- `Timeout` > 0 forces the mission to be completed within that many seconds; otherwise, failure is triggered (reset configurable).
- `EnableNodeDestruction`: When flickering is enabled, node removal interval is dynamically calculated based on the initial visible node count (same as vanilla).
- Custom colors support: color names (e.g., `Red`), hex (e.g., `#FF0000`), or someone's name, idk. Omit or leave empty to use current theme's highlight color.
- **RAM reduction**: Set `RamReductionDuration` to 0 or omit to disable this feature.
- **Theme switching**: If `ThemeToSwitch` is omitted or left empty, no theme switching occurs. Preset theme names correspond to the `OSTheme` enum (e.g., `HacknetBlue`, `HacknetMint`).
- **Post‑destruction delay**: Set `PostDestructionDelay` to 0 or omit to proceed immediately to the mail explosion.

#### 4. Important Notes

- **Flag naming**: Must start with `CustomTrial_`, followed by the config file name (without `.xml`). E.g., `CustomTrial_MyTrial` → loads `MyTrial.xml`.
- **Delay in actions**: Most actions require a `DelayHost` to be delayed; `AddIRCMessage` is an exception. `Delay <= 0` executes immediately. Negative `Delay` only has special meaning for `AddIRCMessage` (backdated messages).
- **Effects toggle**: Set `EnableFlickering` and `EnableMailIconDestroy` to `false` if you don't want those effects.
- **Multi-language**: The start button text and lock message are automatically localized based on the game's language.
- **Path requirement**: This mod **must** run as part of an extension (i.e., `ExtensionLoader.ActiveExtensionInfo` must not be null). All file paths are resolved relative to the extension root. Global plugin mode is not supported.

### Known Limitations / Unimplemented Features

| Feature | Status | Notes |
|---------|--------|-------|
| Porthack heart daemon | ❌ Not started | Planned for future versions |
| DLC aircraft daemon customization | ❌ Not started | Planned for future versions |
| DLC VM attack customization | ❌ Not started | Planned for future versions |
| Customizable timer bar style | 💀 Killed | Replaced by in-process timer |

### Notes

- This is my first C# project and GitHub repository. There may be many shortcomings. Feedback and improvements are welcome.
- Contributions and suggestions are welcome.
- Code is AI‑assisted and may contain errors or not follow best practices. Please test thoroughly before using.
- If you encounter bugs, please submit an issue with your log file (`BepInEx/LogOutput.log`) and your configuration files.
