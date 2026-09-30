# 自定义 Porthack 心脏（PorthackHeartDaemon）

**PorthackHeartDaemon（PHD）** 复刻原版的心脏节点序列——旋转线框立方体 → 对齐 → 心形 → 白色淡出——
并把**每个阶段都开放为可配置项**：标题、音乐、时序、回调、输入锁定。

!!! info "适用版本"
    本页对应 KernelExtensions **0.7**。相关 Daemon：`PorthackHeartDaemon`；相关 Action：`BreakHeart`。

---

## 概览

- **声明**：在计算机 XML 里写 `<PorthackHeartDaemon ... />`（全部参数可选，带默认值）
- **默认态**：旋转的 3D 线框立方体 + 闪烁标题
- **触发**：`<BreakHeart>` Action（剧情显式）或 `AutoOnPorthack`（porthack 破解进度 > 50% 自动，一次性标志防重复）
- **结束**：**不触发原版结局**，而是进入终结态（暗屏）+ 通用清理，然后把控制权交给 `OnComplete`

---

## 完整参数

| 参数 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `Title` | 字符串 | `PortHack.Heart` | 默认态的闪烁标题；`NONE` / 空 = 不显示 |
| `Music` | 字符串 | `Music/Ambient/AmbientDrone_Clipped` | 心碎时切换的歌曲；`NONE` / 空 = 不切歌 |
| `FadeoutDelay` | 数值 | `1` | 周围黑幕淡入的**延迟**（秒） |
| `FadeoutDuration` | 数值 | `10` | 周围黑幕淡入的**时长**（秒） |
| `AlignTime` | 数值 | `2.5` | 立方体旋转对齐到正位的时长（秒） |
| `HeartDuration` | 数值 | `30` | 心形序列的总时长（秒） |
| `FlashOutTime` | 数值 | `3.8` | 心形完成后白色淡出的时长（秒） |
| `OnComplete` | 字符串 | 无 | 序列**结束后**加载的 Action 文件（相对扩展根）；`NONE` / 空 = 不执行 |
| `OnHeartbreak` | 字符串 | 无 | **开始碎心时**加载的 Action 文件；`NONE` / 空 = 不执行 |
| `LockInput` | 布尔 | `true` | 心碎期间是否锁定输入、禁用顶栏按钮（对齐原版） |
| `AutoOnPorthack` | 布尔 | `false` | 是否由 porthack 破解（进度 > 50%）自动触发心碎 |

!!! tip "NONE 约定"
    字符串参数写 `NONE`（大小写不敏感）或留空 = **显式禁用该功能**；**不写**该属性 = 使用默认值。

---

## 用法示例

```xml
<Computer id="heart_node" name="Heart" type="empty">
    <PorthackHeartDaemon
        Title="PortHack.Heart"
        Music="Music/Ambient/AmbientDrone_Clipped"
        FadeoutDelay="1"
        FadeoutDuration="10"
        AlignTime="2.5"
        HeartDuration="30"
        FlashOutTime="3.8"
        OnHeartbreak="Actions/HeartStart"
        OnComplete="Actions/HeartBroken"
        LockInput="true"
        AutoOnPorthack="false" />
</Computer>
```

显式触发碎心（Action 侧的参数均为**覆盖项**）：

```xml
<BreakHeart NodeID="heart_node" OnComplete="Actions/HeartBroken" />
```

---

## 序列流程

```
默认态：旋转 3D 线框立方体 + 闪烁标题（Title）
   ↓  触发：<BreakHeart> 或 AutoOnPorthack（破解进度 > 50%）
① 取消追踪 / 清除弹窗 / 锁定输入（LockInput = true 时）
② 执行 OnHeartbreak 动作文件
③ 切换音乐（Music）并播放启动音效（SFX/TraceKill）
④ 18 秒后播放 glow 音效（SFX/Ending/PorthackSpindown）
⑤ 立方体旋转对齐（AlignTime）
⑥ 心形序列（HeartDuration）
⑦ 白色淡出（FlashOutTime）；周围黑幕在此期间淡入（延迟 FadeoutDelay，时长 FadeoutDuration）
⑧ 进入终结态（暗屏）+ 通用清理
⑨ 执行 OnComplete 动作文件 → 控制权交给剧情
```

---

## 终结态与清理

序列结束后，PHD 会自动做一次**通用清理**，让剧情可以安全接管：

- 解锁输入
- 断开玩家连接
- **heart 节点失效**：移除可见性 / 设为 `disabled` / 清空其 daemon / 换成随机 IP
- 刷新 `ComputerLookup`

随后执行 `OnComplete`。**结局任务、flag、音乐、存档处理都由作者在 `OnComplete` 里自行定义**——
PHD 不替你做这些决定。

---

## 与「自定义结局」的关系

这是**两件事**，容易混：

| | PorthackHeartDaemon | [自定义结局系统（StartEnding）](./custom-ending.md) |
|---|---|---|
| 内容 | **心碎序列**：立方体 → 心形 → 白场 | **结局画面**：报幕文本 + 字幕 + 音乐 |
| 触发 | `BreakHeart` / 自动 | `StartEnding` |
| 结束后的世界 | 进入终结态 + 清理节点 | 按配置淡出、回到主线或停在此处 |

**想在心碎之后播放自定义结局** → 在 `OnComplete` 里写 `<StartEnding ... />` 即可。

---

## 已知限制

- **不触发原版结局**（设计如此）：原版的 `endingSequence` 是硬编码结局，对扩展没有意义。
  心碎后的走向完全由 `OnComplete` 决定。
- **音效不开放配置**：启动音效与 glow 音效直接使用原版 Content 路径（`SFX/TraceKill`、
  `SFX/Ending/PorthackSpindown`），资源缺失时静默跳过。
- ⚠️ **`ResetHeartbreak()` 复位重玩没有 XML 入口**：它是 `PorthackHeartDaemon` 的 **public 方法**，
  目前只能由代码（模组作者）调用，纯扩展作者无法用 XML 触发。需要 XML 入口的话请提出需求。
- 心碎序列期间会**锁定输入**（除非 `LockInput="false"`），`LockInput` 写其他值时按 `true` 处理。

---

## 相关 Action

### `BreakHeart`

显式触发目标节点的 PorthackHeartDaemon 心碎序列。

| 属性 | 必填 | 说明 |
|------|:----:|------|
| `NodeID` | ✅ | 目标计算机 ID |
| 其余参数 | ❌ | 均为**覆盖项**：不写 = 用 daemon 自身配置；写 `NONE` 或留空 = 显式禁用 |

```xml
<BreakHeart NodeID="heart" />
<BreakHeart NodeID="heart" OnComplete="Actions/HeartBroken" />
```

---

## 另请参阅

- [首页](./../index.md) – 返回主索引
- [PorthackHeartDaemon (English)](./../../en/systems/porthack-heart.md) – 英文版
- [自定义结局系统（StartEnding）](./custom-ending.md) – 心碎之后想接结局画面
- [自定义 Daemon](./../components/daemons.md) – Daemon 清单
- [自定义 Action](./../components/actions.md) – `BreakHeart` 与其余动作
