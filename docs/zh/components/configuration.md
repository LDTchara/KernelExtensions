# 配置文件

KernelExtensions 的配置分**两层**：

- **扩展级配置** —— `KE-Config.xml`，KE 自身的全局开关。**本页详述**。
- **各系统配置** —— 每个系统有自己的配置文件或配置段，**详见对应系统页**；本页只给索引。

所有路径均相对于扩展根目录。

---

## 一、扩展级配置（KE-Config.xml）

- 位置：扩展根目录的 `KE-Config.xml`（**不存在时 KE 会自动创建**一份带注释的模板）
- 根元素：`<KEConfig>`
- 这是 KE 自身的全局开关，与各系统配置分开；每次 OSLoad 重新读取（改完重启游戏生效）

| 配置项 | 默认 | 说明 |
|--------|------|------|
| `<Debug>` | `false` | 输出 KE 调试级日志（发布版请保持 `false`） |
| `<Watermark>` | `true` | 主菜单水印是否显示（外观见下） |
| `<SkipVanillaIRCLogs>` | `false` | 跳过原版 `BashLogs.txt` IRC 日志（只加载自定义日志，见下节）；无自定义日志时自动回退原版避免崩 |
| `<CustomIRCLogs>` | — | 额外加载的 IRC 日志文件列表（`<File>` 子元素，路径相对扩展根）；一个都不写时回退到 `CustomIRCLogs.txt`——见下节 |

### 自定义 IRC 日志

IRC 日志会出现在**文件浏览器**里（也就是原版 `BashLogs.txt` 那一栏）。KE 可以在原版之外再塞若干个自己的日志文件。

#### 怎么配

在扩展根目录的 `KE-Config.xml` 里列出要加载的文件（可多个、路径自定义）：

```xml
<SkipVanillaIRCLogs>false</SkipVanillaIRCLogs>

<CustomIRCLogs>
    <File>News/Archive1.txt</File>
    <File>News/Archive2.txt</File>
</CustomIRCLogs>
```

| 写法 | 行为 |
|------|------|
| 列了 `<File>` | 逐个加载这些文件 |
| 一个 `<File>` 都不写 | 回退到扩展根下的 **`CustomIRCLogs.txt`**（旧写法照旧可用） |
| `<SkipVanillaIRCLogs>false</SkipVanillaIRCLogs>`（默认） | 原版文件 + 原版 `BashLogs.txt` + 你的日志**都加载** |
| `<SkipVanillaIRCLogs>true</SkipVanillaIRCLogs>` | **只加载**你的日志；⚠️ 若一个自定义日志都找不到（含 `CustomIRCLogs.txt`），会**强制回退原版日志并记 Warn** —— 空 `FileEntry` 列表会让游戏加载随机文件节点时崩溃 |

- 路径**相对扩展根目录**，不得用 `..` 逃出扩展目录（越界条目会被跳过并记一条 Warn，其余照常）
- 文件不存在时跳过并记 Warn，**不影响其余日志**
- 改完重启游戏生效

#### 文件格式

每条日志以**行首 `#`** 开始：

```
# 话题标题
第 1 行正文
第 2 行正文
# 另一个话题
正文……
```

- 话题标题里可以写 **`标题|来源`** —— 竖线右边会成为条目末尾的 `Archived Via : 来源`；不写竖线时来源取**扩展名**
- 标题里的 `- [X]` 与**所有空格**会被去掉，然后前加 `IRC_Log:` 作为文件浏览器里显示的文件名
- 没有任何 `|` 与标记时，条目就是"`IRC_Log:标题` + 正文 + `Archived Via : 扩展名`"

!!! tip "顺带一提"
    这份格式说明也会写进自动生成的 `KE-Config.xml` 模板里 —— 删掉 `KE-Config.xml` 再跑一次游戏，
    就能拿到带完整注释的模板。

---

