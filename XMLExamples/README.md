# XMLExamples — 配置示例与编写约定

> **XMLExamples** 存放 KernelExtensions 各模块的**配置示例模板**与配套资源，供扩展作者参考、复制后改造。
> 本文件提取散落在各示例注释里的**通用编写约定**；各模块的专属用法仍以对应示例文件的头部注释为权威。
>
> **XMLExamples** holds **configuration example templates** and companion resources for KernelExtensions modules, for extension authors to reference, copy and adapt.
> This file extracts the **common authoring conventions** scattered across the examples' comments; module-specific usage remains authoritative in each example file's header comment.

---

## 一、文件清单 / File Index

| 文件 / File | 模块 / Module | 说明 / Description |
|---|---|---|
| `Trial_Example.xml` | 自定义试炼 Custom Trial | 多阶段试炼配置（`<TrialConfig>`） |
| `PhaseSwift_Example.xml` | 相位穿梭 PhaseSwift | 多场景切换配置（`<PhaseSwiftConfig>`） |
| `MyAttack_Example.xml` | 虚拟机攻击 VM Attack | 崩溃攻击配置（`<VMAttackConfig>`） |
| `Clock_Example.xml` | 定时器 Clock | 定时触发 Action 序列（`<Clock>`） |
| `ExampleEnding.xml` | 自定义结局 Custom Ending | 结局流程配置（`<EndingConfig>`） |
| `CustomColor_Example.xml` | 动态颜色 CustomColor | 渐变色预设文件（`<ColorPreset>`） |
| `Speech.txt` / `CreditsData.txt` | 自定义结局 | Ending 演讲文本 / 报幕名单示例资源 |
| `EndingSpeech.wav` | 自定义结局 | Ending 语音短样本（181 KB） |
| `CustomIRCLogs_Example.txt` | — | 自定义 IRC 语录文件格式示例 |

---

## 二、示例 ≠ 运行时资源 / Examples Are NOT Runtime Assets

- 本目录位于**仓库内**而非扩展内，游戏不会加载这里的任何文件。
  This directory lives in the **repository**, not inside an extension — nothing here is loaded by the game.
- 真实使用：把对应示例文件**连同它引用的资源一起复制进扩展目录**，复制后文件内的相对路径才以「扩展根目录」为基准生效。
  Real usage: copy the example file **together with the resources it references** into your extension directory; only then do the relative paths inside resolve against the extension root.
- 示例中引用的 `Actions/`、`Missions/`、`Docs/` 等路径多为**示意路径**，需按需自建。
  Paths like `Actions/`, `Missions/`, `Docs/` inside examples are mostly **illustrative** — create them as needed.

---

## 三、通用约定 / Common Conventions

以下约定是所有配置 XML 的共同规则（出处见各示例注释）。

### 1. 编码与声明 / Encoding & Declaration

- XML 一律 UTF-8、带声明：`<?xml version="1.0" encoding="utf-8"?>`
- **文本资源（`.txt`）同样要求 UTF-8**（含中文时否则乱码；CustomIRCLogs 注释明确要求）。
  All XML files are UTF-8 with declaration; **text resources (`.txt`) must also be UTF-8**.

### 2. 单根 / Single Root

- 一个文件 = 一个配置实例：根元素即模块配置类型（`<TrialConfig>` / `<PhaseSwiftConfig>` / `<EndingConfig>` / `<VMAttackConfig>` / `<Clock>` / `<ColorPreset>`）。
- 需要展示多个变体时，把附加变体**以注释形式附在文末**（见 `Clock_Example.xml`），保证 XML 校验通过（单根）。
  One file = one config instance; the root element is the module's config type. Show extra variants **as comments at the end** (see `Clock_Example.xml`) to keep the file valid (single root).

### 3. 放置与加载 / Placement & Loading

| 模块 / Module | 放置位置 / Placement | 加载方式 / Loading |
|---|---|---|
| 自定义试炼 Trial | `扩展根/Trial/<名字>.xml`（**约定目录**） | 添加 Flag `CustomTrial_<文件名不含扩展名>` |
| 虚拟机攻击 VM Attack | `扩展根/VMATK/<ConfigName>.xml`（**约定目录**） | `<LaunchVMAttack ConfigName="<ConfigName>" />`；`<ConfigName>` 必须与文件名一致 |
| 定时器 Clock | 扩展根任意位置（惯例 `Clocks/`） | `<ClockStart Filepath="..." />`；`<ClockStop ClockID="..." />` 或按 Filepath |
| 自定义结局 Ending | 扩展根任意位置（无强制目录） | `<StartEnding File="..." />` |
| 动态颜色 CustomColor | `扩展根/CustomColor/`（自动加载目录） | 扩展启动时自动加载，主题/配置中按预设名引用 |

### 4. 路径规则 / Path Rules

- 资源路径一律**相对扩展根目录**，可放任意子目录；`Docs/`、`Missions/`、`Actions/`、`Music/` 仅为惯例命名，**非强制**（Ending 示例注释明确说明）。
  All resource paths are **relative to the extension root**, any subdirectory; `Docs/` etc. are conventions, **not mandatory**.
- 音乐等字段既支持原版**内容名**（如 `Music/Ambient/AmbientDrone_Clipped`），也支持**扩展内相对路径**（如 `Music/MySong.ogg`，可含 `../Extensions/...` 跨扩展引用）。
  Music fields accept vanilla **content names** or **relative paths** inside the extension (e.g. `Music/MySong.ogg`, or even `../Extensions/...`).

### 5. 动作文件引用 / Action File References

