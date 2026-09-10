# Custom ScreenBleed Effect

**ScreenBleed (WCC)** replaces vanilla `StartScreenBleedEffect` while keeping the vanilla full-screen alert presentation, adding **custom background colour, text background colour and alert title**, plus an optional follow-up Action when the effect finishes.

WCC = With Custom Color. It suits system alerts, emergency events and story turning points.

!!! info "Applies to"
    This page covers KernelExtensions **0.7**. Related action: `StartScreenBleedEffectWCC`.

---

## Overview

- Start: `<StartScreenBleedEffectWCC ...>body text</StartScreenBleedEffectWCC>`
- Colours accept CustomColor dynamic colours, `#RRGGBB` / `#AARRGGBB`, and numeric RGB/RGBA
- Configurable total duration and title; body text is up to 3 lines
- Optional `CompleteAction` executed when the effect ends
- Can be aborted early with the **vanilla** `CancelScreenBleedEffect` action (KE stops its own effect in sync)

---

## Basic Usage

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

The element body is parsed line by line — **up to 3 lines are used** (missing lines are padded with blanks, extra lines are ignored).

---

## Parameters

| Attribute | Required | Default | Description |
|-----------|:--------:|---------|-------------|
| `AlertTitle` | ❌ | `EMERGENCY` | Alert title text at the top |
| `TotalDurationSeconds` | ❌ | `200` | Total duration in seconds |
| `BackgroundColor` | ❌ | dark red (`120,0,0`) | Full-screen background colour |
| `TextBackgroundColor` | ❌ | translucent dark red (`105,0,0,200`) | Text background colour |
| `CompleteAction` | ❌ | empty (not executed) | ConditionalActions file loaded when the effect ends; `NONE` / empty = not executed |
| `Delay` | ❌ | `0` | Seconds to wait before executing; `0` or omitted executes immediately |
| `DelayHost` | ❌ | — | Host node ID for the delay service (the host needs the `FastActionHost` daemon) |

---

## Colour Values

Colour attributes are resolved in this order, first match wins:

1. **CustomColor dynamic colours**: `LDTchara:0.1`, `Rainbow`, preset names (`CustomColor/*.xml`) — refreshed every frame
2. **Hexadecimal**: `#RRGGBB` or `#AARRGGBB` (with 8 digits the leading byte is alpha)
3. **Numeric RGB/RGBA**: e.g. `255,0,0` / `255,0,0,128`
4. **Fallback**: unrecognised values fall back to the default colour

!!! warning "Named colours are currently unavailable"
    The current runtime colour resolution has no XNA named-colour table (e.g. `Red`), so named colours fall back to the default colour. Use hexadecimal or a CustomColor preset instead.

---

## Complete Action

When `TotalDurationSeconds` elapses, the configured `CompleteAction` (a ConditionalActions file) is loaded and executed:

```xml
<StartScreenBleedEffectWCC TotalDurationSeconds="3.0" CompleteAction="Actions/AfterAlert.xml">
    EMERGENCY SHUTDOWN
    System will reboot
    Please stand by
</StartScreenBleedEffectWCC>
```

---

## Aborting

Use the **vanilla** action to stop it:

```xml
<CancelScreenBleedEffect />
```

KE patches this so its own effect stops in sync (no stacking of vanilla and custom effects).

---

## See Also

- [Home](./../index.md) – back to the main index
- [自定义全屏警告特效（中文）](./../../zh/systems/screen-bleed.md) – Chinese version
- [Custom Actions](./../components/actions.md) – full list of custom actions
