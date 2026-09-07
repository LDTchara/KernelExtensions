# 自定义试炼系统

**自定义试炼** 是一个灵活、由 XML 驱动的多阶段挑战框架。  
它取代了原版硬编码的 DLC 试炼，提供完全可配置的视觉特效、任务、计时器和动态 UI 元素序列。

---

## 概述

- 通过以 `CustomTrial_` 开头的 Flag 激活（例如 `CustomTrial_MyTrial`）。
- 从扩展根目录的 `Trial/<ConfigName>.xml` 加载配置。
- 可执行程序 `CustomTrial` 必须存在于玩家 `bin/` 文件夹中。

---

## 基本 XML 结构

以下为简短的示例配置文件。更完整的配置文件请参阅 [ExampleTrial.xml](https://github.com/LDTchara/KernelExtensions/blob/main/XMLExamples/ExampleTrial.xml)。

```xml
<TrialConfig>
  <ProgramName>我的试炼</ProgramName>
  <SpinUpDuration>5.0</SpinUpDuration>
  <EnableFlickering>true</EnableFlickering>
  ...
  <Phases>
    <Phase id="0">
      <Title>第一阶段</Title>
      <Subtitle>获取 root 并删除日志</Subtitle>
      <DescriptionText>Docs/phase1_desc.txt</DescriptionText>
      <MissionFile>Missions/phase1_mission.xml</MissionFile>
      <Timeout>120</Timeout>
      <Music>Music/Ambient/dark_drone_008</Music>
      <OnPhaseStart file="Actions/phase1_start.xml" />
      <OnComplete file="Actions/phase1_complete.xml" />
      <OnFail file="Actions/phase1_fail.xml" />
      <EnableResetOnFail>true</EnableResetOnFail>
      <ResetText>Docs/phase1_reset.txt</ResetText>
    </Phase>
    <!-- 更多阶段 -->
  </Phases>
</TrialConfig>
```

---

## 全局配置

| 元素 | 默认值 | 描述 |
|------|--------|------|
| `ProgramName` | `"CustomTrial"` | 可执行程序的显示名称。 |
| `SpinUpDuration` | `13.8` | 旋转动画持续时间（秒）。 |
| `EnableFlickering` | `true` | 是否在旋转动画后启用 UI 闪烁与节点摧毁效果。 |
| `FlickeringDuration` | `10` | 闪烁阶段持续时间（秒）。 |
| `EnableNodeDestruction` | `true` | 是否允许在闪烁期间随机摧毁节点。 |
| `PostDestructionDelay` | `0` | 节点摧毁结束到邮件爆炸（如果启用）之间的等待时间（秒）。 |
| `EnableMailIconDestroy` | `true` | 是否启用邮件图标爆炸效果。 |
| `MailIconDestroyDuration` | `3.82` | 邮件爆炸持续时间（秒）。 |
| `MailPhaseDarkenEnabled` | `true` | 邮件爆炸期间是否将终端外区域变暗。 |
| `EnablePhaseStartFocus` | `true` | 阶段开始时是否显示终端聚焦覆盖层。 |
| `EnableTrialCompleteFocus` | `true` | 试炼完成时是否显示终端聚焦覆盖层。 |
| `ThemeToSwitch` | `null` | 切换至的预设主题名称（如 `HacknetMint`）或自定义主题文件路径。 |
| `ThemeFlickerDuration` | `2` | 主题切换时的闪烁时长（秒）。 |
| `BackgroundColor` / `GlobalTimerColor` / `PhaseTimerColor` / `SpinUpColor` | `null` | 自定义颜色（支持名称、`#RRGGBB` 或把我的名字填进去）。 |
| `RamReductionDelay` | `5` | 阶段开始后延迟多少秒开始减少内存占用。 |
| `RamReductionDuration` | `3` | 内存缩减过程的总时长（秒）。 |
| `DynamicRamReduction` | `false` | 若为 true，则RamReductionDelay与RamReductionDuration将被忽略，ramCost 会根据当前显示的 UI 控件高度动态调整。建议设为`true`以避免潜在的视觉问题。 |
| `GlobalTimeout` | `0` | 整个试炼的总时限（秒）。0 表示无限制。 |
| `EnableGlobalTimer` | `false` | 是否显示全局倒计时条。 |
| `OnGlobalFail` | `null` | 全局超时时执行的动作文件。 |
| `StartMusic` | `null` | 点击“开始试炼”前播放的背景音乐。 |
| `TrialStartMusic` | `null` | 点击“开始试炼”后播放的音乐。 |
| `OnStart` | `null` | 点击“开始试炼”后立即执行的动作文件。 |
| `OnAnimationComplete` | `null` | 所有开场动画完成后执行的动作文件。 |
| `OnComplete` | `null` | 所有阶段成功完成后执行的动作文件。 |
| `OutroText` | `null` | 试炼结束时显示在终端的文字（文件路径或内嵌文本）。支持 `%` 短停顿和 `%%` 长停顿。 |
| `ConnectTarget` | `null` | 试炼完成后自动连接的目标节点 ID。 |
| `StopMusicOnConnect` | `true` | 连接前是否停止音乐。 |
| `EnablePhaseTimer` | `true` | 是否显示每阶段倒计时条。 |

---

## 阶段配置

每个 `<Phase>` 元素可包含以下设置：

| 元素 | 默认值 | 描述 |
|------|--------|------|
| `id` (属性) | 必须 | 阶段编号。 |
| `Title` | 必须 | 程序窗口中显示的阶段标题。 |
| `Subtitle` | 可选 | 标题下方的副标题。 |
| `DescriptionText` | 必须 | 逐字显示的描述文本（文件路径或内嵌文字）。支持 `%` 和 `%%` 停顿。 |
| `MissionFile` | 必须 | 指向 Hacknet 任务 XML 的路径。 |
| `Timeout` | `0` | 阶段时限（秒）。0 = 无限制。 |
| `Music` | `null` | 本阶段专用的背景音乐（覆盖全局音乐）。 |
| `OnPhaseStart` | `null` | 阶段开始时执行的动作文件。 |
| `OnComplete` | `null` | 阶段完成时执行的动作文件。 |
| `OnFail` | `null` | 阶段失败（超时或跟踪超时）时执行的动作文件。 |
| `EnableResetOnFail` | `false` | 若为 true，失败后重置当前阶段而不是结束试炼。 |
| `ResetText` | `null` | 阶段重置时额外显示的文本（在阶段描述之前显示）。 |
| `ExecuteOnPhaseStartOnReset` | `false` | 若为 true，阶段重置时再次执行 `OnPhaseStart` 动作。 |

---

## 相关动作

以下是与试炼系统有关的自定义动作，建议独立调用，但也可放在任何动作文件调用点（如 `OnStart`, `OnPhaseStart`, `OnFail` 等）来调用。

### 强制试炼失败：`FailTrial`

立即让当前正在运行的 `CustomTrialExe` 进入失败结局。若没有任何试炼在运行，则无效果。

```xml
<FailTrial />
```

支持延迟执行：

```xml
<FailTrial Delay="3.0" DelayHost="cheat" />
```

### 恢复被摧毁的节点：`RestoreCustomTrialNodes`

以动画特效逐个恢复之前试炼中摧毁的节点。需要指定 `ConfigName`，即当初设置 `CustomTrial_` Flag 时的配置名。

```xml
<RestoreCustomTrialNodes ConfigName="ExampleTrial" />
```

节点会在 3 秒内依次出现，并伴有高亮闪烁和扩散圆圈。

### 试炼内部动作钩子

在试炼配置 XML 中可以直接引用动作文件，这些钩子会在特定时间点触发：

| 钩子 | 触发时机 |
|------|----------|
| `OnStart` | 玩家点击“开始试炼”后立即执行。 |
| `OnAnimationComplete` | 旋转动画、闪烁、节点摧毁、邮件爆炸等所有开场动画完成后执行。 |
| `OnComplete` (全局) | 所有阶段成功完成后执行。 |
| `OnGlobalFail` | 全局计时器超时时执行。 |
| `OnPhaseStart` | 每个阶段开始时执行。 |
| `OnComplete` (阶段) | 当前阶段任务完成时执行。 |
| `OnFail` | 当前阶段超时或跟踪超时时执行。 |

所有钩子都通过 `file` 属性指定动作文件的路径（相对于扩展根目录），例如：

```xml
<OnPhaseStart file="Actions/MyPhaseStart.xml" />
```

---

## 特效与视觉效果

- **闪烁与节点摧毁**  
  启用时，节点会随机移除并伴随冲击特效。被摧毁的节点会被持久化记录，可随后使用 `RestoreCustomTrialNodes` 动作还原。

- **邮件图标爆炸**  
  完全还原原版效果，包括放射线条、颜色变化和双重爆炸。

- **终端聚焦覆盖层**  
  使终端之外的全屏变暗，并绘制扩展边框。可在阶段开始和试炼完成时启用。

- **主题切换**  
  在试炼开始时（旋转动画后），可将主题切换为预设或自定义文件。

- **动态内存缩减**  
  若启用，窗口高度会缩小至刚好容纳剩余 UI（标题、计时器等），从而降低内存占用。

---

## 持久化

- 已删除节点的索引保存在全局 `CustomTrialNodeStorage` 中，并写入存档文件。
- `CustomTrialSaveExecutor` 会在读档时重新加载它们。
- 使用 `RestoreCustomTrialNodes` 动作可通过特效恢复这些节点。

---

## 多语言支持

按钮和标签（如“开始试炼”、“试炼已锁定”、“正在初始化”、“完成”、“失败”、“退出”）会根据 `Settings.ActiveLocale` 动态切换。  
目前支持的语言：中文、日语、韩语、俄语、德语、法语、西班牙语、土耳其语、荷兰语、英语。  
未来会加入基于语言文件的多语言支持。

---

## 另请参阅

- [首页](./../index.md) – 返回主索引
- [Custom-Trial-System (English)](./../../en/systems/custom-trial.md) – 英文版
- [杂项 (Misc)](./../guides/misc.md) – 其他未在各大系统页面提到的东西
- [配置文件](./../components/configuration.md) – 所有配置文件的集合