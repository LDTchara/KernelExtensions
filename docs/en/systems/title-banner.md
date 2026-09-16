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
<ShowTitle title="WARNING" preset="warning" duration="6" icon="default">
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
| `duration` | ❌ | `5` | Seconds the banner stays on screen |
| `color` | ❌ | empty | CustomColor override for the accent (preset / dynamic); `NONE` / empty = use the `preset` theme colour |
| `icon` | ❌ | empty | Icon: empty / `NONE` = **no icon**; `default` = built-in default icon; anything else = path relative to the extension root |
| `icontint` | ❌ | empty | Icon tinting: empty = **auto** (`default` tinted / custom not tinted); `true` / `false` = force; any other value = auto + warning |
| `Delay` / `DelayHost` | ❌ | — | Pathfinder delayable action; ⚠️ **attribute names are case-sensitive** (`Delay`, not `delay`) |

---

## Colour resolution

`color` uses the same chain as `StartScreenBleedEffectWCC`, first match wins:

1. **CustomColor dynamic** — `LDTchara:0.1`, `Rainbow`, preset names (`CustomColor/*.xml`) — refreshed every frame, never frozen
2. **Hex** — `#RRGGBB` or `#AARRGGBB`
3. **Numeric RGB/RGBA** — e.g. `255,0,0` / `255,0,0,128`
4. **Fallback** — the `preset` theme colour

!!! tip "Follows the theme by default"
    Without `color`, the accent comes from the **active game theme**: `info` uses `defaultHighlightColor` (the theme's highlight base — it is not polluted by the temporary warning flash), `warning` uses `warningColor` (the theme's warning colour). When the player switches themes, the banner follows automatically.

!!! warning "HEX colours are currently unavailable (known issue)"
    The colour parser has **no Hex support** (`#RRGGBB` silently falls back to the `preset` theme colour) and no XNA named-colour table (e.g. `Red`). Both will be added by the colour-parsing unification (9.50). For now use **CustomColor presets or dynamic colours**.

---

## Icon

`icon` and `icontint` together decide how the icon appears:

| `icon` | `icontint` | Result |
|---|---|---|
| omitted / empty / `NONE` | — | **No icon** |
| `default` | omitted | Built-in icon, **tinted** (follows the accent colour) |
| `default` | `false` | Built-in icon, original colours |
| valid path | omitted | That icon, **original colours** (no tint) |
| valid path | `true` | That icon, **tinted** |
| invalid path / load failure | any | **Falls back to the built-in icon** (tinted) + `KELog.Warn` |

- The built-in icon comes from the mod's embedded resources and is **never written into your extension folder**
- Any other `icontint` value (e.g. `yes`) falls back to **auto** and logs a `KELog.Warn`

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
