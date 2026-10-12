# 可执行程序

KernelExtensions 向 Pathfinder 注册了 **6 个**自定义可执行程序，每个绑定一个 `#名称#` 自替换符。
下表就是归这一类的**全部**内容。

| 程序 | 注册名 | 所属功能 | 状态 |
|------|--------|----------|------|
| `CustomTrial` | `#CUSTOMTRIAL#` | [自定义试炼系统](./../systems/custom-trial.md) | ✅ 可用 |
| `PhaseSwift` | `#PHASESWIFT#` | [相位穿梭系统](./../systems/phase-swift.md) | ✅ 可用 |
| `PortHackV2` | `#PORTHACKV2#` | [PortHackV2 破解程序](./../systems/port-hack-v2.md) | ✅ 可用 |
| `ProxyKiller` | `#PROXYKILLER#` | 常驻加速器：加速所有正在过载的 Shell | ✅ 可用 |
| `EffectsPlayer` | `#EFFECTS#` | 特效播放（动态壁纸 / 视频播放的底层能力） | ⚠️ 所属功能未完成 |
| `WPTEST` | `#WPTEST#` | 动态壁纸（测试程序） | ⚠️ 所属功能未完成 |

!!! info "后两个为什么看着像「杂项」"
    `#EFFECTS#` 与 `#WPTEST#` 属于**尚未完成**的功能 —— 动态壁纸与视频播放（都排在 0.8 计划内）。
    它们现在就能注册、自替换符也能解析，但**没有配套的稳定用法文档**，因此请把它们当作
    **实验性入口**看待，不要写进正式发布的内容。

!!! warning "运行前必须让文件真的存在"
    注册**只**让自替换符可解析，**不会把文件放进节点**。玩家要能运行这些程序，
    必须先在自己电脑的 `bin/` 里存在对应文件——在内容 XML 中声明即可：

    ```xml
    <file path="bin" name="CustomTrial.exe">#CUSTOMTRIAL#</file>
    ```

    存档生成时 `#CUSTOMTRIAL#` 会被替换为真正的程序内容，玩家才能运行；文件缺失则无法运行。

## CustomTrial 的调用方式

通过 `CustomTrial_` 开头的 Flag 指定要加载的配置（例如 `CustomTrial_MyTrial`）。
详细用法、配置说明和可用特效请参阅 **[自定义试炼系统](./../systems/custom-trial.md)** 页面。

## PhaseSwift 的调用方式

通过 `PhaseSwift_` 开头的 Flag 指定要加载的配置（例如 `PhaseSwift_MyConfig`）。
详细用法、场景配置与音乐组说明请参阅 **[相位穿梭系统](./../systems/phase-swift.md)** 页面。

## PortHackV2 的调用方式

通过 `PortHackV2_` 开头的 Flag 指定要加载的配置（例如 `PortHackV2_MyCrack`），
配置放在 `<扩展根>/PortHackV2/` 下。
详细用法与完整配置说明请参阅 **[PortHackV2 破解程序](./../systems/port-hack-v2.md)** 页面。

## ProxyKiller 的调用方式

通过 `ProxyKiller_` 开头的 Flag 指定要加载的配置（例如 `ProxyKiller_Default`），
配置放在 `<扩展根>/ProxyKiller/` 下。

它是**常驻加速器**：加速所有**正在过载**的 Shell，让代理更快瘫痪。
也可以开启 `<SelfOverload>` 让它**自己过载代理** —— 那时不再需要 Shell。

!!! warning "默认不瘫痪任何代理"
    原版把「代理过载」做成 Shell 的一种状态，而运行 Shell 需要目标机的管理员权限 —— 那道门槛是设计护栏。

    **默认**（`<SelfOverload>` 为 `false`）它只加速**已经在过载中**的 Shell：
    没有 shell 正在过载时，它只是占着内存跑动画。

    开启 `<SelfOverload>` 后它会自己推进过载。这**不是拆护栏**：原版里「过载代理」本身没有权限门槛
    （`ShellExe.StartOverload` 内无任何检查），门槛只在**运行 Shell** 上 —— 这一项补的是原版一直缺失的手段。

- 倍速由**内存爬升**线性驱动（起始 50MB，`<BurstTarget>` 为爬满目标）。
- N 个 shell 同时在过载时，总速率 = N × 倍速。
- 每帧重新遍历，所以**先运行、后开的 shell 一样会被加速**。
- 只碰**正在过载**的 shell —— 否则空闲 shell 下次过载会从负数起跑，等于秒杀。
- **同一时间只能运行一个实例**：重复执行会被拒绝。它常驻且副作用是全局的，
  两个实例会让同一个 shell 被削两次。

