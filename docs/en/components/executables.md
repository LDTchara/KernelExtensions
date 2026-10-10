# Executables

KernelExtensions registers **six** custom executables with Pathfinder, each bound to a `#name#`
self-replacement token. The table below is the **complete** list of what belongs to this category.

| Program | Registration | Belongs to | Status |
|---------|--------------|------------|--------|
| `CustomTrial` | `#CUSTOMTRIAL#` | [Custom Trial System](./../systems/custom-trial.md) | ✅ Available |
| `PhaseSwift` | `#PHASESWIFT#` | [Phase Swift System](./../systems/phase-swift.md) | ✅ Available |
| `PortHackV2` | `#PORTHACKV2#` | [PortHackV2 Cracker](./../systems/port-hack-v2.md) | ✅ Available |
| `ProxyKiller` | `#PROXYKILLER#` | Resident accelerator: speeds up every shell that is currently overloading | ✅ Available |
| `EffectsPlayer` | `#EFFECTS#` | Effect playback (the underlying capability for dynamic wallpaper / video playback) | ⚠️ Owning feature unfinished |
| `WPTEST` | `#WPTEST#` | Dynamic wallpaper (test program) | ⚠️ Owning feature unfinished |

!!! info "Why the last two look like “misc”"
    `#EFFECTS#` and `#WPTEST#` belong to features that are **not finished yet** — dynamic wallpaper and
    video playback are both planned for 0.8. They register and resolve today, but have **no stable usage
    documentation**, so treat them as **experimental entry points** and don't ship them in released content.

!!! warning "The file must actually exist before it can be run"
    Registration **only** makes the token resolvable — it does **not** put the file into the node.
    For players to run these programs, the corresponding file must already exist in their own `bin/` folder;
    declare it in the content XML:

    ```xml
    <file path="bin" name="CustomTrial.exe">#CUSTOMTRIAL#</file>
    ```

    The save generator replaces `#CUSTOMTRIAL#` with the real program content, which is what makes it
    runnable; a missing file means it cannot be run at all.

## How CustomTrial is invoked

A Flag starting with `CustomTrial_` selects the configuration to load (e.g. `CustomTrial_MyTrial`).
For detailed usage, configuration options and available effects, see the
**[Custom Trial System](./../systems/custom-trial.md)** page.

## How PhaseSwift is invoked

A Flag starting with `PhaseSwift_` selects the configuration to load (e.g. `PhaseSwift_MyConfig`).
For detailed usage, scene configuration and music phases, see the
**[Phase Swift System](./../systems/phase-swift.md)** page.

## How PortHackV2 is invoked

A Flag starting with `PortHackV2_` selects the configuration to load (e.g. `PortHackV2_MyCrack`),
with the config placed under `<extensionRoot>/PortHackV2/`.
For full usage and configuration, see the **[PortHackV2 Cracker](./../systems/port-hack-v2.md)** page.

## How ProxyKiller is invoked

A Flag starting with `ProxyKiller_` selects the configuration to load (e.g. `ProxyKiller_Default`),
with the config placed under `<extensionRoot>/ProxyKiller/`.

It is a **resident accelerator**: it speeds up every shell that is **currently overloading**, so
proxies crash sooner.

!!! warning "It does not overload any proxy itself"
    Vanilla models "proxy overload" as a state of a shell, and running a shell requires admin on the
    target — that gate is a deliberate guard rail, and this program **does not remove it**.
    ProxyKiller only accelerates shells that are *already* overloading.

    As a result: with no shell overloading, it just sits there using RAM and drawing its animation.

- The speed multiplier is driven **linearly by the RAM ramp** (starting at 50MB, ramping towards `<BurstTarget>`).
- With N shells overloading at once, the total rate is N × the multiplier.
- It re-scans every frame, so shells **started after it** are accelerated just the same.
- It only touches shells that are **currently overloading** — otherwise an idle shell would start its
  next overload from a negative counter, which would kill it instantly.

| Element | Description |
|---------|-------------|
| `<ProgramName>` | Name shown in the terminal. Empty = `ProxyKiller` |
| `<BurstTarget>` | Target RAM when fully ramped, in MB. Empty = 100 |
| `<RampRate>` | RAM ramp rate in MB/s. Empty = 200 |
| `<ShellOverloadTarget>` | Speed multiplier relative to the base rate at full ramp. Empty = 1 |
| `<BackgroundColor>` | Data-stream colour. `auto` (case-insensitive) = theme highlight; empty / `NONE` = theme highlight |
| `<BackgroundCount>` | Particle count at the reference height. Empty = 230 |
| `<BackgroundReferenceHeight>` | Reference height for the particle count, in pixels. Empty = 200 |
| `<BackgroundSpeed>` | Flow speed at the reference height, in pixels/second. Empty = 300 |
| `<ParticlePixels>` | Particle head size in pixels. Empty = 2 |
| `<TrailPixels>` | Trail length in pixels. Empty = 5 |
| `<LoadTrailGain>` | Trail multiplier at full ramp. Empty = 2 |
| `<LoadSpeedGain>` | Flow-speed multiplier at full ramp. Empty = 2.6 |

**Fixed, not configurable**: starting RAM of 50MB and the background type (upward data stream).

Particle count and flow speed are derived from the **content-area height** — the
`BackgroundCount` / `BackgroundSpeed` values in the config are "at `BackgroundReferenceHeight`",
and they scale by a power law as the window resizes, so the look stays consistent across window sizes.

See `XMLExamples/ProxyKiller_Example.xml` for a reference config.

---

## See Also

- [Home](./../index.md) – Return to main index
- [可执行程序 (中文)](./../../zh/components/executables.md) – Chinese version
- [Custom Daemons](./daemons.md) – the other kind of mountable object
- [Actions](./actions.md) – the action list
