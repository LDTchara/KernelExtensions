# PortHackV2 Cracker

**PortHackV2** is a configurable remake of vanilla `PortHack`: the cracking flow, durations, appearance, prerequisites and finishing actions are all driven by an XML config.

It is positioned as a **story asset** — one config per crack, placed under `<extensionRoot>/PortHackV2/` and loaded by name from a save flag.

!!! info "Applies to"
    This page covers KernelExtensions **0.7**.

**Overview**

- Registration name: `#PORTHACKV2#`
- Config directory: `<extensionRoot>/PortHackV2/`
- Activation: set the save flag `PortHackV2_<fileName>` (without `.xml`) on the player
- Default RAM cost: 246MB (same as vanilla `PortHack`; change it with `<RamCost>`)
- State machine: Locked → Listing → Cracking → Complete / Interrupted

---

## Usage

1. Drop a config into `<extensionRoot>/PortHackV2/`, e.g. `MyCrack.xml`.
2. Set the save flag `PortHackV2_MyCrack` (without `.xml`) on the player.
3. The player runs `#PORTHACKV2#` in a terminal while connected to a machine that meets the prerequisites.

The flag is the entry token:

| Flags | Behaviour |
|---|---|
| none | the program stays **locked** and shows the unmet-prerequisite notice |
| exactly one | loads that config and runs normally |
| more than one | treated as an authoring error — **locked**, with an error in the log |

See `XMLExamples/PortHackV2_Example.xml` for a reference config.

---

## Flow

| State | Content |
|---|---|
| **Locked** | locked screen while prerequisites are unmet (shows what is missing) |
| **Listing** | lists every protocol in the config and their prerequisites |
| **Cracking** | protocols run **in parallel**, each staggered by its own `StartDelay` |
| **Complete** | success sequence |
| **Interrupted** | wrap-up when the connection drops |

### How cracking resolves

- Each protocol rolls its outcome once **at its own start moment** (seeded by `<Rng>`), and the branch never changes afterwards — but **both branches look identical**; only the duration differs (`<TimeIfSuccess>` / `<TimeIfFail>`), so the player cannot tell which one was rolled.
- A protocol's start time comes from its own `<StartDelay>` (relative to the start of Cracking).
- The root `<Time>` is the total length of the whole Cracking phase; the bottom progress bar follows it.
  The "Estimated Ns" line in the bottom-left of the Listing screen shows **the same number**
  (progress bar, countdown and estimate all agree).
- A protocol that reaches its end settles its own result **immediately** and does not wait for the others.

---

## Root configuration

