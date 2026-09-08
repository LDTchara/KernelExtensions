# 扩展作者指南

本文档面向希望在自己的 Hacknet 扩展中使用 **KernelExtensions** 功能的制作者。  
你将了解如何设置 Flag、编写配置文件、调用动作，以及遵循一些通用规则。

---

## 一、基本认识

KernelExtensions 目前提供三大可配置系统，均通过 Flag 机制触发，由 XML 配置文件驱动：

- **自定义试炼（CustomTrial）**：多阶段任务挑战，包含特效、计时器、音乐、节点摧毁等。
- **VM 攻击（VM Attack）**：模拟虚拟机崩溃攻击，强制玩家与真实文件系统交互以解除锁定。
- **飞机 Daemon（FlightDaemon）**：可配置坠落时长与修复/坠毁动作的飞机守护进程。

所有系统**必须**作为扩展的一部分运行，不支持全局插件模式。配置文件、动作文件等所有路径均相对于**扩展根目录**。

---

## 二、Flag 的使用

两个系统的触发都依赖于 Hacknet 原生的 Flag 机制，一般情况下 Flag 只能通过 Action系统 添加/移除，不能使用控制台指令直接操作。

### 自定义试炼

在任务或动作文件中，使用以下方式设置 Flag：

```xml
<RunFunction FunctionName="addFlags:CustomTrial_MyTrial" />
```

Flag 以 `CustomTrial_` 开头，后面跟配置文件名（不含 `.xml`）。  
运行 `CustomTrial` 程序时，会自动查找并加载对应的 `Trial/<名称>.xml`。试炼成功后会自动删除该 Flag。

### VM 攻击

直接使用自定义 Action `LaunchVMAttack`，传入配置文件名（不含 `.xml`）：

```xml
<LaunchVMAttack ConfigName="MyAttack" />
```

程序将自动加载 `VMATK/<ConfigName>.xml` 并触发攻击，VM 攻击的 Flag 由系统自动管理，无需手动移除。

### 飞机 Daemon

无需额外的 Flag，只需在目标计算机的 XML 中添加 `<FlightDaemon>` 即可（详见 [飞机Daemon系统](./../systems/aircraft.md)）。相应的攻击、修复动作通过 `AttackAircraft` 和 `UploadAircraftSysFile` 触发。

---

## 三、路径与文件约定

- 所有 `file` 属性中指定的路径均为**相对于扩展根目录**的相对路径。  
  例如 `Actions/MyAction.xml` 指向 `Extensions/你的扩展名/Actions/MyAction.xml`。
- 描述文本（`DescriptionText`）和引导文本（`GuideText`）可以是文件路径，也可以是直接内嵌的文本（GuideText建议使用内嵌文本）。  
  当内容以 `.txt` 或其它扩展名结尾时，系统会尝试将其作为文件路径读取；若文件不存在，则当作普通文本显示。
- 支持 `%` 短停顿和 `%%` 长停顿（试炼描述和引导文本，但引导文本建议使用`||PX.X||`和`||SX.X||`等标识符来控制停顿和速度等），可在文本中任意位置使用。

---

## 四、任务文件的编写

试炼中的 `MissionFile` 指向标准的 Hacknet 任务 XML。  
**关键设置**：

```xml
<mission id="test" activeCheck="true" shouldIgnoreSenderVerification="false">
  ... 目标、目标、条件等 ...
  <nextMission IsSilent="true"></nextMission>
  <!-- 注意：IsSilent 属性控制的是当前任务，但必须写在 nextMission 元素里（Matt 的奇妙设计） -->
</mission>
```

- `activeCheck="true"`：让任务在玩家未连接目标时也能跟踪完成状态。（对于此项设为false的情况暂未进行过任何测试，所以强烈建议设为true以避免潜在问题。）
- `<nextMission IsSilent="true">`：任务完成后不会自动加载下一个任务（因为试炼是自己控制流程）。**不要**在 `<nextMission>` 中填写实际内容。

---

## 五、颜色自定义

所有颜色配置项（如 `BackgroundColor`、`GlobalTimerColor` 等）支持：

- 标准颜色名称（`Red`, `Blue`, `Cyan` 等）
- 十六进制格式（`#FF0000`, `#0F0`）
- 我的名字 `LDTchara`：你可以自己试试。

若留空或填写了无法识别的值，将使用当前游戏主题的高亮色。

---

## 六、音乐路径

音乐配置项（如 `StartMusic`、`TrialStartMusic`、`Music`、`SuccessMusic`）支持以下格式，由 `MusicPathResolver` 自动解析：

1. **纯文件名**（如 `AmbientDrone_Clipped`）：
   - 先在扩展根目录查找
   - 再在扩展 `Music/` 文件夹查找（与原版音乐处理机制唯一不同的点，一个没啥用的兜底机制，绿豆汤脑子抽了保留的）
   - 再在 DLC Music 文件夹查找
   - 最后作为原版音乐名处理
2. **相对路径**（如 `Music/MySong.ogg`）：基于扩展根目录解析。
3. **DLC 音乐**：如 `DLC/Music/snidelyWhiplash`，直接使用。
4. 可以省略 `.ogg` 扩展名。

---

## 七、延迟执行

许多自定义 Action 支持 `Delay` 和 `DelayHost` 属性用于延迟执行。

- `Delay`：延迟的秒数（正数）。
- `DelayHost`：提供延迟服务的主机 ID，该主机必须拥有 `FastActionHost` 守护进程。
- 若 `Delay` 为 0 或未填写，动作会立即执行，无需 `DelayHost`。
- 例外：`AddIRCMessage` 支持 `Delay` 但不需要 `DelayHost`，可为负数。

示例：
```xml
<ConditionalActions>
    <Instantly>
        <FailTrial />
    </Instantly>
</ConditionalActions>
```

---

## 八、多语言

KernelExtensions 目前对按钮和提示文字做了硬编码多语言（中文、日语、韩语、俄语、德语、法语、西班牙语、土耳其语、荷兰语、英语）。  
未来会加入基于语言文件的多语言支持，届时扩展作者可自行定制。

---

## 九、快速参考链接

- [自定义试炼系统](./../systems/custom-trial.md)
- [VM攻击系统](./../systems/vm-attack.md)
- [飞机Daemon系统](./../systems/aircraft.md)
- [自定义Action](./../components/actions.md)
- [配置文件](./../components/configuration.md)

---

> 祝你的扩展制作顺利！如果还有疑问，欢迎到 Discussions 讨论。