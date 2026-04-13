# KernelExtensions

> **快速导航**： [中文版本](#中文版本) | [English Version](#english-version)

---

## 前置条件 / Prerequisites

在开始使用本模组前，请确保已安装：
- Hacknet 的 DLC **Labyrinths**
- **Pathfinder** 框架（5.3.2 或更高版本）

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
  - 超时或失败后重置当前阶段（可扩展为执行自定义动作）
  - 可选特效：UI 闪烁（含节点随机消失）、邮件图标爆炸
  - 屏幕正上方独立倒计时条
  - 多语言按钮（根据游戏语言自动切换）

#### 🚧 计划中
- **可配置数量的试炼程序**（通过不同 XML 文件实现多个独立试炼）
- **可自定义的 Porthack 心脏 Daemon**
  - 包括 Porthack 后执行的 Action 配置
- **DLC 中可自定义的飞机 Daemon**（`AircraftDaemon` 扩展）
  - 允许任务制作者配置飞机的飞行路径、事件触发条件等
- 失败/完成时执行外部 Action 文件（需要 Pathfinder 提供相关 API 或自行实现）

> ⚠️ 当前版本仅实现了自定义试炼程序的核心逻辑，且未经过完整测试。Porthack 心脏和飞机 Daemon 尚在计划阶段。

---

## English Version

**KernelExtensions** is a Hacknet mod using the **Pathfinder API**, designed to extend hardcoded mechanics in the game.

### Feature List

#### ✅ Implemented (Experimental)
- **Custom Trial Program** (`CustomTrialExe`)
  - Multi-phase trials based on XML configuration
  - Customizable SpinUp duration, start music, trial music
  - Per-phase configuration: title, subtitle, description text (file or inline), mission file, timeout, phase music
  - Character-by-character printing of mission description
  - Timeout or failure resets current phase (extendable to custom actions)
  - Optional effects: UI flickering (with random node disappearance), mail icon explosion
  - Independent countdown bar at the top of the screen
  - Multi-language button (auto-detects game language)

#### 🚧 Planned
- **Configurable number of trial programs** (multiple independent trials via different XML files)
- **Customizable Porthack heart daemon**
  - Including actions to execute after Porthacking
- **Customizable Aircraft Daemon from DLC** (extension of `AircraftDaemon`)
  - Allow mission authors to configure flight paths, event triggers, etc.
- Execute external action files on failure/completion (requires proper Pathfinder API or custom implementation)

> ⚠️ Only the custom trial program core logic is implemented in the current version, and it is not fully tested. Porthack heart and aircraft daemon are still in the planning stage.

---

## 备注 / Notes

- 这是我的第一个C#项目和github仓库，会有相当多的缺点，欢迎指出不足之处，我会尽力改进
- 欢迎后续贡献代码或提出建议。
- 代码由 AI 辅助生成，可能包含错误或不符合最佳实践，请自行测试后使用。
