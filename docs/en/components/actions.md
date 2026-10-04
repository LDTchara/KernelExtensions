# Custom Actions

KernelExtensions registers **31** custom Actions that can be invoked in any action file. This page has
two parts:

- **1. System-related Actions** — a quick reference with pointers; full parameters live on each system page
- **2. General Actions (not covered by a system)** — full parameters and examples

All paths are relative to the extension root.

---

## 1. System-Related Actions

| System | Actions | Purpose | Details |
|--------|---------|---------|---------|
| Custom Trial | `FailTrial` · `RestoreCustomTrialNodes` | Force a trial to fail · restore destroyed nodes | [Custom Trial System](./../systems/custom-trial.md) |
| Phase Swift | `PhaseSwiftInit` · `PhaseSwiftScene` · `PhaseSwiftMusic` · `PhaseSwiftStop` · `PhaseSwiftFadeOut` · `BlockNode` · `UnblockNode` | Start/stop, scene switching, music-phase switching, runtime blocklist nodes | [Phase Swift System](./../systems/phase-swift.md) |
| VM Attack | `LaunchVMAttack` | Trigger a VM attack | [VM Attack System](./../systems/vm-attack.md) |
| Aircraft Daemon | `AttackAircraft` · `UploadAircraftSysFile` · `ShowAircraftOverlay` · `HideAircraftOverlay` | Attack · repair · altimeter overlay | [Aircraft Daemon System](./../systems/aircraft.md) |
| Node Link Control | `LinkControlReset` · `LinkControlAdd` · `LinkControlRemove` | Runtime link add/remove, restore org baseline | see the section below |
| Timer | `ClockStart` · `ClockStop` | Start/stop a custom timer | [Custom Timer System (Clock)](./../systems/clock.md) |
| Node Icons | `SetNodeIcon` | Set a node's icon | [Custom Node Icon System](./../systems/node-icon.md) |
| Custom Ending | `StartEnding` · `BreakHeart` | Trigger an ending / the heartbreak sequence | [Custom Ending System](./../systems/custom-ending.md) |

### Node Link Control (attribute quick reference)

All three share the **org baseline**: a snapshot of the computer's links taken **at game start**
(content XML `<dlink>` enters the baseline this way), persisted to the save as `<OrgLinks>`.
`Add` / `Remove` only change runtime links and can be undone with `Reset`.

| Action | Example |
|--------|---------|
| `LinkControlReset` | `<LinkControlReset SourceComp="playerComp" />` |
| `LinkControlAdd` | `<LinkControlAdd SourceComp="playerComp" TargetComp="jmail" />` |
| `LinkControlRemove` | `<LinkControlRemove SourceComp="playerComp" TargetComp="jmail" />` |

- Attribute names are **case-insensitive** (handled by the `KEAction` base class; PascalCase is still recommended)
- Missing nodes or a missing `TargetComp` are logged as errors and skipped — never a crash
- `<OrgLinks>` only ever appears in saves; to declare initial links in content XML, use vanilla `<dlink>`

---

## 2. General Actions (Not Covered by a System)

### Terminal and Nodes

