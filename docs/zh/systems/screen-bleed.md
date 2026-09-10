# 自定义全屏警告特效（ScreenBleed）

**ScreenBleed（WCC）** 替代原版 `StartScreenBleedEffect`，在保留原版全屏警告表现的同时，支持**自定义背景色、文字底色与警告标题**，并可在结束时执行后续 Action。

WCC = With Custom Color。该效果适合表现系统告警、紧急事件、剧情转折等全屏提示。

!!! info "适用版本"
    本页对应 KernelExtensions **0.7**。相关 Action：`StartScreenBleedEffectWCC`。

---

## 概览

- 启动：`<StartScreenBleedEffectWCC ...>正文文本</StartScreenBleedEffectWCC>`
- 颜色支持 CustomColor 动态色、`#RRGGBB` / `#AARRGGBB`、数值 RGB/RGBA
- 可配置总时长与标题；正文最多 3 行
- 结束时可选执行 `CompleteAction`
- 可用**原版** `CancelScreenBleedEffect` Action 提前中止（KE 会同步停止自身效果）

---

## 基本用法

```xml
<StartScreenBleedEffectWCC
    AlertTitle="WARNING"
    TotalDurationSeconds="5.0"
    BackgroundColor="#FF2020"
    TextBackgroundColor="#880000"
    CompleteAction="Actions/end.xml">
    Line 1 text
    Line 2 text
    Line 3 text
</StartScreenBleedEffectWCC>
```

元素体中的文本按行解析，**最多取 3 行**（不足自动补空行，多余行忽略）。

---

## 参数

| 属性 | 必填 | 默认值 | 说明 |
|------|:----:|--------|------|
| `AlertTitle` | ❌ | `EMERGENCY` | 顶部警告标题文字 |
| `TotalDurationSeconds` | ❌ | `200` | 效果持续总秒数 |
| `BackgroundColor` | ❌ | 深红（`120,0,0`） | 全屏背景色 |
| `TextBackgroundColor` | ❌ | 半透明暗红（`105,0,0,200`） | 文字底色 |
| `CompleteAction` | ❌ | 空（不执行） | 效果结束时加载执行的 ConditionalActions 文件；`NONE` / 空 = 不执行 |
| `Delay` | ❌ | `0` | 延迟执行的秒数；为 0 或省略时立即执行 |
| `DelayHost` | ❌ | — | 提供延迟服务的主机 ID（该主机需有 `FastActionHost` 守护进程） |

---

## 颜色取值

颜色属性按以下顺序解析，命中即用：

1. **CustomColor 动态色**：`LDTchara:0.1`、`Rainbow`、预设名（`CustomColor/*.xml`）——逐帧刷新
2. **十六进制**：`#RRGGBB` 或 `#AARRGGBB`（8 位时首位为 alpha）
3. **数值 RGB/RGBA**：如 `255,0,0` / `255,0,0,128`
4. **兜底**：无法识别时回退到默认色

!!! warning "命名色暂时不可用"
    当前运行时的颜色解析不包含 XNA 命名色表（如 `Red`），填写命名色会回退到默认色。请使用十六进制或 CustomColor 预设。

---

## 完成动作

效果走完 `TotalDurationSeconds` 后，若配置了 `CompleteAction`，会加载并执行该 ConditionalActions 文件：

```xml
<StartScreenBleedEffectWCC TotalDurationSeconds="3.0" CompleteAction="Actions/AfterAlert.xml">
    EMERGENCY SHUTDOWN
    System will reboot
    Please stand by
</StartScreenBleedEffectWCC>
```

---

## 中止效果

用**原版** Action 即可停止：

```xml
<CancelScreenBleedEffect />
```

KE 通过补丁同步停止自身效果（不会出现原版与自定义效果叠加）。

---

## 另请参阅

- [首页](./../index.md) – 返回主索引
- [Custom ScreenBleed Effect (English)](./../../en/systems/screen-bleed.md) – 英文版
- [自定义 Action](./../components/actions.md) – 全部自定义 Action 列表
