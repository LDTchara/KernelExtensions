# Porthack Heart (PorthackHeartDaemon)

**PorthackHeartDaemon (PHD)** recreates the vanilla heart-node sequence — rotating wireframe cube →
align → heart shape → white fade-out — and opens **every stage** up to configuration: title, music,
timing, callbacks and input locking.

!!! info "Applies to"
    This page corresponds to KernelExtensions **0.7**. Related daemon: `PorthackHeartDaemon`;
    related action: `BreakHeart`.

---

## Overview

- **Declaration**: write `<PorthackHeartDaemon ... />` inside a computer's XML (every parameter is optional with a default)
- **Idle state**: a rotating 3D wireframe cube with a flickering title
- **Trigger**: the `<BreakHeart>` Action (explicit, from story) or `AutoOnPorthack` (automatic when porthack progress exceeds 50%, guarded by a one-shot flag)
- **Ending**: it does **not** fire the vanilla ending — it enters a terminal state (dark screen) plus generic cleanup, then hands control to `OnComplete`

---

## Full parameter list

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `Title` | string | `PortHack.Heart` | Flickering title in the idle state; `NONE` / empty = hide it |
| `Music` | string | `Music/Ambient/AmbientDrone_Clipped` | Track switched to on heartbreak; `NONE` / empty = no switch |
| `FadeoutDelay` | number | `1` | **Delay** before the surrounding dark veil fades in (seconds) |
| `FadeoutDuration` | number | `10` | **Duration** of that veil fade-in (seconds) |
| `AlignTime` | number | `2.5` | Time for the cube to rotate into alignment (seconds) |
| `HeartDuration` | number | `30` | Total length of the heart sequence (seconds) |
| `FlashOutTime` | number | `3.8` | White fade-out after the heart completes (seconds) |
| `OnComplete` | string | none | Action file loaded **after** the sequence ends (relative to the extension root); `NONE` / empty = do not run |
| `OnHeartbreak` | string | none | Action file loaded when the heartbreak **begins**; `NONE` / empty = do not run |
| `LockInput` | bool | `true` | Whether to lock input and disable top-bar buttons during the sequence (matches vanilla) |
| `AutoOnPorthack` | bool | `false` | Whether a porthack (progress > 50%) triggers the heartbreak automatically |

!!! tip "The NONE convention"
    A string parameter set to `NONE` (case-insensitive) or left empty means **explicitly disabled**;
    **omitting** the attribute means "use the default".

---

## Example

```xml
<Computer id="heart_node" name="Heart" type="empty">
    <PorthackHeartDaemon
        Title="PortHack.Heart"
        Music="Music/Ambient/AmbientDrone_Clipped"
        FadeoutDelay="1"
        FadeoutDuration="10"
        AlignTime="2.5"
        HeartDuration="30"
        FlashOutTime="3.8"
        OnHeartbreak="Actions/HeartStart"
        OnComplete="Actions/HeartBroken"
        LockInput="true"
        AutoOnPorthack="false" />
</Computer>
```

Triggering it explicitly (every Action-side parameter is an **override**):

```xml
<BreakHeart NodeID="heart_node" OnComplete="Actions/HeartBroken" />
```

---

## The sequence

```
Idle: rotating 3D wireframe cube + flickering title (Title)
   ↓  trigger: <BreakHeart> or AutoOnPorthack (porthack progress > 50%)
(1) Cancel trace / clear popups / lock input (when LockInput = true)
(2) Run the OnHeartbreak action file
(3) Switch music (Music) and play the startup sound (SFX/TraceKill)
(4) 18 seconds later play the glow sound (SFX/Ending/PorthackSpindown)
(5) Cube rotates into alignment (AlignTime)
(6) Heart sequence (HeartDuration)
(7) White fade-out (FlashOutTime); the surrounding veil fades in during this phase
    (delayed by FadeoutDelay, lasting FadeoutDuration)
(8) Terminal state (dark screen) + generic cleanup
(9) Run the OnComplete action file → control passes to your story
```

---

## Terminal state and cleanup

After the sequence PHD performs a **generic cleanup** so the story can take over safely:

- unlock input
- disconnect the player
- **invalidate the heart node**: remove visibility / mark `disabled` / clear its daemon / assign a random IP
- refresh `ComputerLookup`

Then `OnComplete` runs. **Ending missions, flags, music and save handling are yours to define** in
`OnComplete` — PHD makes none of those decisions for you.

---

## Relationship to “custom endings”

Two different things, easily confused:

| | PorthackHeartDaemon | [Custom Ending System (StartEnding)](./custom-ending.md) |
|---|---|---|
| What | the **heartbreak sequence**: cube → heart → white flash | the **ending screen**: credits text + subtitles + music |
| Trigger | `BreakHeart` / automatic | `StartEnding` |
| State of the world afterwards | terminal state + node cleanup | fades per config, returns to the story or stays |

**Want a custom ending after the heartbreak?** Write `<StartEnding ... />` inside `OnComplete`.

---

## Known limitations

- **It does not fire the vanilla ending** (by design): vanilla's `endingSequence` is a hard-coded ending
  that is useless to extensions. What happens after the heartbreak is entirely up to `OnComplete`.
- **Sounds are not configurable**: the startup and glow sounds use vanilla Content paths
  (`SFX/TraceKill`, `SFX/Ending/PorthackSpindown`) and are skipped silently when missing.
- ⚠️ **`ResetHeartbreak()` has no XML entry point**: it is a **public method** on `PorthackHeartDaemon`,
  callable only from code (mod authors). Plain extension authors cannot trigger it from XML — ask if you
  need that.
- Input is **locked** during the sequence unless `LockInput="false"`; any other value is treated as `true`.

---

## Related action

### `BreakHeart`

Explicitly triggers the PorthackHeartDaemon heartbreak sequence on a node.

| Attribute | Required | Description |
|-----------|:--------:|-------------|
| `NodeID` | ✅ | Target computer ID |
| all others | ❌ | **Overrides**: omit to use the daemon's own config, write `NONE` or leave empty to disable |

```xml
<BreakHeart NodeID="heart" />
<BreakHeart NodeID="heart" OnComplete="Actions/HeartBroken" />
```

---

## See Also

- [Home](./../index.md) – back to the main index
- [自定义 Porthack 心脏（中文）](./../../zh/systems/porthack-heart.md) – Chinese version
- [Custom Ending System (StartEnding)](./custom-ending.md) – for an ending screen after the heartbreak
- [Custom Daemons](./../components/daemons.md) – the daemon list
- [Actions](./../components/actions.md) – `BreakHeart` and the rest
