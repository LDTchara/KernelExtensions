# 虚拟机攻击系统

**虚拟机攻击** 是一个模拟系统崩溃、强制玩家与真实文件系统交互以解除锁定的可配置模块。  
通过自定义动作 `<LaunchVMAttack>` 触发，配置文件完全由 XML 驱动。

---

## 概述

- 触发方式：使用 `<LaunchVMAttack ConfigPath="VMATK/MyAttack.xml" />` 动作。
- 配置文件路径：由 `ConfigPath` 直接指定，**相对于扩展根目录**（可放任意子目录，不限于 `VMATK/`）。
- 支持三种恢复模式，可结合虚假文件、系统日志、引导文本和交互按钮构建完整的恢复流程。

---

## 基本 XML 结构

以下为简短的示例配置文件。完整示例请参阅 [MyAttack_Example.xml](https://github.com/LDTchara/KernelExtensions/blob/main/XMLExamples/MyAttack_Example.xml)。

```xml
<VMAttackConfig>
  <Mode>FileDeletion</Mode>
  <ErrorMessage>ERROR: Critical boot error loading "payload.dll"</ErrorMessage>
  <SystemLogFiles>
    <File>Docs/SystemLog1.txt</File>
    <File>Docs/SystemLog2.txt</File>
  </SystemLogFiles>
  <SystemLogPauseBetween>2.0</SystemLogPauseBetween>
  <GuideText>
    <Line>Hello, this is a guide.||P0.5||</Line>
    <Line>||S0.05||Follow the instructions.||P0.5||</Line>
    <Line>||SR||Good luck.</Line>
  </GuideText>
  <EnableGuideReadFlag>false</EnableGuideReadFlag>
  <ButtonText>Exit VM</ButtonText>
  <HelpFile>Docs/help.txt</HelpFile>
  <SuccessMusic>Music/Ambient/AmbientDrone_Clipped</SuccessMusic>
  <FakeFiles>
    <File Path="payload.dll" Size="512" />
  </FakeFiles>
  <CheckFilePath>payload.dll</CheckFilePath>
</VMAttackConfig>
```

---

## 全局配置

| 元素 | 默认值 | 描述 |
|------|--------|------|
| `Mode` | ❌ | 恢复模式：`FileDeletion`（删除文件，缺省）、`FileExists`（创建文件）、`Password`（输入密码）。 |
| `Password` | `null` | 密码模式下需要的密码。 |
| `EnableHelpDocButton` | `false` | 密码模式下是否显示「帮助文档」按钮（Windows 打开记事本 / Linux 显示在界面内）。 |
| `EnableTerminalButton` | `false` | 密码模式下是否显示「终端」按钮。 |
| `EnableHelpButton` | `false` | **已拆分，仅作兼容**：为 `true` 时等价于同时开启上面两项（旧行为）。 |
| `ErrorMessage` | `"ERROR: Critical boot error loading \"VMBootloaderTrap.dll\""` | 崩溃时显示的自定义错误消息。 |
| `SystemLogFiles` | `null` | 多个文本文件路径，在恢复界面以等宽字体快速逐行显示。 |
| `SystemLogPauseBetween` | `2.0` | 两个系统日志文件之间的停顿秒数。 |
| `GuideText` | `null` | 引导文本行列表，每行自动添加 `> ` 前缀。 |
| `ActionOnGuideTextStart` | `null` | 引导文本开始显示时执行的**动作点**（三种写法见下方「动作点」一节）。 |
| `OnRecovered` | `null` | 恢复流程完成后、**黑屏重启之前**执行一次的动作点（三种写法同上；`NONE` / 空 = 不执行）。触发时机见下方「`OnRecovered` 的触发时机」。 |
| `OnRecoveredPostBoot` | `null` | 重启完成、成功音乐已开始播放后执行一次的动作点（三种写法同上；`NONE` / 空 = 不执行）。适合需要“进入系统后”执行的动作。 |
| `EnableGuideReadFlag` | `false` | 若为 true，首次阅读完毕后，下次直接显示全文（跳过逐字动画）。 |
| `ButtonText` | `"Proceed"` | 交互按钮上显示的文本。 |
| `HelpFile` | `null` | 帮助文件路径，点击按钮时复制到存档目录并打开。 |
| `SuccessMusic` | `null` | 成功解除攻击后播放的音乐。 |
| `FakeFiles` | `null` | 攻击触发时在存档基础目录下生成的虚假文件列表。每项属性：`Path`（相对存档基础目录的路径）、`Size`（生成零字节文件的大小）、`Source`（可选，改为从扩展目录复制该文件）。 |
| `CheckFilePath` | `null` | 文件检测模式下的目标路径（相对于存档基础目录）。 |
| `CheckFilePattern` | `null` | 文件存在模式下可选：填**扩展目录下参考文件的相对路径**，要求目标文件与该参考文件**逐字节一致**（不是正则）。 |

!!! note "NONE 约定"
    字符串配置项遵循 `NONE` 约定：写 `NONE` 或留空 = 禁用 / 回退默认，不写该元素 = 使用默认值。

!!! note "FakeFiles 的生成上限"
    `Size` 只对**凭空生成**生效：单个文件 ≤ **64MB**、全部合计 ≤ **200MB**；超限的项会被**跳过并记录 Warn**，负数按 0 处理。
    `Source` 走 `File.Copy`，**不受此上限限制**（源文件已在扩展目录，且不会一次性分配大块内存）；
    解析不到有效源文件时，回落到按 `Size` 凭空生成。

---

## 路径与命名

`LaunchVMAttack` 的 `ConfigPath`，以及配置内部的各文件路径（`HelpFile`、`SystemLogFiles`、
`FakeFiles[].Source`、`CheckFilePattern` 等），都是**相对于扩展根目录**的路径。

!!! warning "大小写必须与磁盘一致"
    Linux 的文件系统**区分大小写**，`VMATK/MyAttack.xml` 与 `vmatk/myattack.xml` 是两个不同的文件。
    请统一按磁盘上的实际大小写书写——Windows 上虽然不区分、写错也能跑，但同一份内容拿到 Linux 就会找不到文件。

!!! warning "路径不得逃出扩展目录"
    `..` 穿越、绝对路径（含 Windows 盘符）都会被**拒绝**，越界时该路径按「无效」处理：
    读取类跳过并记日志，**删除类不会执行**（不会碰到扩展之外的文件）。

!!! note "感染 Flag 的生成规则"
    感染的判定 Flag 由 `ConfigPath` 推导，**原样保留路径结构**：

    - `VMATK/MyAttack.xml` → `Kernel_VMInfected_VMATK/MyAttack.xml`

    这样做是为了让崩溃后能从 Flag **无损反解**回配置文件。若把斜杠替换成下划线，
    `VMATK/A_B.xml` 与 `VMATK/A/B.xml` 会撞成同一个 Flag 而互相误判为「已感染」，因此**不做替换**。
    反斜杠会自动归一化为正斜杠，开头的 `./` 与 `/` 会被去掉。

---

## 恢复模式详解

### FileDeletion（删除文件）
- 攻击触发时生成虚假文件，玩家必须**手动删除**指定文件后才能恢复。
- 点击按钮会退出游戏，玩家需在系统文件管理器中删除对应文件，重启游戏后自动解除。

### FileExists（创建文件）
- 攻击触发时**不会**生成目标文件，玩家必须**手动创建**指定文件后才能恢复。
- 点击按钮退出游戏，创建文件后重启游戏自动解除。

### Password（密码）
- 恢复界面会出现密码输入框，玩家必须输入正确密码才能解锁。
- 密码匹配后播放成功音乐，自动重启并清除感染。
- 可配置两个辅助按钮：
    - **帮助文档**（`EnableHelpDocButton`）—— Windows 下把帮助文件复制到存档目录并用记事本打开；
      **Linux 下不弹外部程序**，直接把帮助文本逐行显示在恢复界面里（对齐原版 Unix 行为）。
    - **终端**（`EnableTerminalButton`）—— 打开一个系统终端，工作目录为该存档的攻击目录。

!!! note "Linux 下终端如何选择"
    Linux 没有唯一的「默认终端」标准（原版依赖的 `gsettings` 键在 GNOME 3 后已废弃），
    KE 按以下顺序探测，**存在才用**：

    `$TERMINAL` 环境变量 → `xdg-terminal-exec` → `x-terminal-emulator` → 各桌面自带终端
    （gnome-terminal / konsole / xfce4-terminal / mate-terminal / lxterminal / cinnamon-terminal）
    → 常见第三方（tilix / terminator / alacritty / kitty / wezterm / foot / st / urxvt）→ `xterm`。

    全部找不到时才回退原版实现。开启 `<Debug>true` 后日志会记录实际用的是哪一个。

### 恢复界面的按钮差异（按平台）

| 平台 | 恢复模式 | 按钮 |
|------|----------|------|
| Windows | 密码 | 提交 + 帮助文档 + 终端（后两者由开关控制） |
| Windows | FileDeletion / FileExists | 单一主按钮（复制帮助 + 记事本 + 终端 + 崩溃） |
| Linux | 密码 | 提交 + 帮助文档 + 终端 |
| Linux | FileDeletion / FileExists | 三个：README（帮助显示在界面内） / Terminal / Crash VM |

---

## 引导文本特殊语法

引导文本支持行内控制标记，使用 `||` 包裹：

- `||Px.x||` —— 停顿 x.x 秒（如 `||P0.5||`）
- `||Sx.x||` —— 将后续逐字速度改为 x.x 秒/字（如 `||S0.05||` 加速）
- `||SR||` —— 将逐字速度恢复为默认值（0.12 秒/字）

标记可在行内任意位置出现，不会被显示。每行开始时速度自动重置为默认值。

---

## 攻击流程

1. 调用 `<LaunchVMAttack ConfigPath="VMATK/MyAttack.xml" />`。
2. 生成虚假文件，添加 `Kernel_VMInfected_<相对路径>` Flag 并保存（例：`Kernel_VMInfected_VMATK/MyAttack.xml`）。
3. 模拟崩溃前特效（色散闪光），延迟后执行原版 `crash`。
4. 原版蓝屏 → 黑屏 → 启动日志 → 第 50 行错误注入 → 15 秒错误状态。
5. 进入自定义恢复界面：
   - 系统日志阶段（等宽字体逐行输出）
   - 引导文本阶段（逐字打印，支持变速与停顿）
   - 交互阶段（按钮或密码输入）
6. 玩家完成指定操作后，攻击解除，系统重启。

---

## 相关动作

### 触发攻击：`LaunchVMAttack`

```xml
<LaunchVMAttack ConfigPath="VMATK/MyAttack.xml" />
```

`ConfigPath` 是**相对于扩展根目录**的配置文件路径（如 `VMATK/MyAttack.xml`）；
不再需要单独的 `ConfigName`——路径本身就是身份，感染 Flag 也由它推导。

### 攻击解除后自动清理

攻击解除时会自动：
- 移除感染 Flag
- 清除已读标记和引导动作完成标记
- 删除所有虚假文件
- 播放成功音乐（若配置）
- 执行一次黑屏重启

---

## 动作点：`ActionOnGuideTextStart`、`OnRecovered` 与 `OnRecoveredPostBoot`

VM 攻击配置里有三个**动作点**，写法与其它独立配置文件系统相同 —— 三种形态可混用：

| 动作点 | 触发时机 |
|--------|----------|
| `ActionOnGuideTextStart` | 引导文本开始显示时 |
| `OnRecovered` | 恢复流程完成后、黑屏重启之前**执行一次** |
| `OnRecoveredPostBoot` | 重启完成、成功音乐开始播放后**执行一次** |

### 三种写法

```xml
<!-- ① 引用动作文件（file 属性；旧写法继续兼容） -->
<OnRecovered file="Actions/Recovered.xml" />

<!-- ② 元素文本内容 = 动作文件路径 -->
<OnRecovered>Actions/Recovered.xml</OnRecovered>

<!-- ③ 内联动作（不用单独建文件） -->
<OnRecovered>
    <Actions>
        <TerminalWrite Text="system restored" />
    </Actions>
</OnRecovered>
```

- `<Actions>` 里的动作**立即**逐个执行；`<ConditionalActions>` 等条件满足后再执行
- ① 与 ② 同时给出路径时以 **`file` 属性**为准（并记一条 Warn）；路径与内联同时写 → **先执行文件、再执行内联**
- `NONE` / 空 = 不执行（正常配置，不报错）
- 内联内容与动作文件走同一套装载逻辑，条件写法与原版一致；动作清单见[自定义 Action](./../components/actions.md)

### `OnRecovered` 的触发时机

在**恢复流程完成**时触发，三种恢复模式都会触发：感染 Flag 已移除、已读/引导动作完成标记已清除、假文件已删除、恢复界面与输入已恢复正常之后，**黑屏重启之前**。同一轮恢复只触发一次（重新感染后再恢复会再次触发）。

- `Password`：密码在界面内校验通过、`Recover()` 收尾时触发。
- `FileDeletion` / `FileExists`：玩家删除/创建文件后重启，OSLoaded 检测条件满足、清理 Flag 后、重启前触发。

### `OnRecoveredPostBoot` 的触发时机

在**重启真正完成之后**触发：`CrashModule.completeReboot()` 已经执行完，`MusicManager.playSong()` 已让成功音乐开始播放，恢复界面和黑屏都已结束。它适合需要“进入虚拟系统后”执行的动作，例如依赖系统已可交互、成功音乐已起播的剧情或 UI 动作。

- 与 `OnRecovered` 的区别：`OnRecovered` 是**重启前**（清理/准备状态）；`OnRecoveredPostBoot` 是**重启后**（进入系统后）。
- 只有恢复成功时才会挂起并执行；普通重启不会触发。
- 同一轮恢复只执行一次；重新感染并再次恢复会再次触发。
- 如果两个动作点同时配置：先执行 `OnRecovered`，重启完成后执行 `OnRecoveredPostBoot`。
- `OnRecoveredPostBoot` 里的动作如果需要持久化，仍需显式 `<SaveGame />`；该时点不会自动再存档。

---

## 多语言支持

密码匹配/不匹配提示、帮助按钮文本等 UI 文字已支持 10 种语言（同自定义试炼系统的语言列表）。

---

## 另请参阅

- [首页](./../index.md) – 返回主索引
- [VM-Attack-System (English)](./../../en/systems/vm-attack.md) – 英文版

- [配置文件](./../components/configuration.md) – 所有配置文件的集合