| Action | Description | Example |
|--------|-------------|---------|
| `TerminalWrite` | Outputs a line of text to the terminal. | `<TerminalWrite Text="Hello, World!" />` |
| `TerminalType` | Types text character-by-character — **no automatic newline**; appends at the current cursor (like HackerScript's `write`, so several calls can share one line). | `<TerminalType Text="A message typed out" CharDelay="0.04" />` |
| `TerminalFocus` | Plays a terminal focus effect (full-screen darken + expanding border). | `<TerminalFocus Duration="5.0" BorderDuration="2.0" FadeInDuration="0.5" />` |
| `RenameNode` | Renames a node by its ID; takes effect immediately and persists in saves. | `<RenameNode NodeID="dhs" NewName="Secret Base" />` |

### Visuals and Sound

| Action | Description | Example |
|--------|-------------|---------|
| `PlaySound` | Plays a WAV sound effect from the extension directory. | `<PlaySound Path="Sounds/beep.wav" Volume="1" Pitch="0" Delay="1.5" DelayHost="cheat"/>` |
| `FlashScreen` | UI flash: pulses the interface with a colour and fades back to the current theme default. | `<FlashScreen Color="Red" Duration="2.0" />` |
| `SwitchToThemeKeepLayout` | Switches theme while **keeping the panel layout** (colour only). | `<SwitchToThemeKeepLayout ThemePathOrName="HacknetMint" FlickerInDuration="1.5" />` |

- `FlashScreen` accepts dynamic colour keywords (e.g. `LDTchara`) in `Color`; `Duration` defaults to `2.0`
  (non-positive = restore defaults immediately); `PlaySound="true"` plays the warning beep alongside the
  flash. Re-triggering **refreshes** rather than stacking.
- `SwitchToThemeKeepLayout` changes colours only; use vanilla `SASwitchToTheme` if you need the layout changed too.

### Title Banner `ShowTitle`

Remade from vanilla `IncomingConnectionOverlay` (the “machine is being connected to” overlay): a warning
banner appears in the centre of the screen with a title and multi-line body, themed diagonal stripes above
and below, and an optional icon on the left. Good for story beats, system alerts and chapter transitions.

```xml
<ShowTitle Title="WARNING" Preset="warning" Duration="6" Icon="default">
Left the LDTchara VPN
Tracking in about 60 seconds
Get back to the VPN now
</ShowTitle>
```

The body goes between the tags and **each newline is a line break**. Leading/trailing blank lines and the
common indentation of all lines are stripped, so you can lay the XML out freely (indentation never reaches
the display).

| Attribute | Required | Default | Description |
|-----------|:--------:|---------|-------------|
| `Title` | ❌ | empty | Banner title (⚠️ **ASCII only**, see below) |
| `Preset` | ❌ | `info` | Accent preset: `info` = theme highlight base (`defaultHighlightColor`); `warning` = theme warning colour (`warningColor`) |
| `Duration` | ❌ | `5` | Seconds the banner stays on screen |
| `AccentColor` | ❌ | empty | CustomColor override for the accent; `NONE` / empty = use the `Preset` theme colour |
| `Icon` | ❌ | empty | empty / `NONE` = **no icon**; `default` = built-in icon; anything else = path relative to the extension root |
| `IconTint` | ❌ | empty | empty = **auto** (built-in tinted / custom keeps its colours); `true` / `false` = force; other values = auto + warning |

When `AccentColor` is omitted the accent comes from the **current game theme**, so banner colours follow
the player's theme. Full colour-parsing rules are in the
[Custom Color System](./../systems/custom-color.md).

<!-- ke:9.50 -->
!!! warning "Hex and named colours are not available yet (known issue)"
    `AccentColor` currently **does not parse Hex** (`#RRGGBB` silently falls back to the `Preset` theme
    colour), nor XNA named colours (e.g. `Red`). Both will be added by the **colour-parsing unification**;
    until then use a **CustomColor preset or dynamic colour**.

**Icon rules**:

| `Icon` | `IconTint` | Result |
|---|---|---|
| omitted / empty / `NONE` | — | **no icon** |
| `default` | omitted | built-in icon, **tinted** (follows the accent) |
| `default` | `false` | built-in icon, original colours |
| valid path | omitted | that icon, **original colours** |
| valid path | `true` | that icon, **tinted** |
| invalid path / load failure | any | **falls back to the built-in icon** (tinted) + `KELog.Warn` |

- The built-in icon comes from the mod's embedded resources and is **never written into your extension folder**
- Any other `IconTint` value (e.g. `yes`) is treated as **auto** and logs a `KELog.Warn`

!!! warning "Titles are ASCII-only"
    The title uses the game's **title font** (`Kremlin`), for which the developers shipped **no localised
    version in any language** — so non-ASCII characters render as `?`. **The body is not affected**: it uses
    the localised font and displays Chinese, Japanese, etc. normally. When a title contains non-ASCII
    characters KE logs a `KELog.Warn`. If you need non-ASCII text, put it in the **body**.

### Full-Screen Alert `StartScreenBleedEffectWCC`

**Written exactly like vanilla `StartScreenBleedEffect`**, with two extra colour parameters.
Suited to system alarms, emergencies and story turns.

| Attribute | Required | Default | Description |
|-----------|:--------:|---------|-------------|
| `AlertTitle` | ❌ | `EMERGENCY` | Top alert title text |
| `TotalDurationSeconds` | ❌ | `200` | Total duration of the effect in seconds |
| **`BackgroundColor`** | ❌ | dark red (`120,0,0`) | Full-screen background colour |
| **`TextBackgroundColor`** | ❌ | translucent dark red (`105,0,0,200`) | Colour behind the text |
| `CompleteAction` | ❌ | empty (not run) | ConditionalActions file loaded and run when the effect ends; `NONE` / empty = do not run |
| `Delay` / `DelayHost` | ❌ | — | Pathfinder delayed execution |

The element body is parsed line by line and **at most 3 lines are used** (short input is padded, extra
lines ignored).

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

- **Colour values**: go through the CustomColor parsing chain (dynamic colour / preset / numeric RGB / Hex /
  XNA named colour) — full rules in the [Custom Color System](./../systems/custom-color.md)
- **Cancelling**: use the **vanilla** action `<CancelScreenBleedEffect />`. A patch makes KE stop its own
  effect in step, so the vanilla and custom effects never stack
- WCC = **W**ith **C**ustom **C**olor

### RAM Display `RamDisplay`

Adjusts the **RAM module** text on the operation interface: `USED RAM: x / y mb`.
`Multiplier` scales both `x` and `y`; `Unit` replaces the unit string — **text only**.
The RAM bar still follows the real usage ratio.

Useful for faking a machine's specs in the story (e.g. making a small box “look” like it has lots of memory).

!!! note "Relationship with ZeroDayToolKit"
    Stacks with **ZeroDayToolKit**'s `SASetRAM`: the latter sets the RAM values, and this action
    scales them (multiplier + unit) for display. The two do not conflict — values are set first, then displayed.

| Attribute | Required | Default | Description |
|-----------|:--------:|---------|-------------|
| `Multiplier` | ❌ | `1` | Unified scale for `x` and `y`; **decimals preserved** (up to two, trailing zeros trimmed) |
| `Unit` | ❌ | `mb` | Unit string replacing `mb`; `NONE` / empty = fall back to default |
| `Delay` / `DelayHost` | ❌ | — | Pathfinder delayed-action attributes |

```xml
<RamDisplay Multiplier="0.5" Unit="GB" />
<!-- real 1024mb → shown as 512GB -->
```

- **Only explicitly given attributes are applied**; omitted ones **keep their current value** (so you can change just the multiplier, or just the unit)
- **Reset**: write `Multiplier="1" Unit="mb"` explicitly
- **Persistence**: changes are saved immediately and survive loading; a save without this node returns to defaults
- **No KE-Config section**: defaults live in code and are overridden by this action (extensions can set them from their own startup action)
- With neither attribute set, the action does nothing and does not trigger a redundant save

---

## Action Name Conflicts and Fallback

Third-party mods may occupy Action names (real example: `Stuxnet.Audio` occupies `PlaySound`).
Pathfinder's `RegisterAction` throws on a duplicate name and **aborts the whole plugin load**, so
**all 32 KE Actions** are registered under **two names**: the original, and `KE` + the original.

| Original name | Alias registered alongside |
|---------------|---------------------------|
| `FailTrial` | `KEFailTrial` |
| `RestoreCustomTrialNodes` | `KERestoreCustomTrialNodes` |
| `LaunchVMAttack` | `KELaunchVMAttack` |
| `PhaseSwiftInit` | `KEPhaseSwiftInit` |
| `PhaseSwiftScene` | `KEPhaseSwiftScene` |
| `PhaseSwiftMusic` | `KEPhaseSwiftMusic` |
| `PhaseSwiftStop` | `KEPhaseSwiftStop` |
| `PhaseSwiftFadeOut` | `KEPhaseSwiftFadeOut` |
| `BlockNode` | `KEBlockNode` |
| `UnblockNode` | `KEUnblockNode` |
| `TerminalFocus` | `KETerminalFocus` |
| `TerminalWrite` | `KETerminalWrite` |
| `TerminalType` | `KETerminalType` |
| `RenameNode` | `KERenameNode` |
| `SetNodeIcon` | `KESetNodeIcon` |
| `SwitchToThemeKeepLayout` | `KESwitchToThemeKeepLayout` |
| `PlaySound` | `KEPlaySound` |
| `FlashScreen` | `KEFlashScreen` |
| `StartScreenBleedEffectWCC` | `KEStartScreenBleedEffectWCC` |
| `ClockStart` | `KEClockStart` |
| `ClockStop` | `KEClockStop` |
| `AttackAircraft` | `KEAttackAircraft` |
| `UploadAircraftSysFile` | `KEUploadAircraftSysFile` |
| `ShowAircraftOverlay` | `KEShowAircraftOverlay` |
| `HideAircraftOverlay` | `KEHideAircraftOverlay` |
| `BreakHeart` | `KEBreakHeart` |
| `LinkControlReset` | `KELinkControlReset` |
| `LinkControlAdd` | `KELinkControlAdd` |
| `LinkControlRemove` | `KELinkControlRemove` |
| `ShowTitle` | `KEShowTitle` |
| `StartEnding` | `KEStartEnding` |
| `RamDisplay` | `KERamDisplay` |

**Rules**:

- Both names point at the **same Action** — either one runs the same implementation
- **No conflict**: both names work
- **One name taken by a third party**: the other still works, and KE logs a `Warn`
- **Both taken** (very rare): the Action is not registered and KE logs an `Error`

!!! tip "Why register two names for everything"
    - **One rule to remember**: no need to learn which Actions have a `KE`-prefixed variant — **all do**
    - **The `KE`-prefixed name works in every environment**, conflicts or not
    - **Safe against future name grabs**: no conflict at release time does not mean no conflict in a
      player's setup; an extension written with the `KE`-prefixed name **won't change behaviour** if some
      mod later takes the original name
    - **Easier debugging**: on an “unknown action” error, switch to the `KE`-prefixed name to tell
      “never registered” apart from “taken by someone else”

    **Do not write both names** — they point at the same Action and it would run twice.

The full mechanism (why catching the exception is the only option, and the `Compat/` layout) is on
[Mod Compatibility](./mod-compat.md).

---

## Delayed Execution

Most actions support `Delay` and `DelayHost` attributes for delayed execution.

- `Delay`: the number of seconds to wait.
- `DelayHost`: the ID of a host that provides the delay service (must have a `FastActionHost` daemon).

If `Delay` is 0 or negative, the action runs immediately.

---

## See Also

- [Home](./../index.md) – Return to main index
- [自定义Action (中文)](./../../zh/components/actions.md) – Chinese version
- [Custom Color System](./../systems/custom-color.md) – colour value rules
- [Mod Compatibility](./mod-compat.md) – the full Action-name conflict mechanism
- [Phase Swift System](./../systems/phase-swift.md)
- [Custom Trial System](./../systems/custom-trial.md)
- [VM Attack System](./../systems/vm-attack.md)
- [Aircraft Daemon System](./../systems/aircraft.md)
- [Custom Timer System (Clock)](./../systems/clock.md)
- [Custom Node Icon System](./../systems/node-icon.md)
- [Custom Ending System (StartEnding)](./../systems/custom-ending.md)
