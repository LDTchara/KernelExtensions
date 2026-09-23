# 自定义动态色系统（CustomColor）

**CustomColor** 让颜色字段不再只能是静态色值——用**关键字**代替，就能得到逐帧刷新的动态颜色
（彩虹、循环渐变）。它是一层被多个系统共用的**颜色引擎**：相位穿梭、自定义试炼、标题横幅、
全屏警告、`FlashScreen`、特效播放器都可以直接引用。

!!! info "适用版本"
    本页对应 KernelExtensions **0.7**。

---

## 一、三种写法

| 写法 | 例子 | 说明 |
|------|------|------|
| 彩虹色 | `LDTchara` / `Rainbow` | 内置，**不需要预设文件** |
| 预设引用 | `Riptide` | 引用 `CustomColor/Riptide.xml`（推荐） |
| 旧式双色渐变 | `Gradient:#FF0000:#00FF00:2.0` | 为兼容旧配置保留 |

### 彩虹色参数

语法：`LDTchara:速度:透明度:饱和度:明度`（`Rainbow` 是别名，语法完全相同）

| 写法 | 含义 |
|------|------|
| `LDTchara` | 默认彩虹（速度 `0.1`） |
| `LDTchara:0.05` | 自定义速度 |
| `LDTchara:0.1:0.8` | 速度 + 透明度 80% |
| `LDTchara:0.1:1.0:0.8:1.0` | 速度 + 透明度 + 饱和度 + 明度 |

### 预设引用参数

语法：`预设名:速度倍率:透明度`（最多两个参数）

| 写法 | 含义 |
|------|------|
| `Riptide` | 按预设自身速度播放 |
| `Riptide:0.5` | 速度 ×0.5 |
| `Riptide:0.5:0.8` | 速度 ×0.5 + 透明度 80% |

---

## 二、预设文件

预设文件放在扩展根目录的 **`CustomColor/`** 文件夹下，**扩展加载时扫描一次并缓存**。

```
你的扩展/
├── CustomColor/
│   ├── Riptide.xml
│   └── Monochrome.xml
└── ...
```

文件格式：

```xml
<ColorPreset>
  <Name>Riptide</Name>

  <CustomColor id="0">
    <Color>#FF6B6B</Color>
    <Duration>2.0</Duration>
    <Transition>1.0</Transition>
  </CustomColor>

  <CustomColor id="1">
    <Color>#4ECDC4</Color>
    <Duration>2.0</Duration>
    <Transition>1.0</Transition>
  </CustomColor>
</ColorPreset>
```

| 元素 | 说明 |
|------|------|
| `<Name>` | 预设名，**区分大小写**，必须与引用时的写法一致 |
| `<CustomColor id="N">` | 一个色段，按 `id` 顺序播放 |
| `<Color>` | 该段的颜色（格式见下） |
| `<Duration>` | 停留在此颜色的时长（秒） |
| `<Transition>` | 渐变到下一段颜色的时长（秒），默认 `0`（瞬间切换） |

**规则**

- 至少 **2 个色段**才能形成循环
- 总循环时长 = 各段 `Duration + Transition` 之和
- 颜色支持四种格式，**可混用**：
  `#FF0000`（十六进制 RGB）、`#88FF0000`（十六进制 ARGB，首位为透明度）、
  `255,0,0`（数值 RGB）、`255,0,0,128`（数值 RGBA）

### 常见配方

| 想要的效果 | 怎么配 |
|-----------|--------|
| 三色循环 | 3 个色段，各 `Duration 1.0` + `Transition 1.0` |
| 硬切换（无过渡） | `Transition` 填 `0` |
| 呼吸（淡入淡出） | 2 个色段，各 `Duration 0` + `Transition 2.0`，并给颜色加透明度 |
| 完全静态 | 不引用任何预设，直接写普通色值 |

---

## 三、用在哪里

| 位置 | 典型字段 |
|------|----------|
| 自定义主题的 XML | `<defaultHighlightColor>Rainbow</defaultHighlightColor>` |
| [相位穿梭系统](./phase-swift.md) | 程序窗口 `BackgroundColor` |
| [自定义试炼系统](./custom-trial.md) | `BackgroundColor` / `GlobalTimerColor` / `PhaseTimerColor` / `SpinUpColor` |
| [自定义标题横幅（ShowTitle）](./../components/actions.md) | `AccentColor` |
| [全屏警告特效（ScreenBleed）](./../components/actions.md) | `BackgroundColor` / `TextBackgroundColor` |
| `FlashScreen` | `Color` |

只要某个颜色字段**走 CustomColor 解析**，就能填上面三种写法中的任意一种。
另有部分颜色字段走的是**另一条解析链**（只认固定格式），两者的差异见第四节。

!!! warning "适用范围限制"
    - 主题 XML 的扫描**仅在自定义主题（`OSTheme.Custom`）激活时**发生
    - 只适用于 **OS 自身的颜色字段**；IRC 颜色、任务板颜色等**非 OS 字段不支持**
    - 预设文件在扩展加载时扫描一次；运行中新增预设文件需要重载才会被读到

??? note "主题字段的别名联动"
    以下字段成对联动——写其中一个，另一个会自动跟随同一个动态色：

    | 字段 A | 字段 B |
    |--------|--------|
    | `defaultHighlightColor` | `highlightColor` |
    | `lockedColor` | `brightLockedColor` |
    | `unlockedColor` | `brightUnlockedColor` |
    | `defaultTopBarColor` | `topBarColor` |
    | `moduleColorSolidDefault` | `moduleColorSolid` |

    若两个字段**都**被显式设为动态色，则各自独立，不再联动。

---

## 四、与固定格式解析链的区别

引用预设或动态色只是其中一条路。**不带动态关键字**的普通颜色字符串会走另一条解析链，
两条链支持的格式**并不完全一致**：

| 格式 | CustomColor 预设文件内的 `<Color>` | 普通颜色字段的字符串解析 |
|------|:---:|:---:|
| 十六进制 `#RRGGBB` / `#AARRGGBB` | ✅ | ⚠️ 视字段而定 |
| 数值 `R,G,B` / `R,G,B,A` | ✅ | ✅ |
| XNA 命名色（`Red`） | — | ⚠️ 视字段而定 |
| 动态色关键字 | — | ✅（走本系统） |

<!-- ke:9.50 -->
⚠️ 目前**各字段的普通字符串解析实现不统一**：部分字段（如横幅的 `AccentColor`）尚不支持
十六进制与 XNA 命名色，会静默回退到默认色。**颜色解析统一**已在计划内，届时本节表格会收敛为
一张统一表。在那之前，需要确切颜色时请优先使用**动态色关键字或预设**。

---

## 另请参阅

- [首页](./../index.md) – 返回主索引
- [Custom Color System (English)](./../../en/systems/custom-color.md) – 英文版
- [自定义标题横幅与全屏警告](./../components/actions.md) – 使用 `AccentColor` / `BackgroundColor` 的动作
- [相位穿梭系统](./phase-swift.md) – 场景与程序窗口配色
- [Harmony 补丁](./../components/harmony.md) – `CustomColorPatch`
