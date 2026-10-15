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
  - 更新订阅按需退订（`UnsubscribeIfIdle`），无后台累积

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

!!! warning "`FallDuration` 不在下表 —— 它是动作的属性，不是这里的配置项"
    `FlightDaemon` 类里确实有这个字段，但**它是全表唯一没有 `[XMLStorage]` 的**，所以写进 `<FlightDaemon>` 的 XML **不会生效**。坠落时长请用动作的 `FallDuration`（见[相关动作](#相关动作)）。

| 属性 | 默认值 | 描述 |
|------|--------|------|
| `OnFailed` | `null` | 飞机坠毁（高度降至 0）时执行的动作文件。 |
| `OnSaved` | `null` | 飞机被修复（固件重载成功且 DLL 恢复）时执行的动作文件。 |
| `CrashIPPrefix` | `DCLOC:` | 坠机后加在节点 IP 前的前缀，使旧 IP 不可达；空 / `NONE` = 不修改 IP（节点仍可访问）。 |
| `MapOriginX` / `MapOriginY` | `0.4304` / `0.8339` | 高度计覆盖层里**航线起点**的归一化坐标（0~1，相对世界地图矩形）。 |
| `MapDestX` / `MapDestY` | `0.6672` / `0.4264` | 航线**终点**的归一化坐标。 |

!!! note "航线图起终点"
    叠加层里的世界地图用的是原版贴图。坐标是**归一化**的（0~1，相对地图矩形），
    因此与贴图尺寸无关；但换贴图、或想让起终点落在别的位置时就需要改这四项。
    取值会被 clamp 到 `[0, 1]`（超出只会画出图外，没有创作用途）。

> 注意：`FallDuration` **不是能在这个配置文件里改的东西** —— 它是下面动作的属性（见[相关动作](#相关动作)）。`FlightDaemon` 自身确实有这个字段，但**本类没有为它加 `[XMLStorage]`**，所以 XML 里写它不会生效；坠落时长请用动作的 `FallDuration`。

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
  - `-1`：使用 Daemon 自身配置的 `FallDuration`  
  - **省略**：保持动作默认值 **135 秒**（**不是**沿用 Daemon 配置 —— 想沿用请显式写 `FallDuration="-1"`）  
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

可在不连接目标计算机的情况下，在屏幕右侧永久显示飞机的高度计。

```xml
<!-- 显示覆盖层 -->
<ShowAircraftOverlay NodeID="dair_crash" />

<!-- 关闭覆盖层 -->
<HideAircraftOverlay />
```

!!! warning "覆盖层不会自动关闭"
    飞机坠毁后覆盖层会**继续显示残骸**，必须自己调 `<HideAircraftOverlay />` —— 它**不会**随坠毁、
    断开连接或剧情推进自动消失。忘了关，屏幕右侧会一直挂着高度计。

    常见情形：只写了 `<ShowAircraftOverlay />` 却没写关闭。测试时不容易发现，
    等玩家推完剧情回来才看到覆盖层还在。

- 覆盖层通过 `OverlayPatch`（Harmony 补丁）实现，不影响正常游戏流程。

---

## 界面与交互

连接至搭载 `FlightDaemon` 的计算机后，会显示航班仪表界面：

- **Disconnect** 按钮：断开与当前飞机的连接（不再显示仪表）。
- **Pilot Alert** 按钮：向飞行员发送警报（界面颜色随之改变），并在**玩家账号**上写一个存档 flag：
  **`PilotAlerted_<节点 idName>`**（点击后立即存档）。
  ⚠️ **剧情条件里要用这个名字**：它是**带节点名后缀的**，不是裸的 `PilotAlerted`——写 `HasFlags` / `<HasFlag Name="...">` 时若漏掉 `_<idName>` 后缀**永远不成立**。
  同一个节点只写一次（已存在则跳过，不重复写盘）。
- **Reload Firmware** 按钮：启动固件重载过程。若 `FlightSystems` 中存在合法的 `747FlightOps.dll`，6 秒后故障解除并触发 `OnSaved`。

---

### 调试专用动作 `FlightDaemonStatus` / `FlightDaemonMap`

!!! warning "仅用于调试"
    这两个动作**只在 `KE-Config.xml` 的 `<Debug>true</Debug>` 时生效**，
    正式内容里写了也只会收到一句提示、不做任何事——它们不是面向扩展作者的 API。

- **`<FlightDaemonStatus />`** —— 把所有飞机 daemon 的运行时状态输出到玩家终端
  （高度 / 空速 / 航线进度 / 是否坠毁 / 是否已救 / 当前航线坐标等），
  **不连接目标节点也能看**。可选 `NodeId="xxx"` 只列一个（匹配 `idName` 或 `ip`）。
- **`<FlightDaemonMap OriginX="…" OriginY="…" DestX="…" DestY="…" />`** ——
  随时改航线起终点现场调参（不写的项保持不变）。改动只在**本次运行**内有效，
  下次读档会被节点 XML 的配置覆盖——调好的值请写回上面那四个属性。

## 坠落逻辑说明

- **立即坠落模式**（默认）：高度按 `FallDuration`（或动作覆盖的 `H`）线性递减：  
  `CurrentAltitude = 38000 * (1 - timeFallingFor / H)`  
  下降速率固定为 `-876.9231` 英尺/秒，但高度由公式直接计算，确保总时长等于 `H` 秒。

- **非立即坠落模式**（`AircraftFallStartsImmediately = false`）：前 15 秒通过二次缓出加速至最大速率，之后匀速下降。此模式下 `FallDuration` 不控制总时间。

> ⚠️ **本模式目前无法启用**：`AircraftFallStartsImmediately` 是从原版逐字继承的字段（连拼写都一致），**没有 `[XMLStorage]`**，恒为 `true`；`false` 那条分支**从未被执行过**（原版里它就是死代码）。上一条描述保留作参考，**实际行为永远是立即坠落模式**。

---

## 多语言支持

按钮文字（"Disconnect", "Pilot Alert", "Reload Firmware" 等）使用游戏内置的本地化术语，会自动切换语言（中文、英语等）。

## 已知限制

- **读档后飞机状态不可靠**：飞机 daemon 的运行时状态（坠落进度、`IsCrashed` 终结标志等）不随存档
  完整保存。读档后节点本身能恢复正常，但飞机的位置与运行状态可能不正确。

  建议在剧情设计上**避开「飞机飞行途中存读档」的区段**。若确实需要在关键区段阻止存档，
  可依赖第三方模组提供的能力（如 Stuxnet 的 `DenySaves`）——KE 自身不提供存档拦截。

---

## 另请参阅

- [首页](./../index.md) – 返回主索引
- [Aircraft-Daemon-System (English)](./../../en/systems/aircraft.md) – 英文版

- [自定义Action](./../components/actions.md) – 全部自定义动作列表

---

> 特别感谢 **April_Crystal** 为本系统提供核心实现与宝贵建议。