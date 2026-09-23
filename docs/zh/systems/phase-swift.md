# 相位穿梭系统（PhaseSwift）

**PhaseSwift** 是 KernelExtensions 的场景切换系统：把同一张网络地图切出多个「相位」——每个相位（Scene）拥有**自己的主题、拓扑连接、可见节点与音乐**。玩家点一下 Shift 就能在表/里世界之间穿梭，而网络的形状随之改变。

典型用法：同一个节点在相位 0 是普通服务器，切到相位 1 后它的邻居全部换掉、主题变暗、音乐换成另一条轨。

!!! info "适用版本"
    本页对应 KernelExtensions **0.7**。相关 Action：`PhaseSwiftInit`、`PhaseSwiftScene`、`PhaseSwiftMusic`、`PhaseSwiftStop`、`PhaseSwiftFadeOut`、`BlockNode`、`UnblockNode`。

---

## 概览

- 配置：`PhaseSwift/<名称>.xml`（根元素 `<PhaseSwiftConfig>`）
- 启动：`<PhaseSwiftInit ConfigName="MyConfig" />`
- 切场景：`<PhaseSwiftScene TargetScene="1" />`
- 音乐组：`<PhaseSwiftMusic Phase="1" />`
- 退出：`<PhaseSwiftStop />`
- 也可作为可执行程序运行（`#PHASESWIFT#`），窗口内有「开始 / Shift」按钮

### 受控节点——先理解这一个概念

**凡是出现在任意场景的 `StartNodes`、`VisibleNodes`、`Topology`、`BlockedNodes` 里的节点，
都归 PhaseSwift 全权管理**（称为「受控节点」）。其余节点 PS 一概不碰。

PS 对受控节点做的事：

- **链接**：切场景时清除受控节点**之间**的链接，再按该场景的 `<Topology>` 重建
- **可见性**：切场景时隐藏全部受控节点，再显示该场景该显示的
- **连接拦截**：受控节点在不该出现的场景里，玩家连不上（断连提示）

受控节点与非受控节点之间的连接**不会被 PS 改动**——这条很重要，它决定了 PS 能与其它系统安全共存。

---

## 快速上手

### 1. 写配置

```
ExtensionRoot/
├── PhaseSwift/
│   └── MyConfig.xml
├── Actions/
│   ├── scene0_switch.xml
│   └── scene1_switch.xml
└── Music/
    ├── surface.ogg
    └── underworld.ogg
```

```xml
<PhaseSwiftConfig>
    <ProgramName>相位穿梭</ProgramName>
    <InitialScene>0</InitialScene>
    <MusicPhases>
        <Phase id="0">
            <Tracks>
                <Track>Music/surface.ogg</Track>
                <Track>Music/underworld.ogg</Track>
            </Tracks>
        </Phase>
    </MusicPhases>
    <Scenes>
        <Scene id="0">
            <Theme>HacknetBlue</Theme>
            <OnSwitch file="Actions/scene0_switch.xml" />
            <StartNodes>
                <Node id="A" />
            </StartNodes>
            <VisibleNodes>
                <Node id="B" />
            </VisibleNodes>
            <Topology>
                <Link from="A" to="C" />
                <Link from="C" to="D" />
            </Topology>
        </Scene>
        <Scene id="1">
            <Theme>Themes/VOID-ICE.xml</Theme>
            <StartNodes>
                <Node id="A" />
            </StartNodes>
            <Topology>
                <Link from="A" to="B" />
                <Link from="B" to="D" />
            </Topology>
        </Scene>
    </Scenes>
</PhaseSwiftConfig>
```

### 2. 在剧情里启动与切换

```xml
<!-- 启动：加载配置 + 进入 InitialScene -->
<PhaseSwiftInit ConfigName="MyConfig" />

<!-- 推进剧情后切到相位 1（可选覆盖主题与渐变时长） -->
<PhaseSwiftScene TargetScene="1" FadeDuration="2.0" />

<!-- 只换音乐组，不切场景 -->
<PhaseSwiftMusic Phase="1" />

<!-- 淡出所有音轨（剧情演出用，可随时切场景恢复） -->
<PhaseSwiftFadeOut Duration="2" />

<!-- 结束 PS -->
<PhaseSwiftStop FinishMode="full" TopologyMode="restore" />
```

