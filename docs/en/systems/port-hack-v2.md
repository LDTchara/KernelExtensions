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
- A protocol that reaches its end settles its own result **immediately** and does not wait for the others.

---

## Root configuration

| Element | Description |
|---------|-------------|
| `<ProgramName>` | Name shown in the terminal. Empty = `PortHackV2`. Does **not** affect the registration name `#PORTHACKV2#` |
| `<RamCost>` | RAM cost in MB. Empty = 246 (same as vanilla) |
| `<Time>` | Total length of the Cracking phase in seconds. Empty = 10 |
| `<Polyhedron>` | Platonic solid used for the animation: `Tetrahedron` / `Cube` / `Octahedron` / `Dodecahedron` / `Icosahedron` |
| `<AccentColor>` | Colour of the **wireframe solid** |
| `<ProgressColor>` | Colour of the bottom progress bar. Empty = follow `AccentColor` |
| `<BackgroundColor>` | Panel background colour. Empty = follow `AccentColor` |
| `<BackgroundEffect>` | Background effect type |
| `<CompletionText>` | Success text. Empty = `COMPLETE` |
| `<SpinUpRatio>` | Spin-up length as a fraction of `<Time>`, 0–1. Empty = 0.1 |
| `<SpinUpTime>` | Spin-up length in **absolute seconds** (overrides the ratio). **Negative = use the ratio** |
| `<SpinDownTime>` | Brake length in seconds. Empty = 1.7 (same as vanilla) |
| `<CompleteDelay>` | Pause before exiting after the success sequence, in seconds. Empty = 5 |
| `<TakeAdmin>` | Whether to make the player the target's owner on success. Omitted = `false` (**only opens ports, never grants admin on its own**) |
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
| `<StartDelay>` | ❌ | Start delay in seconds, relative to the start of Cracking |
| `<Rng>` | ❌ | Random seed for this protocol |
| `<TimeIfSuccess>` | ❌ | Duration of the success branch, in seconds |
| `<TimeIfFail>` | ❌ | Duration of the failure branch, in seconds |
| `<OnStart>` / `<OnComplete>` / `<OnFail>` | ❌ | This protocol's own action points |

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

`AccentColor` / `ProgressColor` / `BackgroundColor` all accept CustomColor preset names and dynamic colours (`LDTchara` / `Rainbow` / `Gradient:…`), as well as the static formats (`#RGB` / `#RRGGBB` / `#AARRGGBB` / `R,G,B[,A]` / XNA names) — see the [Custom Color System](./custom-color.md).

Empty or `NONE` means "use the default" (`ProgressColor` / `BackgroundColor` then follow `AccentColor`). The semantic state colours (met / unmet) still come from the theme and are not affected by these options.

---

## Interface

- **Locked / Listing** paint a background — these two states have no artwork of their own.
- **Cracking / Complete** do **not** paint a background: the solid animation *is* the main visual.
- The top title bar shows `IP: <target>` and the program name (vanilla behaviour).
- **No exit button during Cracking** — you cannot leave mid-crack. It appears in the other states and disappears the instant the program exits.
- The bottom progress bar colour comes from `<ProgressColor>`.

### Animation

The solid animation renders off-screen and is then composited back (vanilla is inside a single `SpriteBatch.Begin()` and cannot start a second batch mid-frame). It has spin-up, steady and brake phases.

Every Platonic solid spins around **its own rotational symmetry axis**, and that axis is kept vertical.

---

## See Also

- [Executables](./../components/executables.md) – all programs
- [Extension Author Guide](./../guides/author-guide.md) – the two forms of action points and available conditions
- [Configuration Files](./../components/configuration.md) – shared config conventions (NONE, negative values, colour formats)
- [Custom Color System](./custom-color.md) – colour parsing details
- [Changelog](./../guides/changelog.md)
