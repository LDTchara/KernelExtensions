# Phase Swift System (PhaseSwift)

**PhaseSwift** is KernelExtensions' scene-switching system: it splits one network map into multiple "phases" — each scene carries **its own theme, topology, visible nodes and music**. The player presses Shift to slip between the surface and the underworld, and the shape of the network changes with them.

Typical use: a node is an ordinary server in phase 0; after switching to phase 1 its neighbours are all different, the theme darkens, and the music crossfades to another track.

!!! info "Applies to"
    This page covers KernelExtensions **0.7**. Related actions: `PhaseSwiftInit`, `PhaseSwiftScene`, `PhaseSwiftMusic`, `PhaseSwiftStop`, `PhaseSwiftFadeOut`, `BlockNode`, `UnblockNode`.

---

## Overview

- Configuration: `PhaseSwift/<name>.xml` (root element `<PhaseSwiftConfig>`)
- Start: `<PhaseSwiftInit ConfigName="MyConfig" />`
- Switch scene: `<PhaseSwiftScene TargetScene="1" />`
- Music phase: `<PhaseSwiftMusic Phase="1" />`
- Exit: `<PhaseSwiftStop />`
- It can also run as a standalone executable (`#PHASESWIFT#`) with Start / Shift buttons in its window

### Managed nodes — understand this one concept first

**Any node appearing in any scene's `StartNodes`, `VisibleNodes`, `Topology` or `BlockedNodes` is fully managed by PhaseSwift** (a "managed node"). Every other node is left completely alone.

What PS does to managed nodes:

- **Links**: on a scene switch it clears links **between** managed nodes and rebuilds them from that scene's `<Topology>`
- **Visibility**: on a scene switch it hides all managed nodes, then shows the ones the new scene should show
- **Connection blocking**: a managed node that should not exist in the current scene cannot be connected to (custom disconnect message)

Links between a managed node and an unmanaged node are **never touched by PS** — this is the rule that lets PS coexist safely with other systems.

---

## Quick Start

### 1. Write the configuration

```
ExtensionRoot/
├── PhaseSwift/
│   └── MyConfig.xml
├── Actions/
│   ├── scene0_switch.xml
│   └── scene1_switch.xml
└── Music/
    ├── surface.ogg
    └── underworld.ogg
```

```xml
<PhaseSwiftConfig>
    <ProgramName>Phase Swift</ProgramName>
    <InitialScene>0</InitialScene>
    <MusicPhases>
        <Phase id="0">
            <Tracks>
                <Track>Music/surface.ogg</Track>
                <Track>Music/underworld.ogg</Track>
            </Tracks>
        </Phase>
    </MusicPhases>
    <Scenes>
        <Scene id="0">
            <Theme>HacknetBlue</Theme>
            <OnSwitch file="Actions/scene0_switch.xml" />
            <StartNodes>
                <Node id="A" />
            </StartNodes>
            <VisibleNodes>
                <Node id="B" />
            </VisibleNodes>
            <Topology>
                <Link from="A" to="C" />
                <Link from="C" to="D" />
            </Topology>
        </Scene>
        <Scene id="1">
            <Theme>Themes/VOID-ICE.xml</Theme>
            <StartNodes>
                <Node id="A" />
            </StartNodes>
            <Topology>
                <Link from="A" to="B" />
                <Link from="B" to="D" />
            </Topology>
        </Scene>
    </Scenes>
</PhaseSwiftConfig>
```

### 2. Drive it from the story

```xml
<!-- Start: load the config and enter InitialScene -->
<PhaseSwiftInit ConfigName="MyConfig" />

<!-- Switch to phase 1 later (optionally overriding theme and fade) -->
<PhaseSwiftScene TargetScene="1" FadeDuration="2.0" />

<!-- Change the music phase only, without switching scenes -->
<PhaseSwiftMusic Phase="1" />

<!-- Fade every track out (for a story beat; any scene switch brings them back) -->
<PhaseSwiftFadeOut Duration="2" />

<!-- End PS -->
<PhaseSwiftStop FinishMode="full" TopologyMode="restore" />
```

