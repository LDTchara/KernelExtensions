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
proxies crash sooner. With `<SelfOverload>` enabled it can also **overload the proxy by itself** —
no shell needed at all.

!!! warning "By default it does not overload any proxy itself"
    Vanilla models "proxy overload" as a state of a shell, and running a shell requires admin on the
    target — that gate is a deliberate guard rail.

    **By default** (`<SelfOverload>` = `false`) it only accelerates shells that are *already* overloading:
    with no shell overloading, it just sits there using RAM and drawing its animation.

    With `<SelfOverload>` enabled it advances the overload on its own. That is **not** removing the
    guard rail: in vanilla, overloading a proxy has **no permission gate of its own**
    (`ShellExe.StartOverload` performs no checks) — only **running a shell** does. This option fills
    in a means vanilla never provided.

- The speed multiplier is driven **linearly by the RAM ramp** (starting at 50MB, ramping towards `<BurstTarget>`).
- With N shells overloading at once, the total rate is N × the multiplier.
- It re-scans every frame, so shells **started after it** are accelerated just the same.
- It only touches shells that are **currently overloading** — otherwise an idle shell would start its
  next overload from a negative counter, which would kill it instantly.
- **Only one instance may run at a time** — a second launch is rejected. The program is resident and
  its effect is global, so two instances would tick the same shell twice.

| Element | Description |
|---------|-------------|
| `<ProgramName>` | Name shown in the terminal. Empty = `ProxyKiller` |
| `<BurstTarget>` | Target RAM when fully ramped, in MB. Empty = 100 |
| `<RampRate>` | RAM ramp rate in MB/s. Empty = 200 |
| `<ShellOverloadTarget>` | **Accelerating existing shells**: multiplier at full ramp. Empty = 1 (no acceleration) |
| `<SelfOverload>` | **Overloading the proxy itself** — the switch. Empty = `false` (no self-overload) |
| `<SelfOverloadRate>` | Rate for self-overload. **`1` = one vanilla shell's overload speed**. Empty = 1. Range 0–1 |
| `<FollowConnection>` | `true` = close once the player leaves the node it was started on. Empty = `false` (resident) |
| `<AutoCloseOnOverload>` | Close once the watched node's proxy goes **from active to down**. Empty = `false` |
| `<BackgroundColor>` | Data-stream colour. `auto` (case-insensitive) = theme highlight; empty / `NONE` = theme highlight |
| `<BackgroundCount>` | Particle count at the reference height. Empty = 230 |
| `<BackgroundReferenceHeight>` | Reference height for the particle count, in pixels. Empty = 200 |
| `<BackgroundSpeed>` | Flow speed at the reference height, in pixels/second. Empty = 300 |
| `<ParticlePixels>` | Particle head size in pixels. Empty = 2 |
| `<TrailPixels>` | Trail length in pixels. Empty = 5 |
| `<LoadTrailGain>` | Trail multiplier at full ramp. Empty = 2 |
| `<LoadSpeedGain>` | Flow-speed multiplier at full ramp. Empty = 2.6 |

!!! note "`<SelfOverload>` forces follow-the-connection"
    When self-overload is on, `<FollowConnection>` is **ignored and forced to `true`** (a warning
    is logged). That is an **inherent constraint of this ability, not an option**: overloading keeps
    triggering the target's hostile response (trace), so not following would mean "shoot wherever
    you go".

    The **watched node** is the **currently connected node** when following, otherwise the node the
    program was started on; `<AutoCloseOnOverload>` watches that node's proxy state.

!!! tip "Why the switch and the rate are two separate fields"
    `<SelfOverload>` only says **whether** to do it; `<SelfOverloadRate>` only says **how fast**.
    Merging them (using `0` for "off") would overload the value `0` with two meanings —
    "disabled" and "rate is zero" — leaving no way to say "on, rate not decided yet".

!!! tip "Sharing an extension with PortHackV2's `<RequireProxyOverload>`"
    With `<SelfOverload>` on, the "target proxy is down" prerequisite is satisfied **for free**.
    If the same extension also has a cracker depending on it, gate the two apart with flags.

**Fixed, not configurable**: starting RAM of 50MB, the background type (upward data stream), and the
hostile-response interval (vanilla's 0.35s).

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