| Element | Description |
|---------|-------------|
| `<ProgramName>` | Name shown in the terminal. Empty = `PortHackV2`. Does **not** affect the registration name `#PORTHACKV2#` |
| `<RamCost>` | RAM cost in MB. Empty = 246 (same as vanilla) |
| `<Time>` | Total length of the Cracking phase in seconds. Empty = 10. Also what the Listing screen's "Estimated Ns" shows |
| `<ShowEstimate>` | Whether the Listing screen shows the estimated crack time. Empty / non-boolean = `true`. Set `false` to hide it — the duration itself is a spoiler |
| `<ShowProtocolStatus>` | Whether to show the rolling protocol-status log during Cracking / Complete. Default `false`. When enabled, each protocol start adds a row above the progress bar and pushes older rows up; only the text areas get a background (protocol name and status each have one), and they switch to `unlockedColor` / `lockedColor` as soon as that protocol completes; Complete/Failed adds a translucent black veil over the log |
| `<UseProtocolShortNames>` | Whether the status log prefers each protocol's `<ShortName>`. Default `false`; when off, names are still truncated to half the window width |
| `<Polyhedron>` | Platonic solid used for the animation: `Tetrahedron` / `Cube` / `Octahedron` / `Dodecahedron` / `Icosahedron` |
| `<PolyhedronStyle>` | How the solid is drawn: `Wireframe` (single-layer lines, **default**) / `Solid` (filled faces) / `SolidWire` (both) / **`WireframeTrail` (lines + multi-layer trail; the only preset that enables the trail)** |
| `<SpinSpeed>` | **Target spin speed** of the solid, in radians/second. Empty = 3.2 (about one turn per 2s) |
| `<TrailLayers>` | **Multi-layer trail count**: default 20 (current look); 1 = single solid. Entry kept for tuning and shown commented in the example |
| `<TrailSizeStep>` | Extra size per layer (relative to model radius). Default 0.20 (outermost of 20 layers ≈ 4.8×; going off-frame is intentional); 0 = all layers same size |
| `<TrailPulse>` | Per-layer spin-speed pulse. Empty = 0.05; 0 = same speed for all layers |
| `<TrailContraction>` | Whether the trail contracts during spindown. Empty = true |
| `<IdleSpinSpeed>` | Speed of the slow background spin during the success sequence. Empty = scaled from `SpinSpeed` |
| `<AccentColor>` | Colour of the **solid** (shared by wireframe and faces; faces use it as the base and the renderer adds shading) |
| `<ProgressColor>` | Colour of the bottom progress bar. Empty = follow `AccentColor` |
| `<BackgroundColor>` | Panel background colour. Empty = follow `AccentColor` |
| `<BackgroundEffect>` | Background effect type |
| `<CompletionText>` | Success text. Empty = `COMPLETE` |
| `<SpinUpRatio>` | Spin-up length as a fraction of `<Time>`, 0–1. Empty = 0.1 |
| `<SpinUpTime>` | Spin-up length in **absolute seconds** (overrides the ratio). **Negative = use the ratio** |
| `<SpinDownTime>` | Brake length in seconds. Empty = 1.7 (same as vanilla) |
| `<CompleteDelay>` | Pause before exiting after the success sequence, in seconds. Empty = 5 |
| `<TakeAdmin>` | Legacy bool/compatibility mapping: when `<TakeAdminMode>` is absent, `true` → `Always`, `false`/omitted → `False`. If the target is unreachable only a warning is logged |
| `<TakeAdminMode>` | **Admin-grant mode**: `False` (default/fallback, never) / `Always` (regardless of protocol results) / `OnAnySuccess` / `OnAllSuccess`. Takes precedence when present |
| `<IgnoreMissingRequiredPorts>` | Whether to ignore a `<RequireOpenPortsBefore>` entry that is registered but **absent on the target**. Default `false` (block, waiting for PortAdd etc.); only ignores "absent", not "present but closed" or "unregistered protocol" |
| `<AutoExitOnLeave>` | Whether to close when the player leaves the node. Default `true` (vanilla behaviour) |
| `<RequireCrackedPorts>` | Optional prerequisite: how many **already-cracked ports** the target needs — see below |
| `<RequireOpenPortsBefore>` | Prerequisite: these ports must already be open. Written as multiple `<string>` children |
| `<RequireSolveFirewall>` | Prerequisite: the firewall must be solved (`true` / `false`) |
| `<RequireProxyOverload>` | Prerequisite: the proxy must be overloaded (`true` / `false`) |
| `<OnStart>` / `<OnComplete>` | Root-level action points, see below |
| `<Protocols>` | Protocol list, see below |

### Action points

