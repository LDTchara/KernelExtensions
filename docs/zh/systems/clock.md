# 自定义定时器系统（Clock）

**Clock** 是一个通用定时器：按固定间隔循环执行一组 Action。

它定位为**剧情资产**——每个 Clock 是独立的 XML 文件，可放在扩展的任意位置（惯例是 `Clocks/` 目录），由 `ClockStart` 按文件路径引用，**不做集中注册**。

它是**系统级定时器**（挂载到 `os.UpdateSubscriptions`），不依赖 DelayHost 节点（区别于原版 `Delay`）。

!!! info "适用版本"
    本页对应 KernelExtensions **0.7**。相关 Action：`ClockStart`、`ClockStop`。

---

## 概览

- 启动：`<ClockStart Filepath="Clocks/traceFlash.xml" />`
- 按 ID 停止：`<ClockStop ClockID="traceFlash" />`（推荐）
- 按路径停止：`<ClockStop Filepath="Clocks/traceFlash.xml" />`（便利通道，与 ID 等价，两者同给时 ID 优先）
- 持久化：运行中的 Clock 存读档自动恢复，见[数据持久化](#数据持久化)
- `Times=1` 的 Clock 就是"无 DelayHost 的延迟执行"（`Interval` 即延迟时间）

---

## Clock 文件结构

```xml
<Clock ID="traceFlash" Interval="5.0" Times="3" Duration="60" OnComplete="Clocks/done.xml">
    <Actions>
        <TerminalType Text="!WARNING!" />
        <FlashScreen Color="Red" Duration="2.0" />
    </Actions>
</Clock>
```

| 属性 | 必填 | 说明 |
|------|:----:|------|
| `ID` | ✅ | 标识，用于 `ClockStop` / 去重；省略时回退为文件名（不含扩展名） |
| `Interval` | ✅ | 触发间隔（秒），必须 `> 0`（非法值 Warn 并拒绝启动） |
| `Times` | ❌ | 循环次数上限；`0` / 省略 / 负数 = 无限；耗尽后自动停止 |
| `Duration` | ❌ | 运行总时长上限（秒）；与 `Times` 谁先到谁停 |
| `OnComplete` | ❌ | 耗尽自动停止后一次性执行的 Action 文件（支持 `<Actions>` / `<ConditionalActions>` 两种根） |
| `<Actions>` | ✅ | **每次触发**执行的序列（预加载的无条件 instantly 集合） |

---

## 触发语义

- Clock 启动后**先等 `Interval` 才第一次触发**（对齐原版 Delay 的"延迟后执行"）；之后每次触发都把计时器重置为 `Interval`，节拍稳定
- `Times` 计的是**触发次数**（每次触发执行一遍 `<Actions>` 记 1 次）；`TimesElapsed >= Times` 或 `Duration` 到点即停止，谁先到谁停
- **`OnComplete` 只在"耗尽自动停止"时触发**：手动 `ClockStop`（剧情取消）不触发；重复 `ClockStart` 的重置也不触发
- Action 自身的耗时**不计入** `Interval`——Clock 只负责"发起"，内部 `Delay` 由原版 `DelayableActionSystem` 自理，因此节拍不会因 Action 耗时而被拖慢

### 执行顺序

- 每个 Clock 独立计时，互不等待（既非并行、也非依赖串行）
- 同一帧到期的多个 Clock，按注册顺序逐个执行
- 执行期间新启动的 Clock 不会在同一帧触发
- 对同一 `ID` 重复 `ClockStart` → 刷新（替换定义，计时与计数重置）

---

## 使用方法

### 1. 创建 Clock 文件

```
ExtensionRoot/
├── Clocks/
│   ├── traceFlash.xml
│   └── done.xml
└── ...
```

```xml
<!-- Clocks/traceFlash.xml -->
<Clock ID="traceFlash" Interval="5.0" Times="3" OnComplete="Clocks/done.xml">
    <Actions>
        <TerminalType Text="!WARNING!" />
        <FlashScreen Color="Red" Duration="2.0" />
    </Actions>
</Clock>
```

### 2. 在剧情 Action 中启动 / 停止

```xml
<ClockStart Filepath="Clocks/traceFlash.xml" />

<!-- 剧情推进到某处时停止 -->
<ClockStop ClockID="traceFlash" />              <!-- 按 ID（推荐） -->
<ClockStop Filepath="Clocks/traceFlash.xml" />  <!-- 按路径（等价；两者同时提供时 ID 优先） -->
```

### 3. OnComplete 文件（仅耗尽时执行）

```xml
<!-- Clocks/done.xml -->
<ConditionalActions>
    <Instantly>
        <TerminalType Text="[CLOCK] completed!" />
    </Instantly>
</ConditionalActions>
```

---

## 数据持久化

运行中的 Clock 随存档保存、读档恢复，剧情定时不因存读档丢失：

- 保存时，**运行中的** Clock 以 `<ClockData>` 节点写入存档（已耗尽或手动停止的 Clock 已被移除，天然不入档）：

  ```xml
  <ClockData>
    <Clock Id="Inf" SourcePath="Extensions/MyExt/Clocks/Inf.xml"
           ExtensionRoot="Extensions/MyExt" TimesElapsed="159" Elapsed="159.72" Timer="0.64" />
  </ClockData>
  ```

- 读档时按 `SourcePath` 重载 Clock 文件（恢复 Actions 定义），再恢复 `TimesElapsed` / `Elapsed` / `Timer`，计时与 `Times` / `Duration` 判定无缝连续
- 手动停止或已耗尽的 Clock 读档后不会复活
- 触发瞬间已"点火"的一次性效果（FlashFade、TimedPrinter 等）不持久化（原版行为）；带 `Delay` 的 Action 由原版 `DelayableActionSystem` 自行持久化，Clock 不介入

---

## 边界与防御

| 情况 | 行为 |
|------|------|
| `Interval <= 0` | `KELog.Warn` 拒绝启动（防每帧死触发） |
| `Times < 0` | 按 `0` 处理（无限） |
| `Duration <= 0` | 不限时长 |
| `<Actions>` 为空 | 仅计时（配置错误由扩展作者负责） |
| 停止未知 `ClockID` / 路径 | 静默忽略 |
| 重复 `ClockStart` 同一 ID | 重置为新定义 |

---

## 相关 Action

### `ClockStart`

```xml
<ClockStart Filepath="Clocks/traceFlash.xml" />
```

| 属性 | 必填 | 说明 |
|------|:----:|------|
| `Filepath` | ✅ | Clock 文件路径（相对扩展根目录） |

### `ClockStop`

```xml
<ClockStop ClockID="traceFlash" />
<ClockStop Filepath="Clocks/traceFlash.xml" />
```

| 属性 | 必填 | 说明 |
|------|:----:|------|
| `ClockID` | ❌ | 目标 Clock 的 `ID`（推荐） |
| `Filepath` | ❌ | 目标 Clock 的文件路径（等价通道；两者同给时 ID 优先） |

---

## 另请参阅

- [首页](./../index.md) – 返回主索引
- [Clock System (English)](./../../en/systems/clock.md) – 英文版
- [自定义 Action](./../components/actions.md) – 全部自定义 Action 列表
- [全局闪烁：FlashScreen](./../components/actions.md) – Clock 内常用动作
