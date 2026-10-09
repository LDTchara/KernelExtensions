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

## 配置约定

- **NONE 约定**：字符串配置项写 `NONE`（大小写不敏感）或留空 = 显式禁用；不写该属性 = 使用默认值。判断统一走 `Utilities/ConfigValue.IsNone()`
- **数值约定**：数值配置项**负数 = 使用默认 / 继承 / 无限**（`NaN` / `Infinity` 同样回退默认）。判断写 `< 0` 加有限性检查，**不要写 `== -1`** —— 会漏掉 `-2` 与 `NaN`
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