- 统一用 **`file` 属性**指向动作文件：`<OnStart file="Actions/OnStart.xml" />`。
- 省略该元素 = 不执行任何动作。
  Use the **`file` attribute** to reference an Action file; omit the element = no action runs.

### 6. 可选字段：省略 / NONE / 空 / 默认 / Optional Fields: omit vs NONE vs empty vs default

- **省略元素** = 使用模块默认值（最干净的写法）。
- 数值型：省略或 `0` = 禁用（如 `<Timeout>0</Timeout>`）。
- 布尔型：省略或 `false` = 禁用。
- 音乐/文件路径：省略或留空 = 按模块定义——多数模块为「不播放 / 不执行」；个别模块对特定字段提供**内置兜底默认路径**（如 Ending 的 `SpeechFile` 留空 = `Docs/EndingSpeech.wav`），留空即用该默认。
- 颜色字段：省略或留空 = 使用当前主题对应颜色。
- 动作引用：省略 = 不执行。
- 字符串可选字段（按钮文字等）：**写 `NONE` 或留空 = 显式禁用 / 回退内置本地化；不写该元素 = 使用默认值**。`NONE` 大小写不敏感。
  - Omit the element = module default. Numeric: omit or `0` = disabled. Boolean: omit or `false` = disabled.
  - Music/file path: omit or empty per module — most treat it as no-play / no-action; a few fields have a **built-in fallback path** (e.g. Ending `SpeechFile` empty = `Docs/EndingSpeech.wav`).
  - Color: omit or empty = current theme color.
  - String fields: **`NONE` or empty = explicitly disabled / falls back to built-in localisation; element absent = default value**. `NONE` is case-insensitive.

### 7. 颜色格式 / Color Formats

配置颜色字段支持以下格式（可混用，见 `CustomColor_Example.xml`）：

1. 颜色名 / Color names — `Red` 等 XNA 内置颜色名（Trial/PhaseSwift 注释）
2. 十六进制 RGB — `#FF0000`
3. 十六进制 ARGB（带透明度）— `#88FF0000`
4. 数值 RGB — `255,0,0`
5. 数值 RGBA — `255,0,0,128`

另支持 **CustomColor 预设名**（含内置 `Rainbow` / `LDTchara`），带参格式：`<字段>预设名:速度:透明度:饱和度:明度</字段>`（如 `Rainbow:0.1:0.8:1.0:1.0`；参数可省略到只用 `速度`）。自定义预设文件放 `CustomColor/` 自动加载。
CustomColor preset names (including built-in `Rainbow` / `LDTchara`) are also accepted, with parameters: `preset:speed:opacity:saturation:brightness` (optional params may be omitted).

### 8. 文本与停顿标记（各模块不同！）/ Text & Pause Markers (per-module!)

| 用途 / Use | 标记 / Markers |
|---|---|
| Trial 描述文本 `DescriptionText` / `OutroText` / `ResetText` | 支持**文件路径或内嵌文本**（文件不存在则按纯文本显示）；`%` = 短停顿（约 0.5 s）、`%%` = 长停顿（约 2 s） |
| Ending 演讲文本 `TextFile` | `#` = 停顿 1 s（段落感）、`%` = 停顿 0.5 s（短停顿），显示时被过滤 |
| Ending 报幕名单 `CreditsFile` | 每行一条：`^` = 灰小字、`%` = 大标题行、`$` = 小号灰字 |
| VM Attack 引导文本 `GuideText` | 每行自动加 `> ` 前缀；行内控制标记 `\|\|Px.x\|\|` = 停顿 x.x s、`\|\|Sx.x\|\|` = 逐字速度 x.x s/字、`\|\|SR\|\|` = 恢复默认速度（0.12 s/字）；每行开始速度重置 |

> ⚠️ 注意 `%` 在不同模块语义不同（Trial 文本 = 停顿；Ending 报幕 = 大标题行），**跨模块复用文本前请先确认目标模块的标记集**。
> ⚠️ `%` means different things per module (Trial text = pause; Ending credits = heading line) — check the target module's marker set before reusing text.

### 9. 配套资源 / Companion Resources

- 引用外部资源（语音、文本、音乐）时，示例注释必须说明**资源格式**（标记集、编码、行结构），让使用者能自建资源。
  Example comments must document the **resource format** (markers, encoding, line structure) of any referenced asset so users can author their own.
- 示例应尽量附带**真实可用的短样本**（如 `EndingSpeech.wav` 181 KB 短样本，便于直接测试），避免依赖大文件。
  Prefer shipping a **short real sample** (e.g. the 181 KB `EndingSpeech.wav`) over large placeholder files.

---

## 四、给示例维护者：注释惯例 / For Example Maintainers: Comment Style

- **中英双语成对**：同一说明给出中文与 English 两段（整段说明用成对注释块，单行短注释可写在同一行的相邻注释或 `English:` 前缀）。
  Provide **paired Chinese & English** comments (paired blocks for long notes; adjacent comment or `English:` prefix for one-liners).
- 用 `<!-- ==== 分区名 / Section Name ==== -->` 分隔主要区块。
  Separate major sections with `<!-- ==== Section Name ==== -->`.
- 头部注释交代：**定位 / 放置位置 / 触发方式 / 属性或字段速览 / 注意事项**。
  Header comments cover: **positioning / placement / trigger / attribute-field overview / caveats**.
- 公共约定（路径规则、NONE、颜色格式、编码）不再在单个示例中长篇复述——指向本 README。
  Don't re-explain the common conventions above at length in each example — point to this README.
