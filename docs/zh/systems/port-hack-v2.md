# PortHackV2 破解程序

**PortHackV2** 是原版 `PortHack` 的可配置重制版：破解流程、时长、外观、前置条件与收尾动作全部由 XML 配置驱动。

它定位为**剧情资产**——每份配置对应一次破解，放在 `<扩展根>/PortHackV2/` 下，由存档 flag 指名加载。

!!! info "适用版本"
    本页对应 KernelExtensions **0.7**。

**概览**

- 注册名：`#PORTHACKV2#`
- 配置目录：`<扩展根>/PortHackV2/`
- 激活：给玩家挂上存档 flag `PortHackV2_<文件名>`（不含 `.xml`）
- 默认内存占用：246MB（与原版 `PortHack` 一致，可用 `<RamCost>` 改）
- 状态机：Locked → Listing → Cracking → Complete / 中断

---

## 怎么用

1. 在 `<扩展根>/PortHackV2/` 放一份配置，例如 `MyCrack.xml`。
2. 给玩家挂上存档 flag `PortHackV2_MyCrack`（**不含 `.xml`**）。
3. 玩家在终端里运行 `#PORTHACKV2#`，连上满足前置条件的目标机。

flag 就是"入口牌"：

| flag 情况 | 行为 |
|---|---|
| 一个都不存在 | 程序**锁定**，显示前置条件未满足的提示 |
| 恰好一个 | 加载对应配置并正常运行 |
| 出现多个 | 视为作者配置错误 —— **锁定**并在日志里报错 |

参考示例见 `XMLExamples/PortHackV2_Example.xml`。

---

## 流程

| 状态 | 内容 |
|---|---|
| **Locked** | 前置条件未满足时的锁定界面（可看到缺什么） |
| **Listing** | 列出本配置的所有协议与各自的前置条件 |
| **Cracking** | 多协议**并行**推进，各自按 `StartDelay` 错开启动 |
| **Complete** | 成功演出 |
| **中断** | 连接断开时的收尾 |

### 破解过程怎么算

- 每个协议在**自己启动的那一刻**掷一次随机数决定成败（由 `<Rng>` 播种），分支一旦定下就不再变化——但**两条分支的演出完全一样**，只有时长不同（`<TimeIfSuccess>` / `<TimeIfFail>`），所以玩家看不出来。
- 协议的启动时间由它自己的 `<StartDelay>` 决定（相对 Cracking 开始）。
- 根级 `<Time>` 是整个 Cracking 阶段的总时长，底部进度条走的就是它。
  Listing 屏左下角那行「预计 Ns」显示的也是**同一个数**（进度条、倒计时、预计时长三者一致）。
- 到点的协议**立刻结算自己的结果**，不等其它协议。

---

## 根级配置

