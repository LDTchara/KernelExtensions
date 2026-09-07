# 虚拟机攻击系统

**虚拟机攻击** 是一个模拟系统崩溃、强制玩家与真实文件系统交互以解除锁定的可配置模块。  
通过自定义动作 `<LaunchVMAttack>` 触发，配置文件完全由 XML 驱动。

---

## 概述

- 触发方式：使用 `<LaunchVMAttack ConfigName="MyAttack" />` 动作。
- 配置文件路径：扩展根目录下的 `VMATK/<ConfigName>.xml`。
- 支持三种恢复模式，可结合虚假文件、系统日志、引导文本和交互按钮构建完整的恢复流程。

---

## 基本 XML 结构

以下为简短的示例配置文件。完整示例请参阅 [MyAttack.xml](https://github.com/LDTchara/KernelExtensions/blob/main/XMLExamples/MyAttack.xml)。

```xml
<VMAttackConfig>
  <ConfigName>MyAttack</ConfigName>
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
| `ConfigName` | 必须 | 配置名称，需与文件名（不含扩展名）及 Flag 后缀一致。 |
| `Mode` | 必须 | 恢复模式：`FileDeletion`（删除文件）、`FileExists`（创建文件）、`Password`（输入密码）。 |
| `Password` | `null` | 密码模式下需要的密码。 |
| `EnableHelpButton` | `false` | 密码模式下是否显示“帮助”按钮。 |
| `ErrorMessage` | `"ERROR: Critical boot error loading \"VMBootloaderTrap.dll\""` | 崩溃时显示的自定义错误消息。 |
| `SystemLogFiles` | `null` | 多个文本文件路径，在恢复界面以等宽字体快速逐行显示。 |
| `SystemLogPauseBetween` | `2.0` | 两个系统日志文件之间的停顿秒数。 |
| `GuideText` | `null` | 引导文本行列表，每行自动添加 `> ` 前缀。 |
| `ActionOnGuideTextStart` | `null` | 引导文本开始显示时执行的动作文件。 |
| `EnableGuideReadFlag` | `false` | 若为 true，首次阅读完毕后，下次直接显示全文（跳过逐字动画）。 |
| `ButtonText` | `"Proceed"` | 交互按钮上显示的文本。 |
| `HelpFile` | `null` | 帮助文件路径，点击按钮时复制到存档目录并打开。 |
| `SuccessMusic` | `null` | 成功解除攻击后播放的音乐。 |
| `FakeFiles` | `null` | 攻击触发时在存档基础目录下生成的虚假文件列表。 |
| `CheckFilePath` | `null` | 文件检测模式下的目标路径（相对于存档基础目录）。 |
| `CheckFilePattern` | `null` | 文件存在模式下可选的附加内容正则校验（文件内容需匹配）。 |

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
- 可搭配帮助按钮，复制帮助文件并打开终端。
- 密码匹配后播放成功音乐，自动重启并清除感染。

---

## 引导文本特殊语法

引导文本支持行内控制标记，使用 `||` 包裹：

| 标记 | 作用 | 示例 |
|------|------|------|
| `\|\|Px.x\|\|` | 停顿 x.x 秒 | `\|\|P0.5\|\|` |
| `\|\|Sx.x\|\|` | 将后续逐字速度改为 x.x 秒/字 | `\|\|S0.05\|\|`（加速） |
| `\|\|SR\|\|` | 将逐字速度恢复为默认值（0.12 秒/字） | `\|\|SR\|\|` |

标记可在行内任意位置出现，不会被显示。每行开始时速度自动重置为默认值。

---

## 攻击流程

1. 调用 `<LaunchVMAttack ConfigName="MyAttack" />`。
2. 生成虚假文件，添加 `Kernel_VMInfected_<ConfigName>` Flag 并保存。
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
<LaunchVMAttack ConfigName="MyAttack" />
```

`ConfigName` 对应 `VMATK/` 目录下的配置文件名（不含 `.xml`）。

### 攻击解除后自动清理

攻击解除时会自动：
- 移除感染 Flag
- 清除已读标记和引导动作完成标记
- 删除所有虚假文件
- 播放成功音乐（若配置）
- 执行一次黑屏重启

---

## 多语言支持

密码匹配/不匹配提示、帮助按钮文本等 UI 文字已支持 10 种语言（同自定义试炼系统的语言列表）。

---

## 另请参阅

- [首页](./../index.md) – 返回主索引
- [VM-Attack-System (English)](./../../en/systems/vm-attack.md) – 英文版
- [杂项](./../guides/misc.md) – 其他未在各大系统页面提到的东西
- [配置文件](./../components/configuration.md) – 所有配置文件的集合