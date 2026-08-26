# 自定义结局（CustomEnding / StartEnding）使用指南

## 概述

KE 的自定义结局系统由两部分组成：

- **`Actions/StartEnding.cs`**：Pathfinder Action 入口，触发自定义结局序列；
- **`Modules/CustomEndingModule.cs`**：结局 Module，继承原版 `EndingSequenceModule`，由 OS 原生驱动 `Update/Draw`（设置 `os.endingSequence` + `os.canRunContent = false`，**无需任何 Event 或 HarmonyPatch**）。

结局流程：**演讲阶段（Speech）→ 报幕阶段（Credits）→ 结尾提示行 → 完成**（保存游戏、恢复控制、可选执行后续 Action）。

## XML 用法

```xml
<StartEnding SpeechTime="30"
             Title="Hacknet"
             EndingText="Thanks For Playing"
             OnCreditMusic=""
             AfterMusic=""
             AfterAction="Actions/AfterCredits.xml"
             SpeechFile="Docs/EndingSpeech.wav"
             TextFile="Docs/Speech.txt"
             CreditsFile="Docs/CreditsData.txt"/>
```

## 参数

| 参数 | 类型 | 默认值 | 说明 |
|---|---|---|---|
| `SpeechTime` | float | `30` | 无 WAV 时演讲阶段的静默计时秒数 |
| `Title` | string | `Hacknet` | 报幕阶段的大标题文字 |
| `EndingText` | string | `Thanks For Playing` | 报幕末尾的提示行文字（`> xxx_` 带闪烁光标） |
| `OnCreditMusic` | string | 空=原版 `Music\Bit(Ending)` | 报幕阶段播放的音乐 |
| `AfterMusic` | string | 空=原版 `Music\Bit(Ending)` | 结局完成后（回到游戏）播放的音乐 |
| `AfterAction` | string | 空=不执行 | 报幕完成后加载执行的 ConditionalActions XML（`NONE`/空 = 不执行） |
| `SpeechFile` | string | `Docs/EndingSpeech.wav` | 语音 WAV 路径（相对扩展根；`NONE`/空 = 默认路径） |
| `TextFile` | string | `Docs/Speech.txt` | 演讲文本路径（相对扩展根；`NONE`/空 = 默认路径） |
| `CreditsFile` | string | `Docs/CreditsData.txt` | 报幕名单路径（相对扩展根；`NONE`/空 = 默认路径） |

> `NONE` 约定：资源字段填 `NONE`（或空串）即使用默认路径。

## 资源文件格式

三个资源文件放在**扩展根目录**下（路径可配，默认 `Docs/`）：

### 1. `EndingSpeech.wav`（可选）

- 有该文件：演讲阶段按**音频时长**推进（播放完自动进入报幕），并渲染**波形可视化**（通过反射调用 `WaveformRenderer`）；
- 缺失/加载失败：进入静默模式，按 `SpeechTime` 秒计时后进入报幕。

### 2. `Speech.txt`（演讲文本）

逐字显示在屏幕底部（最多 5 行，旧行渐隐）。控制字符：

| 字符 | 含义 |
|---|---|
| `#` | 停顿 1 秒 |
| `%` | 停顿 0.5 秒 |
| 普通字符 | 每字间隔 0.05 秒 |

显示时 `#`/`%` 会被过滤（不显示）。

### 3. `CreditsData.txt`（报幕名单）

每行一个条目，按行滚动显示。行首前缀控制样式：

| 前缀 | 样式 |
|---|---|
| （无） | 白色正常字号 |
| `^` | 灰色小字（`Color.Gray * 0.6f`，正常字号） |
| `%` | 大标题字号（`titlefont`，行高 90） |
| `$` | 灰色小号字（`smallfont`） |

## 结局流程细节

1. **启动**：`StartEnding.Trigger` 构造 `CustomEndingModule`、赋值资源路径与回调，设置 `os.endingSequence`，调用 `StartEnding(SpeechTime)`（内部设 `os.canRunContent = false`，OS 接管 Update/Draw）；
2. **演讲阶段**：播放语音 + 波形 + 底部逐字文本；结束后 `RollCredits()`；
3. **报幕阶段**：Hacknet 标题闪烁文字（约 1.71s 后出现，红色背景闪烁）→ 名单滚动（前 10 秒冻结，之后加速滚动）→ 结尾提示行 `> EndingText_`；
4. **完成**：提示行到达屏幕中央后暂停 5 秒 → `CompleteAndReturnToMenu()`：恢复输入锁、`canRunContent = true`、保存游戏（`threadedSaveExecute`）、恢复 `MediaPlayer.IsRepeating`、播放 `AfterMusic`、执行 `OnCompleteCallback`（即 `AfterAction`）。

## 注意事项

1. **资源路径相对扩展根**：通过 `ExtensionLoader.ActiveExtensionInfo.GetFullFolderPath()` 拼接；
2. **波形与扫描线**：`WaveformRenderer` 和 `OS.drawScanlines` 均通过**反射**调用，失败只是不渲染，不崩溃；
3. **`AfterAction` 用 `RunnableConditionalActions.LoadIntoOS` 加载**，失败仅 Warn；
4. 完成时会处理 `porthackHeart`（隐藏节点、清空 daemons、换 IP），与"胜利"状态联动；
5. 触发即锁定：演讲/报幕期间玩家输入被锁（`canRunContent = false`），完成后自动恢复。

## 示例（Mission / ConditionalActions 中使用）

```xml
<!-- 触发结局：无语音时 40 秒演讲，报幕后回到菜单并播放指定音乐 -->
<StartEnding SpeechTime="40"
             Title="THE END"
             EndingText="Thanks for playing"
             OnCreditMusic="Music/Bit(Ending)"
             AfterAction="Actions/AfterCredits.xml"/>
```