| 元素 | 说明 |
|------|------|
| `<ProgramName>` | 终端里显示的程序名。留空 = `ProxyKiller` |
| `<BurstTarget>` | 爬满时的目标内存（MB）。留空 = 100 |
| `<RampRate>` | 内存爬升速率（MB/秒）。留空 = 200 |
| `<ShellOverloadTarget>` | **加速已有 Shell** 时，爬满时的倍速。留空 = 1（不加速） |
| `<SelfOverload>` | **自己过载代理**的开关。留空 = `false`（不主动过载） |
| `<SelfOverloadRate>` | 自己过载的速率倍数。**`1` = 原版一个 Shell 的过载速度**。留空 = 1。**无上限** |
| `<FollowConnection>` | `true` = 玩家离开启动时所在的节点就自动关闭。留空 = `false`（常驻）|
| `<AutoCloseOnOverload>` | 所盯节点的代理**由生效变瘫痪**后自动关闭。留空 = `false` |
| `<BackgroundColor>` | 数据流颜色。`auto`（大小写不敏感）= 主题高亮色；留空 / `NONE` = 主题高亮色 |
| `<BackgroundCount>` | 在参考高度下的粒子数。留空 = 230 |
| `<BackgroundReferenceHeight>` | 粒子数的参考高度（像素）。留空 = 200 |
| `<BackgroundSpeed>` | 在参考高度下的流速（像素/秒）。留空 = 300 |
| `<ParticlePixels>` | 粒子头部尺寸（像素）。留空 = 2 |
| `<TrailPixels>` | 拖尾长度（像素）。留空 = 5 |
| `<LoadTrailGain>` | 爬满时拖尾的放大倍数。留空 = 2 |
| `<LoadSpeedGain>` | 爬满时流速的放大倍数。留空 = 2.6 |

!!! note "`<SelfOverload>` 打开时强制跟随连接"
    打开自己过载时，`<FollowConnection>` **被忽略并强制为 `true`**（会记一条 Warn）。
    这是该能力的**固有约束，不是可配选项**，原因是**双重**的：

    - **过载会持续触发目标机的敌对行为**。这沿用了原版的做法（Shell 过载时每帧都调）；
      原版的每个端口破解器也都这么做。它带来 trace 与视觉提示。
    - 而那个敌对行为**只在玩家正连着那台机时才生效** —— 不跟随的话，就等于
      "跑到哪打到哪"：走到哪台机就把哪台机的敌对反应点起来。

    ⚠️ 其中「起 trace」这一半还要求目标机**安全等级 ≥ 4**（原版只为这类机器赋 trace 值）。
    低安机器只会收到视觉提示，不会起 trace。

    「所盯节点」= 跟随连接时的**当前所连节点**、否则是启动时所在节点；
    `<AutoCloseOnOverload>` 看的就是它的代理状态。

!!! tip "开关与速率为什么是两个字段"
    `<SelfOverload>` 只管**开不开**，`<SelfOverloadRate>` 只管**多快**。
    合成一个字段（用 `0` 表示关）会让 `0` 同时表示"关掉"和"速率为零"两件事，
    作者也就无法表达"打开、但速率还没想好"。

!!! info "与原版 `SignalScramble`（DLCTraceSlower）的关系"
    本程序的界面观感取自原版 `SignalScramble`，但机制**相反**，因此可以并存：

    | | 原版 SignalScramble | ProxyKiller |
    |---|---|---|
    | 作用 | **压制** trace 速度 | 加速代理过载 / 自己过载 |
    | 敌对行为 | **不调** | 调（过载时每帧） |
    | 跟随连接 | **是**（换节点立即退出） | 可选；自己过载时强制 |

    原版的跟随连接正是本程序把 `<FollowConnection>` 做成配置项、并在自己过载时强制的依据。

!!! tip "与 PortHackV2 的 `<RequireProxyOverload>` 同用时注意"
    `<SelfOverload>` 开启后，「目标机代理已瘫痪」这个前置会被**免费满足**。
    如果同一扩展里还有依赖它的破解程序，请用 flag 或其它前置把两者门控开。

**固定不可配**：起始内存 50MB、背景类型（上升数据流）、敌对行为的触发间隔（沿用原版 0.35 秒）。

粒子数与流速按**内容区高度**换算 —— 配置里的 `BackgroundCount` / `BackgroundSpeed`
是"在 `BackgroundReferenceHeight` 这个高度下"的值，窗口大小变化时按幂律缩放，所以不同窗口下观感一致。

参考示例见 `XMLExamples/ProxyKiller_Example.xml`。

---

## 另请参阅

- [首页](./../index.md) – 返回主索引
- [Executables (English)](./../../en/components/executables.md) – 英文版
- [自定义 Daemon](./daemons.md) – 另一类可挂载对象
- [自定义 Action](./actions.md) – 动作清单