A complete example: [PhaseSwift_Example.xml](https://github.com/LDTchara/KernelExtensions/blob/main/XMLExamples/PhaseSwift_Example.xml).

---

## Configuration File (`PhaseSwiftConfig`)

Location: `PhaseSwift/<name>.xml`. All paths are relative to the **extension root**.

| Option | Default | Description |
|--------|---------|-------------|
| `ProgramName` | `PhaseSwift` | Program window title / the exe's `IdentifierName` |
| `BackgroundColor` | empty | Program window background (colour name / hex / CustomColor preset) |
| `DefaultFadeDuration` | `1.5` | Default music crossfade duration (seconds) |
| `ThemeFlickerDuration` | `0.8` | Theme switch flicker duration (seconds); keep it close to the value above |
| `InitialScene` | `0` | Scene index activated on start |
| `ChangeLayout` | `false` | Whether a scene switch also swaps node layout (`true` applies topology after the flicker finishes) |
| `StartButtonText` | `开始` | Button label while not started |
| `ShiftButtonText` | `Shift` | Button label for scene switching while running |
| `ShowSceneNumber` | `true` | Whether to show the `X/X` scene counter |
| `CompleteText` | empty | Text shown on completion (falls back to the built-in dictionary if empty) |
| `FinishMode` | `none` | **Node visibility** after stopping: `none` hide all / `full` keep all / `scene_N` keep scene N only |
| `TopologyMode` | `restore` | **Topology** after stopping: `restore` / `scene_N` / `merge` — see below |
| `UseDualTrackMusic` | `true` | `true` = PS streams multiple tracks itself via DSEI; `false` = single-track mode |
| `SingleTrack` | empty | Music for single-track mode (effective when `UseDualTrackMusic=false`); falls back to the first phase's first track |
| `RestoreThemeOnStop` | `true` | Whether to restore the pre-start theme on stop |
| `GlobalDiscovery` | `true` | Cross-scene discovery: nodes found in scene A stay visible in scene B if B's `VisibleNodes` also contain them |
| `MusicPhases` | — | Music phase list, see below |
| `Scenes` | — | Scene list, see below |

Both `FinishMode` and `TopologyMode` can be overridden by the same-named parameters on `PhaseSwiftStop` — **Action parameters win**.

---

## Music Phases (`MusicPhases`)

Each `<Phase>` defines a set of tracks, and **the track at index i corresponds to the scene at index i** in `Scenes`:

```xml
<MusicPhases>
    <Phase id="0">
        <Tracks>
            <Track>Music/surface.ogg</Track>
            <Track>Music/underworld.ogg</Track>
        </Tracks>
    </Phase>
</MusicPhases>
```

- `<Phase id>` is the value you pass to `PhaseSwiftMusic Phase=`
- The number of tracks should match the number of scenes (one track per scene)
- Paths resolve against the extension root first, then fall back to `Music/<filename>`
- **OGG only** (NVorbis streaming into a `DynamicSoundEffectInstance`); scene switches crossfade
- Volume follows the game's music volume setting

!!! warning "All tracks in a phase must be the same length"
    Tracks are bound to scenes by index and **scene switches crossfade rather than restart**. If tracks in one
    phase have different lengths, repeated switching gradually drifts out of sync. Keep them equal.

While running, PS **stops `MusicManager` playback** so it will not stack with vanilla music, and hands it back on exit.

---

## Scenes (`Scenes`)

The number of scenes is unlimited (2–4 is typical). The player cycles through them in order with Shift.

| Child element | Required | Description |
|---------------|:--------:|-------------|
| `<Theme>` | ❌ | Scene theme. Empty = keep the current theme (including one set by the previous scene's `OnSwitch`); accepts a built-in name (`HacknetBlue` / `HacknetMint` / `HacknetOrange` / `HacknetRed`) or a custom theme path |
| `<OnSwitch file="...">` | ❌ | Action file executed when switching into this scene (`NONE` / empty = not executed). **This is the proper hook for runtime logic such as LinkControl** |
| `<StartNodes>` | ❌ | Nodes **visible by default** on switching into this scene. Only these appear directly on the map; the rest are discovered by scanning topology links |
| `<VisibleNodes>` | ❌ | The set of nodes **allowed to appear** in this scene; together with `GlobalDiscovery` it decides whether discovered nodes stay visible |
| `<Topology>` | ❌ | Links between managed nodes, declared as `<Link from="A" to="B" />` (**directed**) |
| `<BlockedNodes>` | ❌ | Static blocklist for this scene (`<Node>A</Node>` form), always hidden in the scene |

### Topology replaces rather than appends

On a scene switch, PS first clears all links **between managed nodes**, then adds the new scene's `<Topology>`. Therefore:

- To make a link disappear in a scene, simply **do not write it**
- Links between managed and unmanaged nodes are always preserved
- For a bidirectional connection, write two `<Link>` entries (`A→B` and `B→A`)

---

## Visibility, Blocklist and Connection Blocking

When switching into a scene, the nodes shown are:

```
(StartNodes) ∪ (nodes already discovered in this scene) ∪ (under GlobalDiscovery: nodes discovered
in other scenes whose IDs are also in this scene's VisibleNodes)
− (this scene's runtime blocklist)
```

Nodes being shown get a highlight flash and an expanding ring. Conversely, a managed node that is not in the current scene's visible set **cannot be connected to** by the player (custom disconnect message).

### Runtime blocklist

```xml
<!-- Add node X to the current scene's runtime blocklist -->
<BlockNode NodeId="X" />

<!-- Target a specific scene (omit SceneIndex for the current one) -->
<BlockNode NodeId="X" SceneIndex="1" />

<!-- Remove it again -->
<UnblockNode NodeId="X" />
```

Unlike `<BlockedNodes>` (static, written in the config), the runtime blocklist is added and removed by the story at runtime and **persists with the save file**.

---

## Ending and Topology Handling (`PhaseSwiftStop`)

PS's behaviour on exit has **two orthogonal dimensions**, each configurable:

**① Node visibility (`FinishMode`)**

- `none` — hide everything (default)
- `full` — keep every scene's start nodes and discovered nodes
- `scene_N` — keep only scene N's nodes visible

**② Topology (`TopologyMode`)**

- `restore` — restore the original links snapshotted when PS started (default)
- `scene_N` — restore the original links, then overlay scene N's `<Topology>` (same as staying on that scene at runtime)
- `merge` — clear links between managed nodes, then merge **every** scene's `<Topology>` on top (deduplicated per from-to pair)

!!! warning "`merge` connects paths from later scenes"
    merge stacks every scene's topology together, **including scenes the player has not been through yet** —
    e.g. scene 0 has `A→B` and scene 2 has `B→C`, so after stopping with merge the player can walk straight to C.
    Use it only once the **story line is fully finished and free exploration is restored**.

```xml
<!-- Action parameters take precedence over the config -->
<PhaseSwiftStop FinishMode="full" TopologyMode="merge" />
```

An invalid or unknown mode value **falls back to `restore` and logs a `KELog.Warn`** — never silently.

### When the "original" links are taken

`restore` restores `_originalLinks`, which has two sources:

1. **New game** — a snapshot of managed nodes' links taken at the moment PS **initialises** (i.e. the result of the content XML's `<dlink>`)
2. **Loading a save** — overwritten with the `<OrigLink>` records in the save, rather than re-snapshotting

So the semantics are: **"original" = the state at the moment PS started.** This gives one useful corollary:

> **Using `<LinkControlAdd>` on managed nodes *before* `PhaseSwiftInit` makes those changes part of the "original state".**
> That is not an error — do it before Init when the change should be part of the initial state, and after Init when it should be temporary.

---

## Working with LinkControl

Both touch `Computer.links`, but on different scopes: PS only manages links **between managed nodes**, whereas LC can touch any link.

**The proper combination**: hang runtime link changes on a scene's `OnSwitch` with LC — PS has just rebuilt the topology, so LC's adjustment layers on top with naturally correct ordering.

```xml
<!-- Actions/scene1_switch.xml -->
<ConditionalActions>
    <Instantly>
        <!-- Temporarily open a story link when entering phase 1 -->
        <LinkControlAdd SourceComp="A" TargetComp="ghost" />
    </Instantly>
</ConditionalActions>
```

!!! warning "The one combination to avoid"
    **Using `<LinkControlReset>` while PS is running** — it assigns links wholesale and flattens the current
    scene's topology. To restore topology, switch scenes (PS rebuilds it) or use `PhaseSwiftStop`, not LC Reset.

Everything else coexists safely. Note that LC's temporary changes **between managed nodes** are wiped on the next scene switch — that is expected behaviour, not a bug.

---

## Persistence

- **Flag-driven auto-restore**: `<PhaseSwiftInit>` writes the flag `PhaseSwift_<ConfigName>`; on loading a save the presence of that flag restores PS automatically (scene, topology, visibility, discovered nodes, music phase, theme)
- `<PhaseSwiftStop>` removes the flag (the story has ended properly); cleanup paths such as killing the exe or unloading the extension **do not** remove it, so "kill the exe, reload, the story continues" still holds
- The save contains `<PhaseSwiftData>`:

```xml
<PhaseSwiftData ConfigName="MyConfig" CurrentScene="1" MusicPhase="1" Theme="Themes/VOID-ICE.xml">
  <DiscoveredScene Index="0"><Node>B</Node></DiscoveredScene>
  <OrigLink NodeId="A" Targets="C,D" />
  <RuntimeBlockedScene Index="1"><Node>X</Node></RuntimeBlockedScene>
</PhaseSwiftData>
```

---

## Action Reference

### `PhaseSwiftInit`

Loads the configuration and starts (initialisation + entering `InitialScene`).

| Attribute | Required | Description |
|-----------|:--------:|-------------|
| `ConfigName` | ❌ | Config file name (no path or extension), default `Default`; the file lives at `PhaseSwift/<ConfigName>.xml` |

### `PhaseSwiftScene`

Switches scene: triggers the music crossfade, topology replacement, visibility update, theme switch and the scene's `OnSwitch`. **Use `PhaseSwiftMusic` for music-phase changes** (the two are deliberately separate).

| Attribute | Required | Description |
|-----------|:--------:|-------------|
| `TargetScene` | ✅ | Target scene index (0-based) |
| `FadeDuration` | ❌ | Music fade duration (seconds); negative or omitted = config default |
| `Theme` | ❌ | Override this scene's theme (preset name or custom path) |

### `PhaseSwiftMusic`

Switches the music phase only, without switching scenes.

| Attribute | Required | Description |
|-----------|:--------:|-------------|
| `Phase` | ✅ | Target music phase id (matches `id` in `MusicPhases`) |

### `PhaseSwiftFadeOut`

Fades every track out to silence (players are not released, so any scene switch brings them back).

| Attribute | Required | Description |
|-----------|:--------:|-------------|
| `Duration` | ❌ | Fade duration (seconds), default `1` |

### `PhaseSwiftStop`

Stops PS and cleans up. See "Ending and Topology Handling" above.

| Attribute | Required | Description |
|-----------|:--------:|-------------|
| `FinishMode` | ❌ | Node visibility: `none` / `full` / `scene_N`; config used when omitted |
| `TopologyMode` | ❌ | Topology handling: `restore` / `scene_N` / `merge`; config used when omitted |

### `BlockNode` / `UnblockNode`

Add or remove runtime blocklist entries.

| Attribute | Required | Description |
|-----------|:--------:|-------------|
| `NodeId` | ✅ | Target node ID |
| `SceneIndex` | ❌ | Target scene index; omitted or `-1` = current scene |

---

## Known Limitations

- **Conflict with Stuxnet.Audio**: that mod hijacks extension music (`ReplaceMusicManager`) and breaks PS's multi-track playback chain. If your extension uses both, expect altered track behaviour
- Tracks of unequal length inside one phase drift out of sync after switching (see the warning above)
- When a scene theme is a custom path, write it relative to the extension root (PS does not prepend anything)

---

## See Also

- [Home](./../index.md) – back to the main index
- [相位穿梭系统（中文）](./../../zh/systems/phase-swift.md) – Chinese version
- [Node link control actions](./../components/actions.md) – `LinkControlAdd` / `Remove` / `Reset`
- [Custom Actions](./../components/actions.md) – full list of custom actions
- [Configuration Files](./../components/configuration.md) – index of per-system config files
