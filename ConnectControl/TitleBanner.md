# 自定义标题横幅（ShowTitle / TitleBanner）使用指南

## 概述

KE 的标题横幅系统（dev1 标题系统合并版）由两部分组成：

- **`Actions/Title/ShowTitle.cs`**：Pathfinder Action 入口，弹出横幅；
- **`Actions/Title/TitleBanner.cs`**：横幅渲染实现 + Harmony 钩子（`TitleBannerHooks`）——`OS.LoadContent` Postfix 初始化单例并加载资源，`OS.Update`/`OS.Draw` Postfix 每帧驱动。

横幅样式：屏幕中央黑色横条 + 上下**警告条纹** + 居中标题 + 正文（自动换行）+ 图标，带淡入（0.2s）/淡出（0.5s）动画，触发时播放音效。

## XML 用法

```xml
<ShowTitle title="!!!ATTENTION!!!"
           body="Intrusion detected. Evacuate immediately.\nShutting down."
           time="5"
           type="warning"
           color="#FF9900"
           icon="Images/Info.png"
           iconbg="Images/InfoBG.png"/>
```

## 参数

| 参数 | 类型 | 默认值 | 说明 |
|---|---|---|---|
| `title` | string | 空 | 标题文字（居中，`titlefont` 大字） |
| `body` | string | 空 | 正文（自动换行；支持 `\n` 换行，XML 中写 ` \n ` 会被转换为换行） |
| `time` | float | `5` | 显示时长（秒） |
| `type` | string | `info` | 预设类型：`info`（蓝）/ `warning`（黄），二选一 |
| `color` | string | 空=type 默认色 | 强调色覆盖（详见下方"自定义 icon / iconbg / color"） |
| `icon` | string | `Images/Info.png` | 图标路径（默认即可，一般无需修改） |
| `iconbg` | string | `Images/InfoBG.png` | 图标背景路径（默认即可，一般无需修改） |

## 自定义 icon / iconbg / color

- **`type` 预设**：`info` = 蓝（100,180,255），`warning` = 黄（255,210,0），直接二选一即可；
- **`icon` / `iconbg`**：默认 `Images/Info.png` / `Images/InfoBG.png`，扩展会自动加载，**默认即可，一般无需修改**。要改的话路径相对扩展根（`Texture2D.FromStream` 加载），图标缺失**不崩溃**（Warn 提示，横幅仍显示但无图标）；
- **`color`**：强调色覆盖，支持 CustomColor——Hex（`#FF9900`）、颜色名、CC 预设、**动态关键字**（彩虹/渐变类）；`NONE`/空 = 用 type 默认色；
- **动态色每帧刷新**：横幅 `Draw` 时经 `CustomColorManager.GetDynamicColor` 按当前时间重新取色——动态关键字会持续变化，不会在触发时定格（2026-08-25 修复）；
- 条纹、标题、图标 tint 全部使用强调色；
- **音效**：触发时播放 `SFX/DoomShock` + `SFX/BrightFlash`（原版内置音效，无需配置）。

## 渲染细节

- 黑条高度 230px，随淡入淡出从 130px 缩放；
- 上下条纹用 `PatternDrawer.warningStripe` 绘制；
- 正文用 `Utils.SuperSmartTwimForWidth` 按宽度换行，逐行居中；
- 图标固定 100px，位于标题左侧，带 `AccentColor → White` 渐变 tint；
- 时序：淡入 0.2s → 保持 → 最后 0.5s 淡出 → 隐藏。

## 触发方式

作为 Pathfinder Action，用于 **ConditionalActions / Mission XML**：

```xml
<ShowTitle title="WARNING" body="Firewall activated" time="4" type="warning"/>

<ShowTitle title="NOTICE" body="Upload complete" time="3" type="info" color="rainbow"/>
```

## 注意事项

1. **`body` 换行**：XML 里写 ` \n `（空格 + `\n` + 空格）才会被转换为真正的换行（代码 `body.Replace(" \\n ", "\n")`），直接写 `\n` 不生效；
2. **`NONE` 约定**：`color`/`icon`/`iconbg` 填 `NONE`（或空）即用默认值；
3. **SpriteBatch 安全**：横幅在 `OS.Draw` Postfix 中自行 `Begin/End`，即使绘制异常也会强制 `End`（避免下一帧 "Begin before End" 崩溃，2026-08-25 修复）；持续失败只 Warn 一次；
4. **初始化时机**：`OS.LoadContent` 时创建单例并加载资源，在此之前调用 `Show` 会被忽略（`Instance == null` 直接返回）。

## 示例（一次显示多条）

```xml
<ShowTitle title="!!!ATTENTION!!!" body="Unauthorized access detected\nDisconnect immediately" time="6" type="warning" color="#FF2200"/>
<ShowTitle title="NOTICE" body="File encrypted" time="3" type="info"/>
```
