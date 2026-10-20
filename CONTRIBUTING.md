# Contributing Guide / 贡献指南

Thanks for your interest in contributing to **KernelExtensions**! This guide collects the project's real engineering conventions so you can write code that fits in. When in doubt, follow the existing code and this guide — the maintainer keeps the authoritative convention reference internally.

欢迎为 **KernelExtensions** 贡献代码！本文汇总了项目真实的工程约定，请按此提交代码。如有疑问，以现有代码与本文为准——维护者内部另有权威约定记录。

---

## Table of Contents / 目录

- [English](#english)
  - [1. Project Layout](#1-project-layout)
  - [2. Branch & PR Workflow](#2-branch--pr-workflow)
  - [3. Build](#3-build)
  - [4. Code Conventions](#4-code-conventions)
  - [5. Commit Messages](#5-commit-messages)
  - [6. Git Discipline](#6-git-discipline)
  - [7. Documentation](#7-documentation)
  - [8. Review Checklist](#8-review-checklist)
- [中文](#中文)
  - [1. 项目结构](#1-项目结构)
  - [2. 分支与 PR 流程](#2-分支与-pr-流程)
  - [3. 编译](#3-编译)
  - [4. 代码规范](#4-代码规范)
  - [5. 提交信息](#5-提交信息)
  - [6. Git 纪律](#6-git-纪律)
  - [7. 文档同步](#7-文档同步)
  - [8. 审查清单](#8-审查清单)

---

# English

## 1. Project Layout

```
KernelExtensions/
├── Actions/          # Pathfinder Actions (subfolders like Title/)
├── Configs/          # Config parsing (UsernameProfiles, etc.)
├── Daemons/          # Custom daemons (FlightDaemon, CustomHeartDaemon)
├── Executables/      # Custom executables (CustomTrialExe, PhaseSwiftExe)
├── Managers/         # Global services (ClockManager, PhaseSwiftManager, CustomColorManager)
├── Modules/          # UI / render modules (CustomEndingModule, etc.)
├── Patches/          # Harmony patches
├── Saving/           # Save-load bridge: SaveLoader executors that parse save-XML sections into persistent-state DTOs (PendingRestore) for OSLoaded restore
├── Storage/          # Cross-session data layer: global in-memory state (destroyed-trial node indices, node icons) bridged to save XML via save/load events
├── Utilities/        # Helpers (KELog, ConfigValue, MusicPathResolver, etc.)
└── KernelExtensions.cs  # Main entry (registration + PatchAll + events)
```

All source is UTF-8. The `csproj` enables `<ImplicitUsings>`, so `System`/`System.Linq`/`System.Collections.Generic` are globally available.

## 2. Branch & PR Workflow

- **`main`** — stable release branch. Merged via PR.
- **`dev`** — development trunk. Feature work happens on `dev_feature_*` branches and is merged into `dev` first; `main` receives merges from `dev`.
- **`dev_feature_*`** — feature branches (e.g. `dev_feature_LDT`).

For external contributions: fork → create a feature branch → open a PR against `dev`. Keep the change focused and atomic: one PR = one feature/fix.

## 3. Build

```powershell
dotnet build KernelExtensions.csproj --no-restore
```

Must finish with **0 errors and 0 warnings**. `GenerateDocumentationFile` is on, so `<` and `>` in XML doc comments must be escaped as `&lt;` / `&gt;`.

## 4. Code Conventions

- **Follow existing style.** Match the surrounding code: file-scoped namespaces, expression bodies where the neighbors use them, etc.
- **ImplicitUsings**: don't add redundant `using` directives or fully-qualified names (unless needed for disambiguation). The maintainer cleans up legacy code; new/modified code must follow this from day one.
- **KELog levels** (authoritative: `Utilities/KELog.cs`):
  - `Debug` — KE source debugging (off by default; data/mechanism-level detail)
  - `Info` — extension-author diagnostics (action-level results: one line per trigger)
  - `Warn` — recoverable/degraded/notice
  - `Error` — should never happen / feature failure
  - `os.write` is **only** for in-game player feedback (errors/mutex notices); success-state logging goes through `KELog.Info`.
- **NONE convention**: string config values use `NONE` (case-insensitive) or empty to mean *disabled*; omitting the attribute means *default*. Always test through `Utilities/ConfigValue.IsNone()`.
- **Patch registration**:
  - Types visible at compile time → `[HarmonyPatch]` attribute (picked up by the main entry's `_harmony.PatchAll()`).
  - `internal` types (e.g. vanilla `PortHackExe`) or conditional installs (e.g. the Stuxnet plugin) → manual `harmony.Patch`, wired centrally in the main entry (see `PorthackAutoPatch`).
- **Refactoring**: class/file/namespace names may change, but **XML registration strings are frozen** (`RegisterAction`/`RegisterExecutable`/`RegisterDaemon` arguments). Existing extensions must not break.
- **Dynamic colors** go through `Managers/CustomColorManager.GetDynamicColor` — never route through PhaseSwift for color.
- **Action conventions**:
  - **One action, one job** — when a parameter would switch between fundamentally different operations (e.g. `add` / `remove` / `reset`), split them into separate actions (like `ClockStart` / `ClockStop`). A parameter that only changes *appearance* (e.g. `Preset="info|warning"`) is fine.
  - Prefer `DelayablePathfinderAction` over `PathfinderAction`, so extension authors get `Delay` / `DelayHost`.
  - **XML attribute names are PascalCase for consistency** — through the `KEAction` base class they are **case-insensitive** at parse time (both `Delay` and `delay` resolve), but always write PascalCase; giving the same attribute twice in different casings has unpredictable precedence. Avoid field names that collide with XNA types (use `AccentColor`, not `Color`).
  - **Never hand-write `LoadFromXml` to read attributes** — always use an `[XMLStorage]` field. Hand-written loading has produced two bugs:
    ① forgetting `base.LoadFromXml(info)` → `Delay`/`DelayHost` silently stop working (`TerminalFocus`);
    ② hard-coding an attribute name (e.g. `GetString("text", …)`) → authors writing `Text=` get silent no-ops (`TerminalWrite`).
  - **Multi-frame drawing must re-register `os.postFXDrawActions` every frame** — it is a **one-shot** delegate: the OS invokes it and immediately sets it to `null` (`OS.cs:1257`). Registering only once in `Start()` draws a single frame (effectively invisible). See `CustomHeartDaemon` / `ScreenBleedWCCManager` for the correct pattern.
- **Action points** (the config fields that attach Actions, e.g. `OnStart` / `OnComplete` / `OnFail` / `OnSwitch`):
  - ⚠️ **The existing systems are inconsistent** (some use a `file` attribute, some use element text content, some `string` fields). **New action points must use the `Configs/ActionHook` type** — see the target shape below.
  - `ActionHook` accepts all three forms: **element text content** (a path), the legacy **`file` attribute**, and **inline elements** (`<Actions>` / `<ConditionalActions>`). When both a path and inline content are present, the **file runs first, then the inline part**.
  - Call sites use `hook.Execute(os, source, point)` — do not call `ActionHelper` directly.
  - **Attributes vs elements is not a style choice.** Attributes suit compact records / key-value pairs; elements suit content, containers and nesting. **An action point is inherently a container** (it must be able to hold `<Actions>`), so **it cannot use the attribute form** — XML attributes cannot carry child elements at all.
  - **Parse failures must name the offending element.** `ActionsLoader.ReadAction` throws `KeyNotFoundException` for unregistered names and `LoadActionSets` throws `FormatException` for unknown conditions; catch both and report the element name plus a hint. Never let these escape into `OS.Update` — they are swallowed there and leave no log at all.
  - **Never pre-filter by "is this name valid".** `ActionManager.RegisterAction<T>(name)` accepts **arbitrary strings**, and Pathfinder exposes no public way to query the registry — any local allow-list will silently skip legitimate third-party actions.

## 5. Commit Messages

Conventional Commits format: `type(scope): description` — description may be Chinese.

- Types: `feat` / `fix` / `refactor` (no behavior change) / `docs` / `style` / `test` / `chore` / `build` / `ci` / `perf` / `revert`.
- Scope = module name, written in **full** (`customcolor`, not `color`): e.g. `aircraft`, `config`, `storage`, `utils`, `patches`, `examples`, `naming`, `log`, `wiki`, `meta`, `title`, `ending`, `username`, `stuxnet`, `res`, `localization`.
- Do **not** reference internal plan/ticket IDs in commit messages. If you keep your own internal plan (e.g. a numbered feature list), leave those identifiers out of public commits.

Examples:
```
feat(clock): add Clock timer action
fix(title): TitleBanner draw failure - text sanitising + guaranteed SpriteBatch End
refactor(config): Config renamed to Configs (plural)
```

## 6. Git Discipline

- **Never `git add -A`.** Add exact files/paths only; `git add -u` is risky (it has swept unrelated files into commits before).
- Keep the working tree clean before switching branches.
- Writes to source files should preserve UTF-8 encoding; on Windows prefer editing via tools that write UTF-8 explicitly.

## 7. Documentation

- User-facing features need Wiki pages (Chinese + English, see the `KEwiki/` side repo) and, where applicable, XML doc comments with usage examples in the code.
- The wiki tracks the stable `main` branch; it is not pushed until `dev` is merged to `main`.
- Keep private planning notes out of public commits and docs — for example, if you maintain an internal numbered plan, do not mention your internal IDs in commit messages or Wiki pages.

## 8. Review Checklist

Before opening a PR, verify:

- [ ] Builds with 0 errors / 0 warnings
- [ ] New/modified code follows ImplicitUsings (no redundant `using`)
- [ ] XML registration strings unchanged (unless intentional and documented)
- [ ] String config options honor the NONE convention via `ConfigValue.IsNone()`
- [ ] Logging uses the right KELog level; `os.write` only for player-facing feedback
- [ ] New public API has XML doc comments with usage examples
- [ ] Feature works on a real game instance (test extension), not just compilation
- [ ] Wiki/Chinese-English docs updated for user-facing changes
- [ ] Commit messages follow the conventional format, no internal IDs

---

# 中文

## 1. 项目结构

```
KernelExtensions/
├── Actions/          # Pathfinder Action（含 Title/ 等子目录）
├── Configs/          # 配置解析（UsernameProfiles 等）
├── Daemons/          # 自定义 Daemon（FlightDaemon、CustomHeartDaemon）
├── Executables/      # 自定义可执行程序（CustomTrialExe、PhaseSwiftExe）
├── Managers/         # 全局服务（ClockManager、PhaseSwiftManager、CustomColorManager）
├── Modules/          # 界面/渲染模块（CustomEndingModule 等）
├── Patches/          # Harmony 补丁
├── Saving/           # 存档读写桥：SaveLoader 注册的 Executor 解析存档 XML 各段为持久化状态 DTO（PendingRestore），供 OSLoaded 后恢复（试炼删节点/PS 场景/运行中 Clock）
├── Storage/          # 跨会话数据层：全局内存态（试炼被摧毁节点索引、节点图标）经存档事件桥接到存档 XML
├── Utilities/        # 工具类（KELog、ConfigValue、MusicPathResolver 等）
└── KernelExtensions.cs  # 主入口（注册 + PatchAll + 事件）
```

所有源码 UTF-8。`csproj` 已开启 `<ImplicitUsings>`，`System`/`System.Linq`/`System.Collections.Generic` 全局可用。

## 2. 分支与 PR 流程

- **`main`**：稳定发布分支，走 PR 合并。
- **`dev`**：开发主线。功能在 `dev_feature_*` 分支开发，先并入 `dev`，`main` 再收 `dev` 的合并。
- **`dev_feature_*`**：功能分支（如 `dev_feature_LDT`）。

外部贡献：fork → 建功能分支 → 向 `dev` 提 PR。改动保持聚焦且原子：一个 PR = 一个功能/修复。

## 3. 编译

```powershell
dotnet build KernelExtensions.csproj --no-restore
```

必须 **0 错 0 警**。项目开启 `GenerateDocumentationFile`，XML 注释里的 `<`/`>` 要转义为 `&lt;`/`&gt;`。

## 4. 代码规范

- **跟随现有风格**：与周围代码保持一致（文件级命名空间、表达式体等）。
- **ImplicitUsings**：不写多余的 `using`、不用完全限定名（除非消歧）。存量代码由维护者清理；新增/修改的代码必须从一开始就遵守。
- **KELog 级别**（权威：`Utilities/KELog.cs`）：
  - `Debug` — KE 源码开发者排错（默认关，数据/机制级细节）
  - `Info` — 扩展作者排错（动作级结果，一次触发一条）
  - `Warn` — 可恢复/降级/注意
  - `Error` — 不该发生/功能失败
  - `os.write` **仅**用于玩家终端反馈（错误/互斥提示）；成功状态日志走 `KELog.Info`
- **NONE 约定**：字符串配置项写 `NONE`（大小写不敏感）/留空 = 禁用；不写属性 = 默认值。判断统一走 `Utilities/ConfigValue.IsNone()`。
- **patch 注册格式**：
  - 编译期可见类型 → `[HarmonyPatch]` 特性（主入口 `_harmony.PatchAll()` 统一管理）
  - internal 类型（如原版 `PortHackExe`）或条件安装（如 Stuxnet 插件存在才装）→ 手动 `harmony.Patch`，集中在主入口统一调用（参照 `PorthackAutoPatch`）
- **重构原则**：类名/文件名/命名空间可改，但 **XML 注册字符串冻结**（`RegisterAction`/`RegisterExecutable`/`RegisterDaemon` 的字符串参数），已有扩展零破坏。
- **动态颜色**统一走 `Managers/CustomColorManager.GetDynamicColor`——不要借道 PhaseSwift 取色。
- **Action 编写约定**：
  - **一个 Action 干一件事**——若某个参数会在**本质不同的操作**间切换（如 `add` / `remove` / `reset`），请拆分为多个 Action（参照 `ClockStart` / `ClockStop`）；只改变**外观**的参数（如 `Preset="info|warning"`）无需拆分。
  - 优先继承 `DelayablePathfinderAction`（而非 `PathfinderAction`），以便扩展作者获得 `Delay` / `DelayHost`。
  - **XML 属性名统一用 PascalCase**——经 `KEAction` 基类处理后解析期**大小写不敏感**（`Delay` 与 `delay` 都能命中），但仍请一律写 PascalCase；同一元素里两种大小写混写时哪个生效不可预测。字段名避免与 XNA 类型同名（用 `AccentColor`，不要用 `Color`）。
  - **不要手写 `LoadFromXml` 去读属性**——一律用 `[XMLStorage]` 字段。手写会出现两类 bug：
    ① 忘记调 `base.LoadFromXml(info)` → `Delay`/`DelayHost` 静默失效（`TerminalFocus` 曾如此）；
    ② 硬编码属性名（如 `GetString("text", …)`）→ 作者写 `Text=` 时静默无输出（`TerminalWrite` 曾如此）。
  - **持续多帧绘制必须每帧重挂 `os.postFXDrawActions`**——它是**一次性**委托：OS 调用后立即置 `null`（`OS.cs:1257`）。只在 `Start()` 挂一次只会绘制一帧（肉眼几乎不可见）。参考 `CustomHeartDaemon` / `ScreenBleedWCCManager` 的写法。
- **动作点**（配置里挂 Action 的字段，如 `OnStart` / `OnComplete` / `OnFail` / `OnSwitch`）：
  - ⚠️ **存量各系统写法不一致**（有的用 `file` 属性、有的用元素文本内容、有的是 `string` 字段）。**新增动作点一律用 `Configs/ActionHook` 类型**，按下面的目标形态来。
  - `ActionHook` 同时支持三种写法：**元素文本内容**（路径）、兼容旧写法的 **`file` 属性**、**内联元素**（`<Actions>` / `<ConditionalActions>`）。同时给了路径与内联时，**先执行文件、再执行内联**。
  - 调用点写 `hook.Execute(os, source, point)`——不要自己调 `ActionHelper`。
  - **属性 vs 元素不是风格选择**：属性适合紧凑记录 / 键值对，元素适合内容 / 容器 / 嵌套。**动作点天生是容器**（必须能塞 `<Actions>`），所以**不能用属性形态**——XML 属性在语法上无法承载子元素。
  - **解析失败必须点名到具体元素**：`ActionsLoader.ReadAction` 对未注册名抛 `KeyNotFoundException`、`LoadActionSets` 对未知条件抛 `FormatException`，两者都要接住并报出元素名 + 提示。**绝不能让它们冒到 `OS.Update`**——那里会被吞掉，连日志都不留。
  - **不要做"名字是否合法"的预过滤**：`ActionManager.RegisterAction<T>(name)` 接受**任意字符串**，且 Pathfinder 没有查询注册表的公开接口——任何本地白名单都会静默跳过合法的第三方动作。

## 5. 提交信息

约定式格式：`类型(作用域): 描述`，描述可中文。

- 类型：`feat` / `fix` / `refactor`（不改行为）/ `docs` / `style` / `test` / `chore` / `build` / `ci` / `perf` / `revert`
- 作用域 = 模块名，**写全称**（`customcolor` 不写 `color`）：如 `aircraft`、`config`、`storage`、`utils`、`patches`、`examples`、`naming`、`log`、`wiki`、`meta`、`title`、`ending`、`username`、`stuxnet`、`res`、`localization`
- 不得在提交信息中引用内部计划/工单编号。如果你有自己的内部计划（例如编号功能列表），请让这些标识符远离公开提交。

示例：
```
feat(clock): 新增 Clock 定时器
fix(title): TitleBanner 绘制异常——文本清洗 + try/finally 保证 SpriteBatch End
refactor(config): Config→Configs 全复数
```

## 6. Git 纪律

- **禁止 `git add -A`**。只用精确文件/路径；`git add -u` 有风险（曾误卷无关文件进提交）。
- 切分支前保持工作区干净。
- 写文件保持 UTF-8 编码；Windows 下优先用显式 UTF-8 写入的工具编辑。

## 7. 文档同步

- 面向用户的功能需要 Wiki 页面（中英双语，见 `KEwiki/` 侧仓库），并在代码里补带用法示例的 XML 注释。
- Wiki 与稳定版 `main` 对齐，`dev` 合并到 `main` 前不推送。
- 内部计划笔记不要进入公开提交与文档——例如你用内部编号体系管理功能计划，不要在提交信息或 Wiki 中提及你的内部编号。

## 8. 审查清单

提 PR 前逐项核对：

- [ ] 编译 0 错 0 警
- [ ] 新增/修改代码遵守 ImplicitUsings（无多余 using）
- [ ] XML 注册字符串未变（有意变更需说明）
- [ ] 字符串配置项经 `ConfigValue.IsNone()` 遵守 NONE 约定
- [ ] 日志级别正确；`os.write` 仅玩家反馈
- [ ] 新公开 API 有带用法示例的 XML 注释
- [ ] 功能在真实游戏实例（测试扩展）中验证，而非仅编译通过
- [ ] 面向用户的功能已同步中英文 Wiki
- [ ] 提交信息符合约定式格式，无内部编号
