# Custom Title Banner (ShowTitle)

**ShowTitle** is a remake of the vanilla `IncomingConnectionOverlay` (the "incoming connection" overlay) — it pops a warning banner in the centre of the screen with a title and multi-line body, themed caution stripes on top and bottom, and an optional icon on the left. Useful for story alerts, system warnings and chapter transitions.

!!! info "Applies to"
    This page covers KernelExtensions **0.7**. Related action: `ShowTitle`.

---

## Overview

- Start: `<ShowTitle ...>body text</ShowTitle>`
- The body goes in the **element content** and can span multiple lines — exactly the same style as `StartScreenBleedEffectWCC`
- The accent colour follows the **game theme**: `info` → theme highlight base colour / `warning` → theme warning colour; can also be overridden with CustomColor
- Shows for **5 seconds** by default (configurable)
- Icon path is configurable (relative to the extension root)

---

## Basic usage

```xml
<ShowTitle title="WARNING" preset="warning" time="6" icon="Images/Warn.png">
Left the LDTchara VPN
Tracking begins in 60 seconds
Get back to the VPN ASAP
</ShowTitle>
```

The body sits between the opening and closing tags, and **newlines are line breaks**. Leading/trailing blank lines and the common indentation are removed automatically, so you can indent freely in the XML without affecting what is displayed.

---

## Parameters

| Attribute | Required | Default | Description |
|-----------|:--------:|---------|-------------|
| `title` | ❌ | empty | Banner title (⚠️ **ASCII only**, see below) |
| `preset` | ❌ | `info` | Accent preset: `info` = theme highlight base (`defaultHighlightColor`); `warning` = theme warning colour (`warningColor`) |
| `time` | ❌ | `5` | Seconds the banner stays on screen |
| `color` | ❌ | empty | CustomColor override for the accent (hex / preset / dynamic); `NONE` / empty = use the `preset` theme colour |
| `icon` | ❌ | `Images/Info.png` | Icon path (relative to the extension root); `NONE` / empty = default |
| `Delay` / `DelayHost` | ❌ | — | Inherited from the Pathfinder delayable action mechanism |

---

## Colour resolution

`color` uses the same chain as `StartScreenBleedEffectWCC`, first match wins:

1. **CustomColor dynamic** — `LDTchara:0.1`, `Rainbow`, preset names (`CustomColor/*.xml`) — refreshed every frame, never frozen
2. **Hex** — `#RRGGBB` or `#AARRGGBB`
3. **Numeric RGB/RGBA** — e.g. `255,0,0` / `255,0,0,128`
4. **Fallback** — the `preset` theme colour

!!! tip "Follows the theme by default"
    Without `color`, the accent comes from the **active game theme**: `info` uses `defaultHighlightColor` (the theme's highlight base — it is not polluted by the temporary warning flash), `warning` uses `warningColor` (the theme's warning colour). When the player switches themes, the banner follows automatically.

!!! warning "Named colours are currently unavailable"
    The colour parser does not include the XNA named-colour table (e.g. `Red`); named colours fall back to the default. Use hex or CustomColor presets instead.

---

## Icon

- Default is `Images/Info.png` (relative to the extension root)
- A missing or failing icon **never crashes**: the banner still shows and a single `KELog.Warn` is logged
- When different `ShowTitle` calls in the same extension use different icons, the icon is reloaded on demand

---

## Titles are ASCII only

!!! warning "The title supports ASCII characters only"
    The title uses the game's **title font** (`Kremlin`). The game ships **no localised version of it in any language** (Chinese, Japanese, Russian — none), so non-ASCII characters in the title render as `?`.

    **The body is not affected** — it uses the game's localised fonts and displays Chinese, Japanese, etc. normally, following the active game language.

    If a title contains non-ASCII characters, a `KELog.Warn` note is emitted when the action fires.

    Put any non-ASCII text in the **body** instead.

---

## See Also

- [Home](./../index.md) – back to the main index
- [自定义标题横幅（中文）](./../../zh/systems/title-banner.md) – Chinese version
- [Custom ScreenBleed Effect](./screen-bleed.md) – the same element-content style
- [Custom Actions](./../components/actions.md) – full list of custom actions
