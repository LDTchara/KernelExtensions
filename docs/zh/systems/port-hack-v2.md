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
- 到点的协议**立刻结算自己的结果**，不等其它协议。

---

## 根级配置

| 元素 | 说明 |
|------|------|
| `<ProgramName>` | 终端里显示的程序名。留空 = `PortHackV2`。**不影响**注册名 `#PORTHACKV2#` |
| `<RamCost>` | 程序占用内存（MB）。留空 = 246（与原版一致） |
| `<Time>` | Cracking 阶段总时长（秒）。留空 = 10 |
| `<Polyhedron>` | 动画用的柏拉图立体：`Tetrahedron` / `Cube` / `Octahedron` / `Dodecahedron` / `Icosahedron` |
| `<AccentColor>` | **立体线框**的颜色 |
| `<ProgressColor>` | 底部总进度条颜色。留空 = 跟随 `AccentColor` |
| `<BackgroundColor>` | 面板背景色。留空 = 跟随 `AccentColor` |
| `<BackgroundEffect>` | 背景特效类型 |
| `<CompletionText>` | 成功文案。留空 = `COMPLETE` |
| `<SpinUpRatio>` | 起转时长占 `<Time>` 的比例，0~1。留空 = 0.1 |
| `<SpinUpTime>` | 起转时长的**绝对秒数**（覆盖比例）。**负数 = 用比例** |
| `<SpinDownTime>` | 急刹时长（秒）。留空 = 1.7（与原版一致） |
| `<CompleteDelay>` | 成功演出后退出前的停留（秒）。留空 = 5 |
| `<TakeAdmin>` | 破解成功后是否把目标机 owner 设为玩家。省略 = `false`（**只开端口，不擅自给管理员权限**） |
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

`AccentColor` / `ProgressColor` / `BackgroundColor` 都支持 CustomColor 预设名、动态色（`LDTchara` / `Rainbow` / `Gradient:…`），以及静态格式（`#RGB` / `#RRGGBB` / `#AARRGGBB` / `R,G,B[,A]` / XNA 命名色）——详见[自定义动态色系统](./custom-color.md)。

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

---

## 另请参阅

- [可执行程序](./../components/executables.md) – 所有程序一览
- [扩展作者指南](./../guides/author-guide.md) – 动作点的两种写法与可用条件
- [配置文件](./../components/configuration.md) – 配置通用约定（NONE、负数、颜色格式）
- [自定义动态色系统](./custom-color.md) – 颜色解析细节
- [更新日志](./../guides/changelog.md)
