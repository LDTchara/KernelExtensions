# Custom Color System (CustomColor)

**CustomColor** lets a colour field hold more than a static value: write a **keyword** instead and you get
a colour refreshed every frame (rainbow, looping gradient). It is a shared **colour engine** used by several
systems — Phase Swift, Custom Trial, the title banner, the full-screen alert, `FlashScreen` and the effects
player can all reference it directly.

!!! info "Applies to"
    This page corresponds to KernelExtensions **0.7**.

---

## 1. Three Ways to Write It

| Form | Example | Notes |
|------|---------|-------|
| Rainbow | `LDTchara` / `Rainbow` | Built in — **needs no preset file** |
| Preset reference | `Riptide` | References `CustomColor/Riptide.xml` (recommended) |
| Legacy two-colour gradient | `Gradient:#FF0000:#00FF00:2.0` | Kept for backwards compatibility |

### Rainbow parameters

Syntax: `LDTchara:speed:opacity:saturation:brightness` (`Rainbow` is an alias with identical syntax)

| Form | Meaning |
|------|---------|
| `LDTchara` | Default rainbow (speed `0.1`) |
| `LDTchara:0.05` | Custom speed |
| `LDTchara:0.1:0.8` | Speed + 80% opacity |
| `LDTchara:0.1:1.0:0.8:1.0` | Speed + opacity + saturation + brightness |

### Preset reference parameters

Syntax: `presetName:speedMultiplier:opacity` (at most two parameters)

| Form | Meaning |
|------|---------|
| `Riptide` | The preset's own speed |
| `Riptide:0.5` | Speed ×0.5 |
| `Riptide:0.5:0.8` | Speed ×0.5 + 80% opacity |

---

## 2. Preset Files

Preset files live in the **`CustomColor/`** folder at the extension root. They are **scanned once and
cached when the extension loads**.

```
your-extension/
├── CustomColor/
│   ├── Riptide.xml
│   └── Monochrome.xml
└── ...
```

File format:

```xml
<ColorPreset>
  <Name>Riptide</Name>

  <CustomColor id="0">
    <Color>#FF6B6B</Color>
    <Duration>2.0</Duration>
    <Transition>1.0</Transition>
  </CustomColor>

  <CustomColor id="1">
    <Color>#4ECDC4</Color>
    <Duration>2.0</Duration>
    <Transition>1.0</Transition>
  </CustomColor>
</ColorPreset>
```

| Element | Meaning |
|---------|---------|
| `<Name>` | Preset name, **case-sensitive** — must match exactly how you reference it |
| `<CustomColor id="N">` | One colour segment; played in `id` order |
| `<Color>` | The segment's colour (formats below) |
| `<Duration>` | How long to hold this colour (seconds) |
| `<Transition>` | Time to fade into the next segment (seconds); default `0` (instant) |

**Rules**

- At least **2 segments** are needed to form a loop
- Total cycle time = the sum of every segment's `Duration + Transition`
- Four colour formats are supported and **may be mixed**:
  `#FF0000` (hex RGB), `#88FF0000` (hex ARGB, leading byte is alpha),
  `255,0,0` (numeric RGB), `255,0,0,128` (numeric RGBA)

### Common recipes

| Want | How to write it |
|------|-----------------|
| Three-colour loop | 3 segments, each `Duration 1.0` + `Transition 1.0` |
| Hard switch (no fade) | `Transition` set to `0` |
| Breathing (fade in/out) | 2 segments, each `Duration 0` + `Transition 2.0`, with alpha on the colours |
| Fully static | Don't reference a preset — just write a plain colour value |

---

## 3. Where It Applies

| Location | Typical field |
|----------|---------------|
| Custom theme XML | `<defaultHighlightColor>Rainbow</defaultHighlightColor>` |
| [Phase Swift System](./phase-swift.md) | Program window `BackgroundColor` |
| [Custom Trial System](./custom-trial.md) | `BackgroundColor` / `GlobalTimerColor` / `PhaseTimerColor` / `SpinUpColor` |
| [Title banner (ShowTitle)](./../components/actions.md) | `AccentColor` |
| [ScreenBleed](./../components/actions.md) | `BackgroundColor` / `TextBackgroundColor` |
| `FlashScreen` | `Color` |

Any colour field that **goes through CustomColor parsing** accepts all three forms above.
Some colour fields instead use a **different parsing chain** (fixed formats only) — see section 4.

!!! warning "Scope limits"
    - Theme XML is scanned **only while a custom theme (`OSTheme.Custom`) is active**
    - Only **the OS's own colour fields** are covered; non-OS fields (IRC colours, board colours, …)
      are **not supported**
    - Preset files are scanned once at load; presets added while the game runs need a reload

??? note "Aliased theme fields"
    These pairs move together — setting one makes the other follow the same dynamic colour:

    | Field A | Field B |
    |---------|---------|
    | `defaultHighlightColor` | `highlightColor` |
    | `lockedColor` | `brightLockedColor` |
    | `unlockedColor` | `brightUnlockedColor` |
    | `defaultTopBarColor` | `topBarColor` |
    | `moduleColorSolidDefault` | `moduleColorSolid` |

    If both fields are explicitly set to dynamic colours, each behaves independently.

---

## 4. Versus the Fixed-Format Parsing Chain

Referencing a preset or keyword is only one route. A plain colour string **without** a dynamic keyword goes
through a different chain, and the two do **not** accept exactly the same formats:

| Format | `<Color>` inside a CustomColor preset | Plain colour-field string parsing |
|--------|:---:|:---:|
| Hex `#RRGGBB` / `#AARRGGBB` | ✅ | ⚠️ field-dependent |
| Numeric `R,G,B` / `R,G,B,A` | ✅ | ✅ |
| XNA named colour (`Red`) | — | ⚠️ field-dependent |
| Dynamic colour keywords | — | ✅ (this system) |

<!-- ke:9.50 -->
⚠️ Today the **plain string parsing is not unified across fields**: some fields (such as the banner's
`AccentColor`) do not yet accept hex or XNA named colours and silently fall back to a default.
A **colour-parsing unification** is planned, after which the table above collapses into one.
Until then, prefer **dynamic keywords or presets** when the exact colour matters.

---

## See Also

- [Home](./../index.md) – back to the main index
- [自定义动态色系统（中文）](./../../zh/systems/custom-color.md) – Chinese version
- [Title banner & ScreenBleed](./../components/actions.md) – actions using `AccentColor` / `BackgroundColor`
- [Phase Swift System](./phase-swift.md) – scene and program-window colouring
- [Patches & Harmony](./../components/harmony.md) – `CustomColorPatch`