| 元素 | 说明 |
|------|------|
| `<ProgramName>` | 终端里显示的程序名。留空 = `PortHackV2`。**不影响**注册名 `#PORTHACKV2#` |
| `<RamCost>` | 程序占用内存（MB）。留空 = 246（与原版一致） |
| `<Time>` | Cracking 阶段总时长（秒）。留空 = 10。也是 Listing 屏「预计 Ns」显示的值 |
| `<ShowEstimate>` | Listing 屏是否显示「预计破解时长」。留空 / 非布尔 = `true`。写 `false` 隐藏 —— 时长本身是剧透信息 |
| `<Polyhedron>` | 动画用的柏拉图立体：`Tetrahedron` / `Cube` / `Octahedron` / `Dodecahedron` / `Icosahedron` |
| `<PolyhedronStyle>` | 立体怎么画：`Wireframe`（线框，**默认**）/ `Solid`（实心面）/ `SolidWire`（实心 + 线框） |
| `<SpinSpeed>` | 立体的**目标转速**（弧度/秒）。留空 = 3.2（约每 2 秒一圈） |
| `<TrailLayers>` | **多层尾迹层数**：1（默认）= 单立体；20 ≈ 原版 PortHack 20 层尾迹。5 个立体共用同一套尾迹逻辑 |
| `<TrailSizeStep>` | 每层额外尺寸（相对模型半径）。留空 = 0.10（20 层最外层约 2.9×，接近原版比例）；0 = 所有层同尺寸 |
| `<TrailRotationStep>` | 每层绕自转轴的旋转偏移（弧度）。留空 = 0.10；0 = 所有层同角度 |
| `<TrailPulse>` | 逐层转速脉冲强度。留空 = 0.05；0 = 所有层同角速度 |
| `<TrailContraction>` | spindown 时是否收缩尾迹。留空 = true |
| `<IdleSpinSpeed>` | 成功演出期间**背景慢转**的速度（弧度/秒）。留空 = 按 `SpinSpeed` 等比缩放 |
| `<AccentColor>` | **立体**的颜色（线框与实心面共用；实心面上它是基色，明暗由渲染叠加） |
| `<ProgressColor>` | 底部总进度条颜色。留空 = 跟随 `AccentColor` |
| `<BackgroundColor>` | 面板背景色。留空 = 跟随 `AccentColor` |
| `<BackgroundEffect>` | 背景特效类型 |
| `<CompletionText>` | 成功文案。留空 = `COMPLETE` |
| `<SpinUpRatio>` | 起转时长占 `<Time>` 的比例，0~1。留空 = 0.1 |
| `<SpinUpTime>` | 起转时长的**绝对秒数**（覆盖比例）。**负数 = 用比例** |
| `<SpinDownTime>` | 急刹时长（秒）。留空 = 1.7（与原版一致） |
| `<CompleteDelay>` | 成功演出后退出前的停留（秒）。留空 = 5 |
| `<TakeAdmin>` | 是否把目标机 owner 设为玩家。**只看开关，与协议成败无关**（全部协议失败也夺；目标不可达时只记 Warn）。省略 = `false`（**只开端口，不擅自给管理员权限**） |
| `<AutoExitOnLeave>` | 玩家离开本程序所在节点时是否自动关闭。默认 `true`（原版行为） |
| `<RequireCrackedPorts>` | 可选前置：目标机上**已破解端口数量**要求，见下节 |
| `<RequireOpenPortsBefore>` | 前置：这些端口必须先打开。写成多个 `<string>` 子元素 |
| `<RequireSolveFirewall>` | 前置：需要先解决防火墙（`true` / `false`） |
| `<RequireProxyOverload>` | 前置：需要先让代理瘫痪（`true` / `false`） |
| `<OnStart>` / `<OnComplete>` | 根级动作点，见下 |
| `<Protocols>` | 协议列表，见下 |

### 动作点

根级与协议级都有动作点；每个动作点**既可以内联写动作，也可以填一个动作文件路径**（相对扩展根）
—— 详见[扩展作者指南 · 四、动作点：内联还是引文件](./../guides/author-guide.md)。

| 动作点 | 位置 | 触发时机 |
|--------|------|----------|
| `<OnStart>` | 根级 | 破解整体开始 |
| `<OnComplete>` | 根级 | 整体收尾（总时长跑满） |
| `<OnStart>` | 协议级 | 该协议启动 |
| `<OnComplete>` | 协议级 | 该协议成功 |
| `<OnFail>` | 协议级 | 该协议失败 |

根级**没有** `OnFail`——根级没有随机性，总时长跑满固定走成功演出；某个协议有没有破开由它自己的 `OnFail` 表达。

一次性效果（放音效、开端口之外的动作）一律写在动作点里。程序自身只负责"成功协议各自把端口打开"这一件事。

### 协议