| `<CustomImages>` | — | 自定义图标注册（`<Image>` 子元素，路径相对扩展根，自动注册为 `@文件名`），供 `SetNodeIcon` 使用——见[自定义节点图标系统](./../systems/node-icon.md) |
| `<BannedUsernames>` | — | 新账号创建时的用户名拦截（支持直接原因与随机原因块）——见[禁用用户名](#禁用用户名) |

### 主菜单水印长什么样

开启时（默认），Hacknet 主菜单标题右侧会出现 **`+ KernelExtensions <版本号>`**：

- 位置在 ZeroDayToolKit 水印**右侧、同一行**，与其他模组水印不重叠
- 文字颜色**随时间平滑流动**（彩虹色），并带逐字轻微的上下晃动
- 前缀 `+` 是社区模组水印的通用风格
- 版本号自动跟随模组版本，无需手动维护
- 扩展卸载时水印自动消失

把 `<Watermark>` 设为 `false` 即可关闭。

---

### 禁用用户名

创建新账号时拦截指定的用户名，并给玩家显示一条原因。

```xml
<BannedUsernames>
    <Reasons>
        <Block Name="Test1">
            <Reason>该名称已被占用，再试一遍也没用。</Reason>
            <Reason>换一个吧。</Reason>
        </Block>
    </Reasons>
    <Ban Name="admin" Reason="保留用户名" />
    <Ban Name="root" ReasonBlock="Test1" />
</BannedUsernames>
```

| 元素 / 属性 | 说明 |
|------|------|
| `<Ban Name="…">` | 要拦截的用户名。**大小写不敏感** |
| `Reason="…"` | 直接原因（一条固定文案） |
| `ReasonBlock="…"` | 引用 `<Reasons>` 里某个 `<Block Name>`，从该块**随机**选一条 `<Reason>` |
| `<Reasons><Block Name="…">` | 原因块容器；块名供 `ReasonBlock` 引用 |
| 块内多条 `<Reason>` | 供随机挑选 |
| **同名多条 `<Ban>`** | 原因**追加到同一个池**（不是覆盖）——配合 `ReasonBlock` 可做多套随机文案 |

- `Reason` 与 `ReasonBlock` 可以只写一个；两个都写时优先用直接原因
- 原因是**给玩家看的文案**，走游戏本地化显示

---

## 二、各系统配置索引

各系统的配置格式与**全部字段**都在它们各自的系统页里维护；下表只回答「配置放在哪」。

| 系统 | 配置位置 | 根元素 | 详情 |
|------|----------|--------|------|
| 自定义试炼 | `Trial/<名称>.xml` | `<TrialConfig>` | [自定义试炼系统](./../systems/custom-trial.md) |
| VM 攻击 | `VMATK/<名称>.xml` | `<VMAttackConfig>` | [VM攻击系统](./../systems/vm-attack.md) |
| PortHackV2 破解程序 | `PortHackV2/<名称>.xml` | `<PortHackV2Config>` | [PortHackV2 破解程序](./../systems/port-hack-v2.md)（入口：存档 flag `PortHackV2_<名称>` → 终端运行 `#PORTHACKV2#`） |
| 代理加速器 ProxyKiller | `ProxyKiller/<名称>.xml` | `<ProxyKillerConfig>` | [可执行程序](./executables.md)（入口：存档 flag `ProxyKiller_<名称>` → 终端运行 `#PROXYKILLER#`） |
| 相位穿梭 | 见系统页 | — | [相位穿梭系统](./../systems/phase-swift.md) |
| 自定义结局 | 见系统页 | — | [自定义结局系统](./../systems/custom-ending.md) |
| 飞机 Daemon | 直接写进目标计算机的 XML | — | [飞机Daemon系统](./../systems/aircraft.md) |
| 自定义心脏 Daemon（CustomHeartDaemon） | 直接写进目标计算机的 XML（`<CustomHeartDaemon … />`） | — | [自定义心脏 Daemon](./../systems/custom-heart.md)（入口：`<BreakHeart>` Action，或 `AutoOnPorthack` 自动触发） |
| 自定义动态色预设 | `CustomColor/<预设名>.xml` | `<ColorPreset>` | [自定义动态色系统](./../systems/custom-color.md) |
| 节点图标 | `KE-Config.xml` 的 `<CustomImages>` | — | [自定义节点图标系统](./../systems/node-icon.md) |
| 定时器 / 横幅 / 全屏警告 | 由 Action 参数直接指定，无独立配置文件 | — | [自定义 Action](./actions.md) |

> 示例 XML 统一放在仓库的
> [`XMLExamples/`](https://github.com/LDTchara/KernelExtensions/tree/main/XMLExamples) 目录。

!!! note "为什么本页不再罗列各系统的字段"
    各系统的配置字段（名称、默认值、取值格式）**只在它们的系统页维护**。
    本页过去复制过其中一部分，结果是代码更新后本页悄悄过期——现在改为索引，
    让「配置的权威说明」只有一处。

---

## 三、通用规则

- `file` 类属性中的路径一律**相对扩展根目录**。
- 音乐字段可用纯文件名、相对路径或 DLC 路径，由 `MusicPathResolver` 自动解析。
- 颜色字段的取值格式见[自定义动态色系统](./../systems/custom-color.md)
  （其中说明了不同字段所用解析链的差异）。

---

## 四、动作点写法速查

**"动作点"**指配置里"某个时机要执行什么动作"的字段（如 `OnStart` / `OnComplete` / `OnFail` / `OnSwitch`）。
独立配置文件系统（试炼 / 相位穿梭 / VM 攻击 / PortHackV2 / 自定义结局）的动作点**已统一**为同一套写法（三种形态，可混用）；
少数动作点是**父元素属性**，只能填路径。写错形态会**静默不执行**，动手前先查下表。

### 三种写法（独立配置文件系统通用）

| 写法 | 长什么样 | 说明 |
|------|----------|------|
| 元素文本路径 | `<OnComplete>Actions/x.xml</OnComplete>` | 值就是动作文件路径（最常用） |
| `file` 属性 | `<OnComplete file="Actions/x.xml" />` | 旧写法，继续兼容；与元素文本同时给出路径时**属性优先**并记 Warn |
| 内联动作 | `<OnComplete><Actions>…</Actions></OnComplete>` | `<Actions>` 立即执行；`<ConditionalActions>` 等条件满足后执行 |

- 路径与内联**可以同时写** → **先执行文件、再执行内联**
- `NONE` / 空 = 不执行（正常配置，不报错）
- 内联内容与动作文件走同一套装载逻辑，条件写法与原版一致；动作清单见[自定义 Action](./actions.md)

### 哪些动作点用哪种写法

| 系统 | 动作点 | 写法 |
|------|--------|------|
| PortHackV2 | 根级 `OnStart` / `OnComplete`；协议级 `OnStart` / `OnComplete` / `OnFail` | 三形态 |
| 自定义试炼 | 根级 `OnStart` / `OnAnimationComplete` / `OnGlobalFail` / `OnComplete`；阶段 `OnPhaseStart` / `OnComplete` / `OnFail` | 三形态 |
| 相位穿梭 | 场景的 `OnSwitch` | 三形态 |
| VM 攻击 | `ActionOnGuideTextStart` / `OnRecovered` / `OnRecoveredPostBoot` | 三形态 |
| 自定义结局 | `AfterAction` | 三形态 |
| 定时器（Clock） | `OnComplete`（父元素属性） | **仅路径** |
| 破碎之心（`CustomHeartDaemon` / `BreakHeart`） | `OnComplete` / `OnHeartbreak`（父元素属性） | **仅路径** |
| 飞机 Daemon（`FlightDaemon`） | `OnFailed` / `OnSaved`（父元素属性） | **仅路径** |
| 全屏警告（`StartScreenBleedEffectWCC`） | `CompleteAction`（父元素属性） | **仅路径** |

!!! note "为什么有的动作点只能填路径"
    **XML 属性在语法上无法承载子元素** —— 定时器 / 破碎之心 / 飞机 Daemon 的动作点是父元素的属性，
    因此只能填路径。Daemon 声明在节点 XML 里；`<OnComplete><Actions>…</Actions></OnComplete>` 这种内联约定
    **只适用于独立配置文件**，不要用在 Daemon 或原版 Action 上。

!!! warning "写错不会崩，但也不会生效"
    动作点里写了不存在的文件、越界路径、或无法识别的元素名，**只会记一条日志并跳过** ——
    不会让整个破解 / 场景 / 试炼中断。**所以"没报错"不等于"生效了"，出问题时先看日志。**

---

## 另请参阅

- [首页](./../index.md) – 返回主索引
- [Configuration Files (English)](./../../en/components/configuration.md) – 英文版
- [自定义动态色系统](./../systems/custom-color.md) – 颜色取值规则
- [自定义 Action](./actions.md) – 动作参数