完整示例见 [PhaseSwift_Example.xml](https://github.com/LDTchara/KernelExtensions/blob/main/XMLExamples/PhaseSwift_Example.xml)。

---

## 配置文件（`PhaseSwiftConfig`）

存放位置：`PhaseSwift/<名称>.xml`。所有路径相对**扩展根目录**。

| 配置项 | 默认 | 说明 |
|--------|------|------|
| `ProgramName` | `PhaseSwift` | 程序窗口标题 / Exe 的 `IdentifierName` |
| `BackgroundColor` | 空 | 程序窗口背景色（颜色名 / hex / CustomColor 预设） |
| `DefaultFadeDuration` | `1.5` | 音乐交叉淡化默认时长（秒） |
| `ThemeFlickerDuration` | `0.8` | 主题切换的闪烁时长（秒），建议与上者接近 |
| `InitialScene` | `0` | 启动时激活的场景索引 |
| `ChangeLayout` | `false` | 切场景是否连节点布局一起换（`true` 时等闪烁结束再应用拓扑） |
| `StartButtonText` | `开始` | 未启动时按钮文字 |
| `ShiftButtonText` | `Shift` | 运行中切场景按钮文字 |
| `ShowSceneNumber` | `true` | 是否显示 `X/X` 场景号 |
| `CompleteText` | 空 | 完成时显示的文字（不填回退内置词典） |
| `FinishMode` | `none` | 结束后的**节点可见性**：`none` 全隐藏 / `full` 全保留 / `scene_N` 只留场景 N |
| `TopologyMode` | `restore` | 结束后的**拓扑**：`restore` / `scene_N` / `merge`，详见下文 |
| `UseDualTrackMusic` | `true` | `true` = PS 自己用 DSEI 流式播放多轨；`false` = 走单曲模式 |
| `SingleTrack` | 空 | 单曲模式的音乐（`UseDualTrackMusic=false` 时有效），不填回退到第一个 Phase 的 Track |
| `RestoreThemeOnStop` | `true` | 停止时是否恢复启动前的主题 |
| `GlobalDiscovery` | `true` | 跨场景发现同步：在场景 A 发现的节点，切到 B 时若 B 的 `VisibleNodes` 也含它则保持可见 |
| `MusicPhases` | — | 音乐组列表，见下 |
| `Scenes` | — | 场景列表，见下 |

`FinishMode` 与 `TopologyMode` 都可以被 `PhaseSwiftStop` 的同名参数覆盖——**Action 参数优先**。

---

## 音乐组（`MusicPhases`）

每个 `<Phase>` 定义一组音轨，**索引 i 的 Track 对应 `Scenes` 里索引 i 的场景**：

```xml
<MusicPhases>
    <Phase id="0">
        <Tracks>
            <Track>Music/surface.ogg</Track>
            <Track>Music/underworld.ogg</Track>
        </Tracks>
    </Phase>
</MusicPhases>
```

- `<Phase id>` 就是 `PhaseSwiftMusic Phase=` 传入的值
- 条数应与场景数一致（1 个场景 1 条轨）
- 路径先按扩展根解析，找不到再回退到 `Music/<文件名>`
- **格式为 OGG**（NVorbis 流式解码 + `DynamicSoundEffectInstance`），切场景时交叉淡化
- 音量跟随游戏音乐音量设置

!!! warning "同一 Phase 内各轨长度必须一致"
    音乐按下标与场景绑定，**切换场景时是淡入淡出而非重新计时**。若同一 Phase 内各轨长度不同，
    多次切换后进度会逐渐错位。请让同组音轨等长。

PS 运行时会**停掉 `MusicManager` 的播放**，避免与原版音乐叠加；退出时归还。

---

## 场景（`Scenes`）

场景数量不限（2~4 个常见）。玩家点 Shift 按序号循环。

| 子元素 | 必填 | 说明 |
|--------|:----:|------|
| `<Theme>` | ❌ | 场景主题。留空 = 保持当前主题不变（包括上一个场景的 `OnSwitch` 所设）；可填内置名（`HacknetBlue` / `HacknetMint` / `HacknetOrange` / `HacknetRed`）或自定义主题路径 |
| `<OnSwitch file="...">` | ❌ | 切到本场景时执行的动作文件（`NONE` / 空 = 不执行）。**这是挂 LinkControl 等运行期逻辑的正规入口** |
| `<StartNodes>` | ❌ | 切到本场景时**默认可见**的节点。只有它们直接出现在网络地图上，其余靠扫描拓扑自然发现 |
| `<VisibleNodes>` | ❌ | 本场景**允许出现**的节点集合，配合 `GlobalDiscovery` 决定已发现的节点是否保留可见 |
| `<Topology>` | ❌ | 受控节点之间的链接，用 `<Link from="A" to="B" />` 声明（**有向**） |
| `<BlockedNodes>` | ❌ | 本场景的静态黑名单节点（`<Node>A</Node>` 形式），在场景里始终隐藏 |

### 拓扑是「替换」而不是「追加」

切场景时，PS 会先清除**受控节点之间**的所有链接，再按新场景的 `<Topology>` 添加。所以：

- 想让某条链接在某场景消失，**不写它就行**
- 受控节点与非受控节点之间的连接**始终保留**
- 同一对节点要有双向连接，就写两条 `<Link>`（`A→B` 与 `B→A`）

---

## 可见性、黑名单与连接拦截

切到某场景时，显示规则是：

```
(StartNodes) ∪ (本场景已发现的节点) ∪ (GlobalDiscovery 下：其他场景已发现、且本场景 VisibleNodes 含有的节点)
− (本场景的运行时黑名单)
```

被显示的节点会带一次高亮闪烁与扩散圆环。反之，受控节点若不在当前场景的可见集合里，玩家**无法连接**它（走断连提示）。

### 运行时黑名单

```xml
<!-- 把节点 X 加入当前场景的运行时黑名单 -->
<BlockNode NodeId="X" />

<!-- 指定场景（不填 SceneIndex 则用当前场景） -->
<BlockNode NodeId="X" SceneIndex="1" />

<!-- 移出黑名单 -->
<UnblockNode NodeId="X" />
```

与 `<BlockedNodes>`（静态、写在配置里）不同，运行时黑名单由剧情在运行中增删，**并随存档持久化**。

---

## 结束与拓扑处置（`PhaseSwiftStop`）

PS 结束时的行为分**两个正交维度**，各自可配：

**① 节点可见性（`FinishMode`）**

- `none` —— 全隐藏（默认）
- `full` —— 保留所有场景的起始节点与已发现节点
- `scene_N` —— 只保留场景 N 的节点可见

**② 拓扑（`TopologyMode`）**

- `restore` —— 恢复 PS 启动时备份的原始链接（默认）
- `scene_N` —— 恢复原始链接后，再叠加场景 N 的 `<Topology>`（等同于「运行期停在该场景」的状态）
- `merge` —— 清除受控节点之间的链接后，把**所有场景**的 `<Topology>` 合并叠加（按 from→to 单向去重）

!!! warning "`merge` 会连通后续场景的路径"
    merge 把每个场景的拓扑都叠在一起，**包括玩家还没经历过的后期场景**——比如场景 0 有 `A→B`、
    场景 2 有 `B→C`，merge 后玩家一停止就能走到 C。建议只在**剧情线已全部走完、要交还自由探索**时使用。

```xml
<!-- Action 参数优先于配置 -->
<PhaseSwiftStop FinishMode="full" TopologyMode="merge" />
```

无效或未知的模式值会**回退 `restore` 并写一条 `KELog.Warn`**，不会静默失败。

### 关于「原始链接」的时机

`restore` 恢复的是 `_originalLinks`，它有两个来源：

1. **新游戏** —— PS **初始化那一刻**对受控节点现有链接做快照（即内容 XML 里 `<dlink>` 的结果）
2. **读档** —— 用存档里记录的 `<OrigLink>` 覆盖，而不是重新拍一遍

因此语义是：**「原始」= PS 启动那一刻的状态**。这带来一个有用的推论：

> **在 `PhaseSwiftInit` 之前用 `<LinkControlAdd>` 改动受控节点，会被认作「原始状态」的一部分。**
> 这不是错误——想让改动成为初始状态就在 Init 前做，想让它只是临时的就放在 Init 之后。

---

## 与 LinkControl 配合

两者都会改 `Computer.links`，但各管一段：PS 只动**受控节点之间**的链接，LC 可以动任意链接。

**正规用法**：运行期的临时链接变化，挂在场景的 `OnSwitch` 里用 LC——此时 PS 刚重建完拓扑，LC 的微调叠在上面，时序天然正确。

```xml
<!-- Actions/scene1_switch.xml -->
<ConditionalActions>
    <Instantly>
        <!-- 进入相位 1 时临时打通一条剧情链路 -->
        <LinkControlAdd SourceComp="A" TargetComp="ghost" />
    </Instantly>
</ConditionalActions>
```

!!! warning "唯一要避免的组合"
    **PS 运行中使用 `<LinkControlReset>`**——它是整体赋值，会抹平当前场景的拓扑。
    想恢复拓扑请用**切场景**（PS 会重建）或 `PhaseSwiftStop`，而不是 LC Reset。

其余情况可安全共存。注意 LC 对**受控节点之间**的临时改动会在下次切场景时被 PS 清除——那是预期行为，不是 bug。

---

## 持久化

- **flag 驱动自动恢复**：`<PhaseSwiftInit>` 会写入 flag `PhaseSwift_<ConfigName>`；读档时检测到该 flag 就自动恢复 PS（场景、拓扑、可见性、已发现节点、音乐组、主题）
- `<PhaseSwiftStop>` 会移除该 flag（代表剧情正常结束）；而 exe 被杀死、扩展卸载等清理路径**不会**移除，所以「杀掉 exe 后读档剧情继续」仍然成立
- 存档里写入 `<PhaseSwiftData>`：

```xml
<PhaseSwiftData ConfigName="MyConfig" CurrentScene="1" MusicPhase="1" Theme="Themes/VOID-ICE.xml">
  <DiscoveredScene Index="0"><Node>B</Node></DiscoveredScene>
  <OrigLink NodeId="A" Targets="C,D" />
  <RuntimeBlockedScene Index="1"><Node>X</Node></RuntimeBlockedScene>
</PhaseSwiftData>
```

---

## Action 参考

### `PhaseSwiftInit`

加载配置并启动（等价于「初始化 + 进入 InitialScene」）。

| 属性 | 必填 | 说明 |
|------|:----:|------|
| `ConfigName` | ❌ | 配置文件名（不含路径与扩展名），默认 `Default`；文件位于 `PhaseSwift/<ConfigName>.xml` |

### `PhaseSwiftScene`

切换场景：触发音乐交叉淡化、拓扑替换、可见性更新、主题切换与场景的 `OnSwitch`。
**音乐组切换请用 `PhaseSwiftMusic`**（两者有意分开）。

| 属性 | 必填 | 说明 |
|------|:----:|------|
| `TargetScene` | ✅ | 目标场景索引（从 0 开始） |
| `FadeDuration` | ❌ | 音乐渐变时长（秒）；负数或省略 = 用配置默认 |
| `Theme` | ❌ | 覆盖本场景主题（预设名或自定义路径） |

### `PhaseSwiftMusic`

只切换音乐组，不切场景。

| 属性 | 必填 | 说明 |
|------|:----:|------|
| `Phase` | ✅ | 目标音乐组 id（对应 `MusicPhases` 里的 `id`） |

### `PhaseSwiftFadeOut`

把所有音轨淡出到静音（不释放播放实例，随时可切场景恢复）。

| 属性 | 必填 | 说明 |
|------|:----:|------|
| `Duration` | ❌ | 淡出时长（秒），默认 `1` |

### `PhaseSwiftStop`

停止 PS 并清理。参数见上文「结束与拓扑处置」。

| 属性 | 必填 | 说明 |
|------|:----:|------|
| `FinishMode` | ❌ | 节点可见性：`none` / `full` / `scene_N`；不填用配置 |
| `TopologyMode` | ❌ | 拓扑处置：`restore` / `scene_N` / `merge`；不填用配置 |

### `BlockNode` / `UnblockNode`

运行时黑名单的增删。

| 属性 | 必填 | 说明 |
|------|:----:|------|
| `NodeId` | ✅ | 目标节点 ID |
| `SceneIndex` | ❌ | 目标场景索引；不填或 `-1` = 当前场景 |

---

## 已知限制

- **单实例**：PS 是**全局单实例**——同一时刻只有一个配置在运行。再次执行 `<PhaseSwiftInit>`、
  或在另一个 PhaseSwift 程序窗口按开始，**不会**启动第二个实例，只会在日志里补一条 `Warn`。
  需要「另一套场景 / 拓扑」时，请把它做成**同一配置里的另一个场景**；
  只需要换音乐而不换场景，用 `<PhaseSwiftMusic>`。

- **运行期播放控制独占**：PS 会拦截 `MusicManager` 的播放入口，因此**原版与第三方都无法在此时发起
  新的音乐播放**。这是设计内的独占行为，不是缺陷。PS 停止后入口不再被拦截，播放控制交还。
- **音轨等长**：同一 Phase 内音轨长度不一致会导致切换后进度漂移（见上文 warning）。
- **主题路径**：自定义主题路径需相对扩展根目录书写（PS 不额外拼接前缀）。
- **音乐位置不持久化**：存档只记场景 / 音乐组 / 主题，**不记播放位置**——读档后音乐从头播放。

!!! note "与第三方模组共存"
    音频链与 Action 注册名的冲突面、处理机制与 `Compat/` 架构约定，统一见
    **[与第三方模组兼容](./../components/mod-compat.md)**（含「必须无条件 `MusicManager.stop()`」与
    「读档需要观察窗口」两处实机修正）。与 SASS 同用时需注意：KE 的 `PlaySound` 会退化为 `KEPlaySound`。

---

## 另请参阅

- [首页](./../index.md) – 返回主索引
- [Phase Swift System (English)](./../../en/systems/phase-swift.md) – 英文版
- [节点连接控制动作](./../components/actions.md) – `LinkControlAdd` / `Remove` / `Reset`
- [自定义 Action](./../components/actions.md) – 全部自定义 Action 列表
- [配置文件](./../components/configuration.md) – 各系统配置文件索引
