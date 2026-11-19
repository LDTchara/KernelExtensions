# ProxyKiller Accelerator

ProxyKiller is a standalone resident executable provided by KE (`#PROXYKILLER#`), loaded by a save Flag starting with `ProxyKiller_`. It accelerates every shell that is currently overloading, and can also overload the proxy by itself.

## Invocation

A Flag starting with `ProxyKiller_` selects the configuration to load (e.g. `ProxyKiller_Default`),
with the config placed under `<extensionRoot>/ProxyKiller/`.

Its window content area uses a caller-side opaque black backdrop (drawn by `ProxyKillerExe`, not by
`FlowFieldEffect`), so the flow particles/lines are layered on black instead of the game UI.

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
- ⚠️ **The RAM ramp is capped by available system memory**: the program never pushes "used RAM"
  past the total. If other programs already fill memory it stops at what fits (the speed multiplier
  then cannot reach its target either — that is a physical limit, not a bug).
  Implementation compares the **integer MB the frame would add** against `os.ramAvaliable` and
  deducts it synchronously (matching KernelFix's fix for the vanilla `DLCTraceSlower`
  high-refresh-rate `(int)` truncation issue).
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
| `<SelfOverloadRate>` | Rate for self-overload. **`1` = one vanilla shell's overload speed**. Empty = 1. **No upper bound** |
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
    is logged). That is an **inherent constraint of this ability, not an option**, for **two**
    reasons:

    - **Overloading keeps firing the target's hostile response.** This follows vanilla, which does
      the same every frame while a Shell overloads; every vanilla port cracker does it too. It
      produces the trace and the visual ping.
    - That hostile response **only fires while the player is connected to that machine** — so not
      following would mean "shoot wherever you go": every node you touch gets its hostile reaction.

    ⚠️ The **trace** half additionally requires the target to be **security level ≥ 4** (vanilla
    only assigns a trace value to those). Lower-security machines get the visual ping only.

    The **watched node** is the **currently connected node** when following, otherwise the node the
    program was started on; `<AutoCloseOnOverload>` watches that node's proxy state.

!!! tip "Why the switch and the rate are two separate fields"
    `<SelfOverload>` only says **whether** to do it; `<SelfOverloadRate>` only says **how fast**.
    Merging them (using `0` for "off") would overload the value `0` with two meanings —
    "disabled" and "rate is zero" — leaving no way to say "on, rate not decided yet".

!!! info "Relationship to vanilla `SignalScramble` (DLCTraceSlower)"
    This program's look comes from vanilla `SignalScramble`, but its mechanics are **inverted**,
    so the two can coexist:

    | | vanilla SignalScramble | ProxyKiller |
    |---|---|---|
    | Effect | **suppresses** trace speed | accelerates proxy overload / self-overload |
    | Hostile response | **not called** | called (every overload frame) |
    | Follows the connection | **yes** (exits the moment you switch) | optional; forced with self-overload |

    Vanilla's follow-the-connection is exactly why this program turned `<FollowConnection>` into a
    config option and forces it for self-overload.

!!! tip "Sharing an extension with PortHackV2's `<RequireProxyOverload>`"
    With `<SelfOverload>` on, the "target proxy is down" prerequisite is satisfied **for free**.
    If the same extension also has a cracker depending on it, gate the two apart with flags.

**Fixed, not configurable**: starting RAM of 50MB, the background type (upward data stream), and the
hostile-response interval (vanilla's 0.35s).

Particle count and flow speed are derived from the **content-area height** — the
`BackgroundCount` / `BackgroundSpeed` values in the config are "at `BackgroundReferenceHeight`",
and they scale by a power law as the window resizes, so the look stays consistent across window sizes.

See `XMLExamples/ProxyKiller_Example.xml` for a reference config.

## See Also

- [Executables overview](./../components/executables.md) – all custom executables
- [Custom Color System](./custom-color.md) – colour parsing rules
- [Home](./../index.md) – back to the main index
