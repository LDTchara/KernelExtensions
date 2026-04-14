# KernelExtensions

> **快速导航**： [中文版本](#中文版本) | [English Version](#english-version)

---

## 前置条件

在开始使用本模组前，请确保已安装：
- Hacknet 的 DLC **Labyrinths**
- **Pathfinder** 框架（5.3.4 或更高版本）

> ⚠️ **注意**：本模组代码由 AI 辅助生成，尚未经过充分测试。可能存在 Bug 或不兼容情况，请谨慎使用并欢迎反馈。

---

## 中文版本

**KernelExtensions** 是一个使用 **Pathfinder API** 的 Hacknet 模组，旨在扩展游戏中硬编码的机制。

### 功能列表

#### ✅ 已实现（实验性）
- **自定义试炼程序** (`CustomTrialExe`)
  - 基于 XML 配置的多阶段试炼
  - 可自定义 SpinUp 动画时长、启动音乐、试炼音乐
  - 每个阶段可独立配置：标题、副标题、描述文本（文件或内嵌）、任务文件、超时时间、阶段音乐
  - 逐字打印任务描述文本
  - 超时或失败后重置当前阶段，并支持执行自定义动作文件
  - 可选特效：UI 闪烁（含节点随机消失）、邮件图标爆炸
  - 屏幕正上方独立倒计时条
  - 多语言按钮（根据游戏语言自动切换）

#### 🚧 计划中
- **可配置数量的试炼程序**（通过不同 XML 文件实现多个独立试炼）
- **可自定义的 Porthack 心脏 Daemon**（包括 Porthack 后执行的 Action 配置）
- **DLC 中可自定义的飞机 Daemon**（除完全还原外还允许配置坠毁时长、维持飞行的固件名称等）
- **DLC 中的VMBootloaderTrap.dll/虚拟机级别攻击**（除完全还原外还可自定义被攻击后重启显示的文本和执行的Action、植入dll的名称等）

> ⚠️ 当前版本仅实现了自定义试炼程序的核心逻辑，且未经过完整测试。Porthack 心脏和飞机 Daemon 尚在计划阶段。

### 安装与使用

1. **安装模组**  
   将编译好的 `KernelExtensions.dll` 放入扩展的 `Plugins` 文件夹中（例如 `Extensions/YourExtension/Plugins/`）。**本模组必须作为扩展的一部分运行**，不支持全局插件模式。

2. **准备配置文件**  
   在扩展根目录下创建 `Trial` 文件夹，将试炼配置 XML 文件放入其中（例如 `Trial/MyTrial.xml`）。  
   所有其他相关文件（动作文件、描述文本文件、任务文件）也应放在扩展目录下的适当位置（路径相对于扩展根目录）。

3. **选择要运行的试炼（通过 Flag）**  
   - **重要**：Flag 只能通过 Action 添加/移除，不能使用控制台指令直接操作，若想测试请修改 Action 文件。
   - 在任务或 faction 的动作文件中，使用 `<RunFunction FunctionName="addFlags" FunctionValue="CustomTrial_MyTrial" />` 来设置 Flag。
   - 运行 `CustomTrial` 后，程序会自动查找以 `CustomTrial_` 开头的 Flag，取后面的部分作为配置文件名（如 `CustomTrial_MyTrial` → 加载 `MyTrial.xml`）。
   - 如果没有设置任何此类 Flag，则默认加载 `Default.xml`。

4. **在游戏中启动**  
   游戏内玩家节点bin/存在CustomTrial.exe后终端输入 `CustomTrial` 即可启动试炼程序。

### 给扩展作者（任务制作者）的指南

#### 一、如何设置 Flag 来选择试炼配置

试炼由原版的 Flag 系统控制。你需要在任务或 Action 中使用 `RunFunction` 来添加 Flag。示例：

```xml
<RunFunction FunctionName="addflags:CustomTrial_TEST"/>
```

这会将 `CustomTrial_MyTrial` Flag 添加到当前存档。之后玩家运行 `CustomTrial` 时，程序就会加载 `MyTrial.xml`。

如果需要移除 Flag（例如试炼完成后），可以使用：
```xml
<RunFunction FunctionName="removeflags:CustomTrial_TEST"/>
```

#### 二、编写动作文件（用于阶段完成/失败时执行）

在试炼配置的 `<OnComplete file="..."/>` 或 `<OnFail file="..."/>` 中，可以指定一个 Action 的 XML 文件。例如：

```xml
<Instantly>
  <!-- 运行函数：添加 Flag -->
  <RunFunction FunctionName="addflags:TESTTrialDone"/>
</Instantly>
```

更多可用动作（如 `AddIRCMessage`、`SwitchToTheme` 等）请参考 Hacknet 官方示例扩展 `IntroExtension`。

#### 三、配置文件结构（`TrialConfig.xml`）

```xml
<TrialConfig>
  <!-- 全局配置 -->
  <SpinUpDuration>5.0</SpinUpDuration>                 <!-- 旋转动画时长（秒） -->
  <EnableFlickering>false</EnableFlickering>           <!-- 是否启用 UI 闪烁+节点消失特效 -->
  <EnableMailIconDestroy>false</EnableMailIconDestroy> <!-- 是否启用邮件图标爆炸 -->
  <FlickeringDuration>8.0</FlickeringDuration>         <!-- 闪烁特效持续时间 -->
  <MailIconDestroyDuration>4.5</MailIconDestroyDuration> <!-- 邮件爆炸持续时间 -->
  <StartMusic>Music/Ambient/dark_drone_008</StartMusic> <!-- 程序启动音乐 -->
  <TrialStartMusic>DLC/Music/snidelyWhiplash</TrialStartMusic> <!-- 点击开始后音乐 -->

  <Phases>
    <Phase id="0">
      <Title>第一阶段标题</Title>
      <Subtitle>副标题</Subtitle>
      <DescriptionText>Docs/MyTrialDesc.txt</DescriptionText>    <!-- 相对于扩展根目录的文本文件，或直接写文本 -->
      <MissionFile>Missions/MyMission.xml</MissionFile>          <!-- 任务文件路径（相对于扩展根目录） -->
      <Timeout>120</Timeout>                                     <!-- 超时秒数，0=无 -->
      <Music>Music/Ambient/AmbientDrone_Clipped</Music>          <!-- 本阶段音乐 -->
      <OnComplete file="Actions/Phase0Complete.xml" />           <!-- 完成时执行的动作文件 -->
      <OnFail file="Actions/Phase0Fail.xml" />                   <!-- 失败时执行的动作文件 -->
    </Phase>
    <!-- 可以添加更多 Phase -->
  </Phases>

  <OnComplete file="Actions/AllComplete.xml" />                  <!-- 全部阶段完成后的动作 -->
</TrialConfig>
```

**说明**：
- 所有 `file` 属性中的路径均为**相对于扩展根目录的相对路径**。例如 `Docs/MyTrialDesc.txt` 会被解析为 `Extensions/YourExtension/Docs/MyTrialDesc.txt`。
- `DescriptionText` 可以是文件路径，也可以是直接内嵌的文本。如果文件不存在，则当作纯文本显示。
- `MissionFile` 指向一个有效的 Hacknet 任务 XML 文件。
- `Timeout` 如果大于 0，任务必须在指定秒数内完成，否则触发失败重置。

#### 四、注意事项

- **Flag 命名**：必须以 `CustomTrial_` 开头，后面跟配置文件名（不含 `.xml`）。例如 `CustomTrial_MyTrial` → 加载 `MyTrial.xml`。
- **动作文件中的延迟**：部分动作（如 `AddIRCMessage`）支持 `Delay` 属性，单位为秒。详细用法请参考官方示例扩展 `IntroExtension`。
- **特效开关**：如果不希望使用 UI 闪烁或邮件爆炸，请在配置中将对应开关设为 `false`。
- **多语言**：按钮文本会根据游戏语言自动显示，无需额外配置。
- **路径要求**：本模组**必须**作为扩展的一部分运行（即 `ExtensionLoader.ActiveExtensionInfo` 不为空），所有文件路径均基于扩展根目录。不支持将模组作为全局插件使用。

### 已知限制 / 未实现功能

| 功能 | 状态 | 说明 |
|------|------|------|
| Porthack 心脏 Daemon | ❌ 未开始 | 计划后续版本添加 |
| DLC 飞机 Daemon 自定义 | ❌ 未开始 | 计划后续版本添加 |
| 独立计时器样式自定义 | ❌ 未实现 | 位置、颜色、大小当前硬编码 |
| 多个试炼程序同时注册 | ✅ 不需要 | 通过不同 Flag 选择不同 XML 即可实现 |

### 备注

- 这是我的第一个 C# 项目和 GitHub 仓库，会有相当多的缺点，欢迎指出不足之处，我会尽力改进。
- 欢迎后续贡献代码或提出建议。
- 代码由 AI 辅助生成，可能包含错误或不符合最佳实践，请自行测试后使用。
- 如果遇到 Bug，请提交 Issue 并附上日志文件（`BepInEx/LogOutput.log`）和你的配置文件。



## English Version

**KernelExtensions** is a Hacknet mod using the **Pathfinder API**, designed to extend hardcoded mechanics in the game.

---

## Prerequisites

Before using this mod, please ensure you have installed:
- Hacknet DLC **Labyrinths**
- **Pathfinder** framework (5.3.4 or higher)

> ⚠️ **Note**: The code of this mod is AI‑assisted and has not been thoroughly tested. Bugs or incompatibilities may exist. Use with caution and feedback is welcome.

---

### Feature List

#### ✅ Implemented (Experimental)
- **Custom Trial Program** (`CustomTrialExe`)
  - XML‑based multi‑phase trial configuration
  - Customizable SpinUp animation duration, start music, trial music
  - Per‑phase configuration: title, subtitle, description text (file or inline), mission file, timeout, phase music
  - Character‑by‑character printing of mission description
  - Timeout or failure resets the current phase and supports custom action files
  - Optional effects: UI flickering (with random node disappearance), mail icon explosion
  - Independent countdown bar at the top of the screen
  - Multi‑language button (auto‑detects game language)

#### 🚧 Planned
- **Configurable number of trial programs** (multiple independent trials via different XML files)
- **Customizable Porthack heart daemon** (including actions to execute after Porthacking)
- **Customizable aircraft daemon from the DLC** (fully restore and allow configuration of crash duration, firmware name for persistent flight, etc.)
- **VM‑level attack / VMBootloaderTrap.dll from the DLC** (fully restore and allow customization of reboot text, actions to execute, implanted DLL name, etc.)

> ⚠️ Currently only the custom trial program core logic is implemented and not fully tested. Porthack heart and aircraft daemon are still in the planning stage.

### Installation & Usage

1. **Install the mod**  
   Place the compiled `KernelExtensions.dll` into your extension's `Plugins` folder (e.g., `Extensions/YourExtension/Plugins/`). **This mod must run as part of an extension**; global plugin mode is not supported.

2. **Prepare configuration files**  
   Create a `Trial` folder in your extension root. Place your trial configuration XML file there (e.g., `Trial/MyTrial.xml`).  
   All other related files (action files, description text files, mission files) should also be placed under your extension root using relative paths.

3. **Select which trial to run (via Flag)**  
   - **Important**: Flags can only be added/removed via Actions, not via console commands. If you need to test, modify an action file.
   - In your mission or faction action files, use `<RunFunction FunctionName="addFlags" FunctionValue="CustomTrial_MyTrial" />` to set the Flag.
   - When the player runs `CustomTrial` (after the executable exists in the player's `bin/`), the program will look for a Flag starting with `CustomTrial_` and use the suffix as the config name (e.g., `CustomTrial_MyTrial` → loads `MyTrial.xml`).
   - If no such Flag is set, it defaults to `Default.xml`.

4. **Launch in game**  
   After ensuring `CustomTrial.exe` is present in the player's `bin/` folder, type `CustomTrial` in the terminal to start the trial program.

### Guide for Extension Authors (Mission Creators)

#### 1. Setting the Flag to Choose a Trial

The trial is controlled by the vanilla Flag system. Use `RunFunction` in your actions to add the Flag:

```xml
<RunFunction FunctionName="addflags:CustomTrial_TEST"/>
```

To remove the Flag (e.g., after trial completion):
```xml
<RunFunction FunctionName="removeflags:CustomTrial_TEST"/>
```

#### 2. Writing Action Files (for phase completion/failure)

In your trial config's `<OnComplete file="..."/>` or `<OnFail file="..."/>`, specify an action XML file. Example:

```xml
<Instantly>
  <RunFunction FunctionName="addflags:TESTTrialDone"/>
</Instantly>
```

For more available actions (e.g., `AddIRCMessage`, `SwitchToTheme`), refer to the official Hacknet example extension `IntroExtension`.

#### 3. Configuration File Structure (`TrialConfig.xml`)

```xml
<TrialConfig>
  <!-- Global settings -->
  <SpinUpDuration>5.0</SpinUpDuration>                 <!-- Spin animation duration (seconds) -->
  <EnableFlickering>false</EnableFlickering>           <!-- Enable UI flickering + node removal -->
  <EnableMailIconDestroy>false</EnableMailIconDestroy> <!-- Enable mail icon explosion -->
  <FlickeringDuration>8.0</FlickeringDuration>         <!-- Flickering effect duration -->
  <MailIconDestroyDuration>4.5</MailIconDestroyDuration> <!-- Mail explosion duration -->
  <StartMusic>Music/Ambient/dark_drone_008</StartMusic> <!-- Music before clicking start -->
  <TrialStartMusic>DLC/Music/snidelyWhiplash</TrialStartMusic> <!-- Music after clicking start -->

  <Phases>
    <Phase id="0">
      <Title>Phase 1 Title</Title>
      <Subtitle>Subtitle</Subtitle>
      <DescriptionText>Docs/MyTrialDesc.txt</DescriptionText>    <!-- File path (relative to extension root) or inline text -->
      <MissionFile>Missions/MyMission.xml</MissionFile>          <!-- Mission file path (relative to extension root) -->
      <Timeout>120</Timeout>                                     <!-- Timeout in seconds, 0 = none -->
      <Music>Music/Ambient/AmbientDrone_Clipped</Music>          <!-- Phase‑specific music -->
      <OnComplete file="Actions/Phase0Complete.xml" />           <!-- Action file on completion -->
      <OnFail file="Actions/Phase0Fail.xml" />                   <!-- Action file on failure -->
    </Phase>
    <!-- More phases can be added -->
  </Phases>

  <OnComplete file="Actions/AllComplete.xml" />                  <!-- Action after all phases -->
</TrialConfig>
```

**Notes**:
- All `file` attributes use **paths relative to the extension root**. For example, `Docs/MyTrialDesc.txt` will be resolved to `Extensions/YourExtension/Docs/MyTrialDesc.txt`.
- `DescriptionText` can be a file path or inline text. If the file doesn't exist, it is treated as plain text.
- `MissionFile` must point to a valid Hacknet mission XML file.
- `Timeout` > 0 forces the mission to be completed within that many seconds; otherwise, failure is triggered.

#### 4. Important Notes

- **Flag naming**: Must start with `CustomTrial_`, followed by the config file name (without `.xml`). E.g., `CustomTrial_MyTrial` → loads `MyTrial.xml`.
- **Delay in actions**: Some actions (like `AddIRCMessage`) support a `Delay` attribute (in seconds). See the official `IntroExtension` for details.
- **Effects toggle**: Set `EnableFlickering` and `EnableMailIconDestroy` to `false` if you don't want those effects.
- **Multi-language**: The start button text is automatically localized based on the game's language.
- **Path requirement**: This mod **must** run as part of an extension (i.e., `ExtensionLoader.ActiveExtensionInfo` must not be null). All file paths are resolved relative to the extension root. Global plugin mode is not supported.

### Known Limitations / Unimplemented Features

| Feature | Status | Notes |
|---------|--------|-------|
| Porthack heart daemon | ❌ Not started | Planned for future versions |
| DLC aircraft daemon customization | ❌ Not started | Planned for future versions |
| Customizable timer bar style | ❌ Not implemented | Position, color, size are hardcoded |
| Multiple trial executables registration | ✅ Not needed | Use different Flags to select different XML configs |

### Notes

- This is my first C# project and GitHub repository. There may be many shortcomings. Feedback and improvements are welcome.
- Contributions and suggestions are welcome.
- Code is AI‑assisted and may contain errors or not follow best practices. Please test thoroughly before using.
- If you encounter bugs, please submit an issue with your log file (`BepInEx/LogOutput.log`) and your configuration files.
