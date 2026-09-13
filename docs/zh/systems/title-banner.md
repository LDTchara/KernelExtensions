# 自定义标题横幅（ShowTitle）

**ShowTitle** 重制自原版 `IncomingConnectionOverlay`（"本机被外部连接"覆盖层）——在屏幕中央弹出一条警示横幅，显示标题与多行正文；上下带主题色斜条纹，左侧可配图标。适合剧情提示、系统告警、章节转场等场景。

!!! info "适用版本"
    本页对应 KernelExtensions **0.7**。相关 Action：`ShowTitle`。

---

## 概览

- 启动：`<ShowTitle ...>正文文本</ShowTitle>`
- 正文写在**元素内容**里，可直接多行——写法与 `StartScreenBleedEffectWCC` **完全一致**
- 强调色跟随**游戏主题**：`info` → 主题高亮基色 / `warning` → 主题警告色；也可用 CustomColor 覆盖
- 默认显示 **5 秒**，可配
- 图标路径可配（相对扩展根）

---

## 基本用法

```xml
<ShowTitle title="WARNING" preset="warning" time="6" icon="Images/Warn.png">
已离开 LDTchara VPN
预计 60 秒后将被追踪
请尽快回到 VPN
</ShowTitle>
```

正文写在开闭标签之间，**换行符即分行**。首尾空行与各行公共缩进会自动去除，因此可以自由排版（XML 缩进不会进入显示内容）。

---

## 参数

| 属性 | 必填 | 默认值 | 说明 |
|------|:----:|--------|------|
| `title` | ❌ | 空 | 横幅标题（⚠️ **仅支持 ASCII**，见下） |
| `preset` | ❌ | `info` | 强调色预设：`info` = 主题高亮基色（`defaultHighlightColor`）；`warning` = 主题警告色（`warningColor`） |
| `time` | ❌ | `5` | 横幅显示秒数 |
| `color` | ❌ | 空 | CustomColor 覆盖强调色（Hex / 预设 / 动态色）；`NONE` / 空 = 用 `preset` 的主题色 |
| `icon` | ❌ | `Images/Info.png` | 图标路径（相对扩展根）；`NONE` / 空 = 默认 |
| `Delay` / `DelayHost` | ❌ | — | 继承自 Pathfinder 延迟动作机制 |

---

## 颜色取值

`color` 与 `StartScreenBleedEffectWCC` 走同一条解析链，命中即用：

1. **CustomColor 动态色**：`LDTchara:0.1`、`Rainbow`、预设名（`CustomColor/*.xml`）——逐帧刷新，不定格
2. **十六进制**：`#RRGGBB` 或 `#AARRGGBB`
3. **数值 RGB/RGBA**：如 `255,0,0` / `255,0,0,128`
4. **兜底**：回退到 `preset` 主题色

!!! tip "默认跟随主题"
    不写 `color` 时，强调色取自**当前游戏主题**：`info` 用 `defaultHighlightColor`（主题高亮基色，不会被警告闪烁临时改色污染），`warning` 用 `warningColor`（主题警告色）。玩家切换主题时横幅配色自动跟随。

!!! warning "命名色暂时不可用"
    颜色解析不含 XNA 命名色表（如 `Red`），填写命名色会回退到默认色。请使用十六进制或 CustomColor 预设。

---

## 图标

- 默认 `Images/Info.png`（相对扩展根）
- 图标缺失或加载失败**不会崩溃**：横幅继续显示，仅记录一条 `KELog.Warn`
- 同一扩展内多次 `ShowTitle` 使用不同图标时，图标会按需重载

---

## 标题仅支持 ASCII

!!! warning "标题只支持 ASCII 字符"
    标题使用游戏的**标题字体**（`Kremlin`）。官方**没有为它提供任何语言的本地化版本**（中文、日文、俄文等一概如此），因此标题中的非 ASCII 字符会显示为 `?`。

    **正文不受此限制**——正文使用官方本地化字体，会随游戏语言正常显示中文、日文等。

    触发时若检测到标题含非 ASCII 字符，会在日志（`KELog.Warn`）中给出提示。

    需要显示非 ASCII 文本时，请把它放进**正文**。

---

## 另请参阅

- [首页](./../index.md) – 返回主索引
- [Custom Title Banner (English)](./../../en/systems/title-banner.md) – 英文版
- [自定义全屏警告特效（ScreenBleed）](./screen-bleed.md) – 同样的元素内容写法
- [自定义 Action](./../components/actions.md) – 全部自定义 Action 列表