```xml
<Protocols>
  <Protocol>
    <Name>ssh</Name>
    <StartDelay>0</StartDelay>
    <Rng>60</Rng>
    <TimeIfSuccess>12</TimeIfSuccess>
    <TimeIfFail>4</TimeIfFail>
    <OnStart>Actions/sshStart.xml</OnStart>
    <OnComplete>Actions/sshDone.xml</OnComplete>
    <OnFail>Actions/sshFail.xml</OnFail>
  </Protocol>
  <Protocol>
    <Name>ftp</Name>
    <StartDelay>3</StartDelay>
    <Rng>90</Rng>
    <TimeIfSuccess>6</TimeIfSuccess>
    <TimeIfFail>3</TimeIfFail>
  </Protocol>
</Protocols>
```

| 元素 | 必填 | 说明 |
|------|:----:|------|
| `<Name>` | ❌ | 协议名（显示用，也用于日志溯源） |
| `<StartDelay>` | ❌ | 相对 Cracking 开始的启动延迟（秒） |
| `<Rng>` | ❌ | 该协议的随机数种子 |
| `<TimeIfSuccess>` | ❌ | 成功分支的时长（秒） |
| `<TimeIfFail>` | ❌ | 失败分支的时长（秒） |
| `<OnStart>` / `<OnComplete>` / `<OnFail>` | ❌ | 该协议自己的动作点 |

### 已破解端口数前置

`<RequireCrackedPorts>` 支持三种写法：

| 写法 | 含义 |
|------|------|
| 不写 / 留空 / `0` / 负数 | **不检查** |
| `auto`（大小写不敏感） | 对齐**目标节点自己的要求**——即原版 `PortHack` 的判定 |
| 正整数 N | 固定要求**至少 N 个**已破解端口 |

!!! warning "两条语义的差别"
    `auto` 沿用的是节点自身的 `portsNeededForCrack`（原版引擎按 security 算出来的值，界面上显示的也是它 +1）。手写数字则是"作者说至少几个就至少几个"，**不做 +1**。

### 颜色

`AccentColor` / `ProgressColor` / `BackgroundColor` 都支持 CustomColor 预设名、动态色（`LDTchara` / `RGB` / `Gradient:…`），以及静态格式（`#RGB` / `#RRGGBB` / `#AARRGGBB` / `R,G,B[,A]` / XNA 命名色）——详见[自定义动态色系统](./custom-color.md)。

留空或写 `NONE` = 用默认（`ProgressColor` / `BackgroundColor` 会跟随 `AccentColor`）。目标机状态的语义色（满足 / 未满足）仍走主题色，不归这几项管。

---

## 界面

- **Locked / Listing** 铺背景——这两个状态没有自己的画面。
- **Cracking / Complete** **不铺背景**：立体动画本身就是画面主体。
- 顶部标题栏显示 `IP: <目标>` 与程序名（原版行为）。
- **Cracking 阶段不显示退出按钮**——破解中不给退出；退出按钮在其它状态出现，且退出瞬间立即消失。
- 底部总进度条的颜色由 `<ProgressColor>` 决定。

### 动画

立体动画走离屏渲染后贴回（原版在一个 `SpriteBatch.Begin()` 里，中途不能再起一个批次），包含起转、匀速、急刹三段。

每种柏拉图立体都绕**自己的旋转对称轴**自转，且那根轴保持竖直。

#### 多层尾迹（`<TrailLayers>`）

`TrailLayers=1`（默认）保持原来的单立体效果；写 `20` 时同一个立体画 20 遍：
每层按 `TrailSizeStep` 放大、按 `TrailRotationStep` 错开旋转角，形成螺旋尾迹；
spindown 时外层按指数收缩、层间旋转偏移归零（对齐原版 20 层立方体），
最终收成一个立体再进入 `IdleSpin` 慢转。

