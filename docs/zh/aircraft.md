# 飞机 Daemon 系统

**飞机 Daemon** 是对原版 `AircraftDaemon` 的完全替代品，支持自定义坠落总时长、在修复或坠毁时触发动作，并提供全局高度计覆盖层以便于剧情设计。  
本系统由 **April_Crystal** 贡献核心实现，特此感谢。

---

## 概述

- 守护进程名称：`FlightDaemon`
- 注册方法：在目标计算机 XML 中直接添加 `<FlightDaemon .../>`
- 主要增强：
  - 可配置的坠落时长（`FallDuration`，单位秒，默认 135）
  - 坠毁/修复时分别触发 `OnFailed` / `OnSaved` 动作
  - 专用的攻击、修复与覆盖层动作
  - 自动清理静态字典，避免内存泄漏

---

## 基本配置

在计算机配置文件中直接声明守护进程：

```xml
<Computer id="dair_crash" ... >
  ...
  <FlightDaemon FallDuration="90" OnFailed="Actions/plane_failed.xml" OnSaved="Actions/plane_saved.xml" />
  ...
</Computer>
```

### FlightDaemon 可配置属性

| 属性 | 默认值 | 描述 |
|------|--------|------|
| `FallDuration` | `135` | 从 38000 英尺坠毁至地面的总秒数（立即坠落模式下）。 |
| `OnFailed` | `null` | 飞机坠毁（高度降至 0）时执行的动作文件。 |
| `OnSaved` | `null` | 飞机被修复（固件重载成功且 DLL 恢复）时执行的动作文件。 |

> 注意：`FallDuration` 只在立即坠落模式（`AircraftFallStartsImmediately = true`）下生效，该模式默认开启。守护进程初始化时会用 `FallDuration` 设置运行时变量 `H`。

---

## 相关动作

### 攻击飞机：`AttackAircraft`

触发目标计算机上的 `FlightDaemon` 进入关键固件故障，并开始坠落。

```xml
<AttackAircraft NodeID="dair_crash" FallDuration="60" />
```

- `NodeID`：目标计算机的 `idName`（必须已配置 `FlightDaemon`）。
- `FallDuration`（可选）：指定坠落总秒数，**优先级高于** Daemon 配置中的 `FallDuration`。  
  特殊值：  
  - `-1`（或省略）：使用 Daemon 当前的坠落时长（由自身配置决定）  
  - `0`：立即坠毁（跳过下降过程，直接触发 `OnFailed` 和节点移除）  
  - 正数：覆盖 Daemon 的 `FallDuration`。

> 攻击流程：生成固件 DLL 并立即删除 → 6 秒后固件重载失败 → 进入关键故障 → 飞机按设定时长坠落。

### 修复飞机：`UploadAircraftSysFile`

向目标计算机的 `FlightSystems` 文件夹写入合法的 `747FlightOps.dll`。

```xml
<UploadAircraftSysFile NodeID="dair_crash" />
```

- 如果目标计算机已运行 `FlightDaemon`，则直接写入 `FlightSystems/747FlightOps.dll`。
- 否则，按 `Path` 属性指定的路径写入文件。
- 玩家仍需连接该计算机，手动点击 **Reload Firmware** 按钮完成修复（触发 `OnSaved`）。

### 全局高度计覆盖层

可在不连接目标计算机的情况下，在屏幕左侧永久显示飞机的高度计。

```xml
<!-- 显示覆盖层 -->
<ShowAircraftOverlay NodeID="dair_crash" />

<!-- 关闭覆盖层 -->
<HideAircraftOverlay />
```

- 坠毁时会自动关闭覆盖层（如果正在显示该飞机）。
- 覆盖层通过 `OverlayPatches`（Harmony 补丁）实现，不影响正常游戏流程。

---

## 界面与交互

连接至搭载 `FlightDaemon` 的计算机后，会显示航班仪表界面：

- **Disconnect** 按钮：断开与当前飞机的连接（不再显示仪表）。
- **Pilot Alert** 按钮：向飞行员发送警报（设置 `PilotAlerted` 标记，改变界面颜色）。
- **Reload Firmware** 按钮：启动固件重载过程。若 `FlightSystems` 中存在合法的 `747FlightOps.dll`，6 秒后故障解除并触发 `OnSaved`。

---

## 坠落逻辑说明

- **立即坠落模式**（默认）：高度按 `FallDuration`（或动作覆盖的 `H`）线性递减：  
  `CurrentAltitude = 38000 * (1 - timeFallingFor / H)`  
  下降速率固定为 `-876.9231` 英尺/秒，但高度由公式直接计算，确保总时长等于 `H` 秒。

- **非立即坠落模式**（`AircraftFallStartsImmediately = false`）：前 15 秒通过二次缓出加速至最大速率，之后匀速下降。此模式下 `FallDuration` 不控制总时间。

> 通常只需关注立即坠落模式，其坠毁时长完全由 `FallDuration` 或动作指定的值决定。非立即坠落模式暂未投入使用。

---

## 多语言支持

按钮文字（"Disconnect", "Pilot Alert", "Reload Firmware" 等）使用游戏内置的本地化术语，会自动切换语言（中文、英语等）。

---

## 另请参阅

- [首页](./index.md) – 返回主索引
- [Aircraft-Daemon-System (English)](./../en/aircraft.md) – 英文版
- [杂项 (Misc)](./misc.md) – 其他未在各大系统页面提到的东西
- [自定义Action](./actions.md) – 全部自定义动作列表

---

> 特别感谢 **April_Crystal** 为本系统提供核心实现与宝贵建议。