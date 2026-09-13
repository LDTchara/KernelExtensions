# 自定义结局系统（StartEnding）

**自定义结局**由两部分组成：`StartEnding` Action 触发，`CustomEndingModule` 播放——后者继承原版 `EndingSequenceModule`，由 OS 原生驱动 `Update`/`Draw`，**不依赖任何 Event 或 Harmony 补丁**。

结局流程：**演讲阶段（Speech）→ 报幕阶段（Credits）→ 结尾提示行 → 完成**（断开连接、恢复控制、存档、可选执行后续 Action）。

!!! info "适用版本"
    本页对应 KernelExtensions **0.7**。相关 Action：`StartEnding`；相关 Daemon：`PorthackHeartDaemon`。

---

## 三步用法

### 1. 准备资源文件

把资源放进扩展目录（**可放任意子目录**，不必是 `Docs/`）：

```
ExtensionRoot/
├── Endings/
│   ├── Speech.txt          # 演讲文本
│   ├── CreditsData.txt     # 报幕名单
│   └── EndingSpeech.ogg    # 演讲语音（可选）
└── Endings/MyEnding.xml    # 结局配置
```

### 2. 写结局配置（`<EndingConfig>` 根元素）

```xml
<EndingConfig>
    <Title>THE END</Title>
    <EndingText>Thanks for playing</EndingText>
    <SpeechTime>-1</SpeechTime>
    <SpeechFile>Endings/EndingSpeech.ogg</SpeechFile>
    <SpeechTextFile>Endings/Speech.txt</SpeechTextFile>
    <CreditsFile>Endings/CreditsData.txt</CreditsFile>
    <AfterAction file="Actions/AfterCredits.xml" />
</EndingConfig>
```

### 3. 在剧情中触发

```xml
<StartEnding File="Endings/MyEnding.xml" />
```

`File` **必填**，相对**扩展根目录**，指向结局配置文件；路径**不限文件夹**。

---

## 配置字段

| 字段 | 默认值 | 说明 |
|------|--------|------|
| `Title` | `Hacknet` | 报幕阶段的大标题 |
| `EndingText` | `Thanks For Playing` | 报幕末尾的提示行 |
| `OnCreditMusic` | 空 → 原版 `Music\Bit(Ending)` | 报幕阶段播放的音乐 |
| `AfterMusic` | 空 → 原版 `Music\Bit(Ending)` | 结局完成、回到游戏后播放的音乐 |
| `AfterAction` | 空 = 不执行 | 报幕完成后加载执行的动作文件（写作 `<AfterAction file="..." />`，也兼容元素内容） |
| `SpeechTime` | `-1` | 演讲阶段计时方式，见下 |
| `SpeechFile` | `Docs/EndingSpeech.wav` | 演讲语音路径（`.wav` 或 `.ogg`，可选） |
| `SpeechTextFile` | `Docs/Speech.txt` | 演讲文本路径 |
| `CreditsFile` | `Docs/CreditsData.txt` | 报幕名单路径 |

**路径规则**：以上路径均**相对扩展根目录**，可放在任意子目录。写 `NONE` 或留空 = 用默认路径；不写该子元素 = 用默认值。

---

## SpeechTime 语义

| 取值 | 行为 |
|------|------|
| **负数 / 无效值 / 不写** | 有语音：**跟随音频时长**（播完自动进报幕）；无语音：静默 **30 秒**兜底（并 `KELog.Warn`） |
| `0` | **跳过演讲阶段**，直接进入报幕（不播语音、不显示演讲文本） |
| `N > 0` | 演讲**上限 N 秒**：音频先播完则提前进报幕；N 先到则截断语音 |

!!! note "无效值"
    `NaN`、`Infinity` 等无法作为时长的值，与负数同义（跟随音频），不会被当成"上限"。

---

## 资源文件格式

### 语音（`SpeechFile`）

- **`.wav`** —— 走 `SoundEffect` 播放
- **`.ogg`** —— NVorbis 解码为 PCM（**体积约为 wav 的 1/10**，推荐用于长语音）

两者都会在演讲阶段绘制**波形图**（自绘实现，不依赖原版反射）。

### 演讲文本（`SpeechTextFile`）

逐字显示在屏幕**左下角**，同时**最多显示最近 5 行**：最新一行不透明，越往上的旧行越透明（每行透明度 ×0.6），第 6 行起不再绘制。  
**换行由文件自身的换行符决定**（不做自动折行），请自行控制每行长度。支持行内控制符：

| 字符 | 含义 |
|------|------|
| `#` | 停顿 1 秒 |
| `%` | 停顿 0.5 秒 |

其余字符以 **0.05 秒/字**的速度逐字显示；控制符本身不显示。

### 报幕名单（`CreditsFile`）

每行一条，支持前缀：

| 前缀 | 效果 |
|------|------|
| `^` | 灰色小字 |
| `%` | 大标题行 |
| `$` | 小号灰字 |

无前缀 = 普通正文。

!!! warning "演讲阶段的音乐必须混入语音文件"
    演讲阶段**只播放一条音频轨**（`SpeechFile`），没有独立的背景音乐配置项。若希望演讲时同时有背景音乐或氛围音，**必须先用音频工具把音乐与语音混合导出成同一个 wav/ogg**，再作为 `SpeechFile` 提供。
    只想放音乐、不要人声时，同样把音乐本身作为 `SpeechFile` 即可（此时"语音"就是音乐）。
    报幕阶段的音乐则用 `OnCreditMusic` 单独配置。

---

## 结束时的行为

结局播放完毕（报幕走完）后会自动：

- 断开当前连接
- 解锁输入（终端 / 内存 / 网络地图 / 顶栏按钮）
- 恢复 `os.canRunContent`
- 保存游戏
- 切回音乐（`AfterMusic`，未配置则原版 `Music\Bit(Ending)`）
- 执行 `AfterAction`（若配置）

!!! note "职责边界"
    结局模块**只负责结局自身**，**不清理**任何剧情节点（例如 `porthackHeart`）。节点清理由对应系统的拥有者负责——如果你用 `PorthackHeartDaemon`，它会在碎心结束时自行处理该节点。

---

## 与 PorthackHeart Daemon 联动

两者**互相独立**，联动方式是在 PHD 的 `OnComplete` 指向的动作文件里调用结局：

```xml
<!-- Actions/HeartBroken.xml —— 由 PHD 的 OnComplete 引用 -->
<ConditionalActions>
    <Instantly>
        <StartEnding File="Endings/MyEnding.xml" />
    </Instantly>
</ConditionalActions>
```

执行顺序：碎心序列 → PHD 清理（断开、处理 heart 节点）→ `OnComplete` → 结局播放 → 结局收尾。

---

## 另请参阅

- [首页](./../index.md) – 返回主索引
- [Custom Ending System (English)](./../../en/systems/custom-ending.md) – 英文版
- [自定义 Daemon](./../components/daemons.md) – PorthackHeart Daemon 等守护进程
- [自定义 Action](./../components/actions.md) – 全部自定义 Action 列表