- **5 个立体共用一套参数**：Cube / Tetrahedron / Octahedron / Dodecahedron / Icosahedron 都能开尾迹；
- 实心模式（`Solid` / `SolidWire`）暂不参与尾迹，仍按 1 层绘制（避免多层实心 z-fighting）；
- 层数越高单帧绘制调用越多（20 层 ≈ 600 条线），但仍在预算内；上限 64；
- 尾迹模式使用固定相机（俯角 30° / 水平 15°、固定距离）；**调 `TrailSizeStep` 不会移动相机**，最终收缩尺寸也不随它变化。单层模式保持原相机。

#### 呈现方式（`<PolyhedronStyle>`）

| 值 | 效果 |
|----|------|
| `Wireframe` | **默认**。只画线框，与原版 `PortHack` 观感一致 |
| `Solid` | 只画实心面：**逐面明暗**（平面着色）+ 背面剔除 |
| `SolidWire` | 实心面 **+** 线框叠加：低多边形观感，**被面挡住的棱不可见**（线也过深度测试） |

大小写不敏感，也接受 `wire` / `solid` / `both` 等简写；无法识别时回退 `Wireframe` 并记一条警告。

实心面的明暗是**每面一个亮度**（不是逐顶点插值）—— 低多边形"看得出体积"靠的就是这个；
逐顶点插值会把棱角磨平，反而糊。光向在**屏幕空间固定**，所以立体转过去面才变暗，
观感是"实体在转"而不是"光在跟着转"。

!!! tip "面数多的立体建议用 `SolidWire`"
    面数少的（正四面体 4 / 立方体 6 / 正八面体 8）纯 `Solid` 体积感最好；
    面数多的（正十二面体 12 / 正二十面体 20）纯实心容易糊成一团，加回线框（`SolidWire`）层次更清楚。

!!! note "`Wireframe` 是默认值"
    面渲染是新增能力，默认值必须等于加入之前的行为 —— 所以已有配置的观感不会改变，
    想试实心面请显式写 `<PolyhedronStyle>`。

#### 转速（`<SpinSpeed>`）

立体整个动画的角速度是"**慢 → 快 → 慢**"三段：起转线性加速到目标转速，匀速到破解结束，
再指数急刹回慢转。`<SpinSpeed>` 调的就是中间那个**目标转速**（弧度/秒）：

| 值 | 观感 |
|----|------|
| `0` | 不转（静态展品） |
| `1.6` | 慢 |
| `3.2` | **默认**，约每 2 秒一圈 |
| `6.4` | 明显更快 |
| `9.6` | 很快，且开始看出一跳一跳 |

**起转与急刹后的慢转按固定比例跟着缩放**（0.094 倍与 0.156 倍），所以整条转速曲线不会变形 ——
这是调转速时通常想要的效果。

!!! warning "转速不能无限加"
    转速越高，**单帧转角**越大，到某个点就从"转得快"变成"一跳一跳"（观感是跳帧而非流畅）。
    60fps 下每帧转角 ≈ `SpinSpeed × 0.955` 度：

    | `SpinSpeed` | 每帧转角 | 观感 |
    |---|---|---|
    | 3.2（默认） | 约 3.1° | 稳 |
    | 6.4 | 约 6.1° | 已能看出跳 |
    | 12 | 约 11.5° | 明显跳帧 |

    帧率低于 60fps 时倍数更大。**想更强烈的动感，调 `<Time>` 或换呈现方式通常比一味提转速更有效。**

`<IdleSpinSpeed>` 单独控制**成功演出期间**背景里的慢转（那一段是 `IdleSpin`，立体继续转着当背景）。
留空或负数 = 按 `SpinSpeed` 等比缩放；想让收尾画面动得明显些就单独调它。

---

## 另请参阅

- [可执行程序](./../components/executables.md) – 所有程序一览
- [扩展作者指南](./../guides/author-guide.md) – 动作点的两种写法与可用条件
- [配置文件](./../components/configuration.md) – 配置通用约定（NONE、负数、颜色格式）
- [自定义动态色系统](./custom-color.md) – 颜色解析细节
- [更新日志](./../guides/changelog.md)