Both the root level and each protocol have action points; each accepts **either an inline action or a
path to an action file** (relative to the extension root) — see
[Extension Author Guide § 4](./../guides/author-guide.md#4-action-points-inline-or-file).

| Action point | Level | Fires when |
|--------------|-------|------------|
| `<OnStart>` | root | cracking begins |
| `<OnComplete>` | root | overall wrap-up (total time elapsed) |
| `<OnStart>` | protocol | that protocol starts |
| `<OnComplete>` | protocol | that protocol succeeds |
| `<OnFail>` | protocol | that protocol fails |

The root level has **no** `OnFail` — there is no randomness at that level, so a full run always ends in the success sequence. Whether a given protocol cracked is expressed by that protocol's own `OnFail`.

One-shot effects (anything beyond playing a sound or opening a port) belong in action points. The program itself only does one thing: each successful protocol opens its own port.

### Protocols

```xml
<Protocols>
  <Protocol>
    <Name>ssh</Name>
    <StartDelay>0</StartDelay>
    <Rng>60</Rng>
    <TimeIfSuccess>12</TimeIfSuccess>
    <TimeIfFail>4</TimeIfFail>
    <OnStart>Actions/sshStart.xml</OnStart>
    <OnComplete>Actions/sshDone.xml</OnComplete>
    <OnFail>Actions/sshFail.xml</OnFail>
  </Protocol>
  <Protocol>
    <Name>ftp</Name>
    <StartDelay>3</StartDelay>
    <Rng>90</Rng>
    <TimeIfSuccess>6</TimeIfSuccess>
    <TimeIfFail>3</TimeIfFail>
  </Protocol>
</Protocols>
```

| Element | Required | Description |
|---------|:--------:|-------------|
| `<Name>` | ❌ | Protocol name (shown in the UI, also used for log tracing) |
| `<ShortName>` | ❌ | Optional abbreviation; used in the status log only when `UseProtocolShortNames=true`, otherwise falls back to truncation |
| `<StartDelay>` | ❌ | Start delay in seconds, relative to the start of Cracking |
| `<Rng>` | ❌ | Random seed for this protocol |
| `<TimeIfSuccess>` | ❌ | Duration of the success branch, in seconds |
| `<TimeIfFail>` | ❌ | Duration of the failure branch, in seconds |
| `<OnStart>` / `<OnComplete>` / `<OnFail>` | ❌ | This protocol's own action points |

### Cracked-port and missing-port prerequisites

When the target has **no ports at all**, `<RequireCrackedPorts>` still blocks, but its row is **always shown** (`0/N`).
No extra inline explanation is added; add ports with `PortAdd`-style actions and the Listing re-checks every frame.
`<IgnoreMissingRequiredPorts>` only affects `<RequireOpenPortsBefore>` entries that are registered but absent on
the target; it does not affect the cracked-port prerequisite.

### Cracked-port prerequisite

`<RequireCrackedPorts>` accepts three forms:

| Value | Meaning |
|-------|---------|
| omitted / empty / `0` / negative | **not checked** |
| `auto` (case-insensitive) | align with the **target node's own requirement** — i.e. vanilla `PortHack`'s check |
| positive integer N | always require **at least N** already-cracked ports |

!!! warning "The two forms differ"
    `auto` uses the node's own `portsNeededForCrack` (the value the vanilla engine derives from security, which is also what the UI displays, plus one). A handwritten number means "the author says at least N", with **no +1 applied**.

### Colours

`AccentColor` / `ProgressColor` / `BackgroundColor` all accept CustomColor preset names and dynamic colours (`LDTchara` / `RGB` / `Gradient:…`), as well as the static formats (`#RGB` / `#RRGGBB` / `#AARRGGBB` / `R,G,B[,A]` / XNA names) — see the [Custom Color System](./custom-color.md).

Empty or `NONE` means "use the default" (`ProgressColor` / `BackgroundColor` then follow `AccentColor`). The semantic state colours (met / unmet) still come from the theme and are not affected by these options.

---

## Interface

- **Locked / Listing** paint a background — these two states have no artwork of their own.
- **Cracking / Complete** do **not** paint a background: the solid animation *is* the main visual.
- With `<ShowProtocolStatus>` enabled, a rolling protocol-status log appears at the bottom of the Cracking / Complete screen, just above the progress bar; the row count is derived from the window height and overflow at the top is clipped rather than compressing row height. Only text areas are backed; Complete/Failed overlays a translucent black veil on the log.
- The top title bar shows `IP: <target>` and the program name (vanilla behaviour).
- **No exit button during Cracking** — you cannot leave mid-crack. It appears in the other states and disappears the instant the program exits.
- The bottom progress bar colour comes from `<ProgressColor>`.
- **The Listing paginates when rows exceed the window height** (top-right `< 1/N >`): nothing is omitted and row height is never compressed; non-protocol prerequisites (firewall/proxy/cracked ports) always precede protocol prerequisites.

### Animation

The solid animation renders off-screen and is then composited back (vanilla is inside a single `SpriteBatch.Begin()` and cannot start a second batch mid-frame). It has spin-up, steady and brake phases.

Every Platonic solid spins around **its own rotational symmetry axis**, and that axis is kept vertical.

#### Multi-layer trail (`<TrailLayers>`)

Only `<PolyhedronStyle>WireframeTrail</PolyhedronStyle>` enables the trail; plain `Wireframe` is always single-layer.
With `TrailLayers=20` (default) the same
solid 20 times. All layers start at the **same angle**; during spin-up the base rotation grows, the inner layers start first and outer layers start after reaching an automatically derived activation threshold. Thresholds are normalized to the spin-up angle so the outermost layer starts near the end of spin-up; no manual configuration is needed.
forming a spiral trail. During spindown the layers contract to the same limited degree as vanilla
(scaleFactor≈0.868 / torsion≈0.565); they **keep their size/angle differences and do not merge into one solid**, then enter `IdleSpin`.

- **All five solids share one set of parameters** — Cube / Tetrahedron / Octahedron / Dodecahedron / Icosahedron;
- Solid modes (`Solid` / `SolidWire`) do not participate in the trail yet (drawn as one layer to avoid z-fighting);
- More layers = more draw calls (20 layers ≈ 600 lines), still within budget; capped at 64;
- Trail mode uses a fixed camera (pitch 30° / yaw 15°, fixed distance); **changing `TrailSizeStep` does not move the camera**, and the final contraction size is independent of it. Single-layer mode keeps the original camera.

#### Presentation (`<PolyhedronStyle>`)

| Value | Effect |
|-------|--------|
| `Wireframe` | **Default.** Lines only, matching vanilla `PortHack` |
| `Solid` | Filled faces only: **per-face shading** (flat) + back-face culling |
| `SolidWire` | Filled faces **plus** the wireframe overlay: a low-poly look where **edges hidden by a face are not drawn** (the lines depth-test too) |

Case-insensitive; `wire` / `solid` / `both` are also accepted. An unrecognised value falls back to
`Wireframe` with a warning.

Face shading is **one brightness per face** (not per-vertex interpolation) — that is exactly what
makes a low-poly solid read as a volume; interpolating would smooth the edges away and look muddy.
The light direction is **fixed in screen space**, so a face only darkens once the solid has rotated
away from it — the look is "a solid turning", not "a light following it".

!!! tip "Prefer `SolidWire` for high face counts"
    Low face counts (tetrahedron 4 / cube 6 / octahedron 8) read best as pure `Solid`;
    high ones (dodecahedron 12 / icosahedron 20) tend to look muddy when filled, and adding the
    wireframe back (`SolidWire`) keeps the structure legible.

!!! note "`Wireframe` is the default"
    Face rendering is a new capability, so the default must equal the behaviour from before it
    existed — existing configs keep their exact look. Write `<PolyhedronStyle>` explicitly to try
    the filled modes.

#### Spin speed (`<SpinSpeed>`)

The animation's angular velocity is a **slow → fast → slow** curve: linear ramp-up to the target
speed, steady until the crack finishes, then an exponential brake back to a slow spin.
`<SpinSpeed>` sets that **target speed**, in radians per second:

| Value | Feel |
|-------|------|
| `0` | Not spinning (a static exhibit) |
| `1.6` | Slow |
| `3.2` | **Default**, about one turn every 2 seconds |
| `6.4` | Noticeably faster |
| `9.6` | Very fast, and you start to see it stepping |

The ramp-up speed and the post-brake idle speed **scale with it at fixed ratios** (0.094 and 0.156),
so the shape of the curve stays the same — which is what you normally want when tuning speed.

!!! warning "The spin speed cannot be raised without limit"
    The faster it spins, the larger the **per-frame rotation**, and past some point it stops looking
    like "spinning fast" and starts looking like it is **stepping/juddering**. At 60fps the per-frame
    rotation is roughly `SpinSpeed × 0.955` degrees:

    | `SpinSpeed` | Per frame | Feel |
    |---|---|---|
    | 3.2 (default) | ~3.1° | Smooth |
    | 6.4 | ~6.1° | Judder is visible |
    | 12 | ~11.5° | Clearly stepping |

    Below 60fps the numbers grow further. **For a more energetic feel, adjusting `<Time>` or the
    presentation mode usually works better than pushing the spin speed.**

`<IdleSpinSpeed>` controls only the slow spin **during the success sequence** (that phase is
`IdleSpin` — the solid keeps turning as a backdrop). Empty or negative = scaled from `SpinSpeed`;
set it explicitly if you want the finishing shot to move more.

---

## See Also

- [Executables](./../components/executables.md) – all programs
- [Extension Author Guide](./../guides/author-guide.md) – the two forms of action points and available conditions
- [Configuration Files](./../components/configuration.md) – shared config conventions (NONE, negative values, colour formats)
- [Custom Color System](./custom-color.md) – colour parsing details
- [Changelog](./../guides/changelog.md)
