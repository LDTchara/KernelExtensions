# AGENTS.md

> 本文件面向**在此仓库工作的 AI 助手**，只列「改代码时最容易踩的」约定。
> 完整规范以 [`CONTRIBUTING.md`](CONTRIBUTING.md) 为准（面向人类贡献者）；如有冲突，以 `CONTRIBUTING.md` 与源码注释为准。

## 构建

```bash
dotnet build KernelExtensions.csproj --no-restore
```

**必须保持 0 错 0 警。** 项目开启了 `GenerateDocumentationFile`，XML 文档注释里的 `<` / `>` 要写成 `&lt;` / `&gt;`。

## 提交信息

约定式：`类型(作用域): 描述`，描述可用中文。

- 类型：`feat` / `fix` / `refactor`（不改行为）/ `docs` / `style` / `test` / `chore` / `build` / `ci` / `perf` / `revert`
- 作用域用模块名：`aircraft` / `config` / `storage` / `utils` / `patches` / `examples` / `naming` / `log` / `meta` 等

**本仓库会公开发布** —— 提交信息、源码注释与示例文件里都**不要出现内部计划编号**。

## 日志（KELog）

| 级别 | 用途 |
|------|------|
| `Debug` | 源码开发者排错（**默认关**，数据 / 机制级细节） |
| `Info` | 扩展作者排错（动作级结果：Clock 启停、预设加载、播放成功 —— **一次触发一条**） |
| `Warn` | 可恢复 / 降级 / 需要注意 |
| `Error` | 不该发生 / 功能失败 |

`os.write` **只**用于给玩家终端反馈（错误、互斥提示）；成功状态走 `KELog.Info`。

## 编写 Action

- **一个 Action 干一件事**；多模式必须拆（例：`ConnectControl` → `LinkControl` 三件套）
- **优先继承 `KEAction`** —— 自动获得 `Delay` / `DelayHost`，以及 XML 属性名大小写容错
- XML 属性名用 **PascalCase**，并避开 XNA 类型名（用 `AccentColor` 而不是 `Color`）
- **不要手写 `LoadFromXml` 读属性** —— 会造成两类静默失效：忘调 `base` 让 `Delay` 失效、硬编码属性名读不到。属性用 `[XMLStorage]` 字段，交给基类解析
- 需要持续多帧绘制时，**每帧重新挂** `os.postFXDrawActions`（那是一次性委托，OS 调用后即为 null）
- **目录分类**：`Actions/` 根目录只留 `KEAction.cs`；系统级各占一个文件夹（`Aircraft/` `PhaseSwift/` `CustomTrial/` `VMAttack/` `Clock/` `Ending/` `Node/`），其余归 `Misc/`。**非 Action 的辅助类不要放进 `Actions/`**

## 编写动作点（配置里挂 Action 的字段）

⚠️ **现状各系统写法不一致，新增动作点一律按下面的目标形态来**（存量迁移见 plans 的「动作点统一」立项）。

- **一律用 `Configs/ActionHook` 类型**，不要新增 `string` 或 `ActionFileRef` 形态的动作点字段
- `ActionHook` 同时支持三种写法：**元素文本内容**（路径）/ **`file` 属性**（旧写法兼容）/ **内联元素**（`<Actions>` / `<ConditionalActions>`）；执行顺序是**先文件、后内联**
- 调用点写 `hook.Execute(os, source, point)`，不要自己调 `ActionHelper`
- **属性 vs 元素不是风格选择**：属性适合紧凑记录 / 键值对，元素适合内容 / 容器。
  **动作点天生是容器**（要能塞 `<Actions>`），所以**不能用属性形态** —— 属性在语法上无法承载子元素
- 解析失败**必须点名到具体元素**：`ActionsLoader.ReadAction` 对未注册名抛 `KeyNotFoundException`、
  `LoadActionSets` 对未知条件抛 `FormatException`，都要接住并报出元素名 + 提示
  （不要让异常冒到 `OS.Update` —— 那里会被吞掉、连日志都不留）
- **不要做"名字是否合法"的预过滤**：`ActionManager.RegisterAction<T>(name)` 接受**任意字符串**，
  且 Pathfinder 没有公开查询注册表的接口 —— 任何本地白名单都会误伤第三方动作

## 配置约定

- **配置项变动 → `XMLExamples/` 同批更新（硬约定）**：新增 / 改名 / 删除任何 XML 配置元素或属性时，必须同步更新对应的 `XMLExamples/*_Example.xml`（必要时连 `XMLExamples/README.md`）与文档站对应页面。示例是作者抄写的第一入口，不允许留到以后补。

- **NONE 约定**：字符串配置项写 `NONE`（大小写不敏感）或留空 = 显式禁用；不写该属性 = 使用默认值。判断统一走 `Utilities/ConfigValue.IsNone()`
- **数值约定**：数值配置项**负数 = 使用默认 / 继承 / 无限**（`NaN` / `Infinity` 同样回退默认）。判断写 `< 0` 加有限性检查，**不要写 `== -1`** —— 会漏掉 `-2` 与 `NaN`
- **数值上限约定（不要随手加）**：数值配置项**默认不限上限**。`ConfigValue.ParseInRange` 的 `max` 只在两类情况下给真实值：① **语义有界**（如百分比 0~100、比例 0~1）；② **后续要转 `int`**（超大 `float` 转 `int` 会溢出成 `int.MinValue`）。其余一律传 `float.MaxValue`
    - ⚠️ 超范围值会被**回退成默认值**（不是夹到上限），所以一个随手拍的上限 = 作者写了配置却「行为不符」，且很难排查 —— 本项目曾给 `<SelfOverloadRate>` 定过上限 1，作者写 10 被静默换成 1，就是这个问题
    - 数值平衡是**扩展作者的事**，解析层只管「是不是一个能用的数」
- **扩展内路径**：作者书写的路径（`HelpFile` / `SystemLogFiles` / `FakeFiles[].Source` / `CheckFilePath` / `CheckFilePattern` 等）一律**相对于扩展根目录**，且**大小写必须与磁盘上的实际文件名一致**（Linux 文件系统区分大小写，Windows 上写错也能跑但同一内容到 Linux 会失效）。路径**不得逃出扩展目录**：不要新增裸的 `Path.Combine(扩展根, 作者字符串)`，越界校验计划统一走 `Utilities/KEPath.ResolveInsideExtension()`（`GetFullPath` 规范化后比对扩展根前缀）

## Harmony 补丁

- 编译期可见的类型 → 用 `[HarmonyPatch]` 特性，由主入口 `_harmony.PatchAll()` 统一管理
- `internal` 类型（如原版 `PortHackExe`）或**条件安装**（如检测到其他模组才装）→ 手动 `harmony.Patch`，集中在主入口统一调用
- 卸载时必须干净移除，**包括副作用**（纹理、音量、订阅等），避免残留

## 依赖与许可证

仓库根 `NOTICE.md` 记录依赖与许可证，区分两类：

- **随分发** —— 被 Costura 织入 dll（如 `NVorbis`、`Costura`）
- **运行时依赖** —— `Private=false`，不随分发（Pathfinder / Harmony / FNA / BepInEx 等）

新增会被织入的 NuGet 依赖时，**同步更新 `NOTICE.md`**。

## 平台支持

Pathfinder 只支持 **Windows / Linux**，**不支持 macOS** —— 平台分支不必考虑 macOS。
