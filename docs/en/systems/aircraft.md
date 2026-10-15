# Aircraft Daemon System

The **Aircraft Daemon** is a complete replacement for the vanilla `AircraftDaemon`. It supports custom crash durations, actions triggered on repair or crash, and provides a global altimeter overlay for story design.  
Core implementation contributed by **April_Crystal** — special thanks.

---

## Overview

- Daemon name: `FlightDaemon`
- Registration: add `<FlightDaemon .../>` directly in the target computer's XML.
- Key enhancements:
  - Configurable crash duration (`FallDuration` in seconds, default 135)
  - Trigger `OnFailed` / `OnSaved` actions on crash or repair respectively
  - Dedicated attack, repair, and overlay actions
  - On-demand unsubscription of update hooks (`UnsubscribeIfIdle`); nothing accumulates in the background

---

## Basic Configuration

Declare the daemon directly in the computer configuration file:

```xml
<Computer id="dair_crash" ... >
  ...
  <FlightDaemon FallDuration="90" OnFailed="Actions/plane_failed.xml" OnSaved="Actions/plane_saved.xml" />
  ...
</Computer>
```

### FlightDaemon Configurable Attributes

| Attribute | Default | Description |
|-----------|---------|-------------|
| `FallDuration` | `135` | Total crash duration in seconds. `0` = **when a fall is triggered, skip the descent and crash at once** (see [Fall Logic Explanation](#fall-logic-explanation)). |
| `OnFailed` | `null` | Action file executed when the aircraft crashes (altitude reaches 0). |
| `OnSaved` | `null` | Action file executed when the aircraft is repaired (firmware reload succeeds and the DLL is restored). |
| `CrashIPPrefix` | `DCLOC:` | Prefix prepended to the node's IP after a crash, making the old IP unreachable; empty / `NONE` = leave the IP untouched (the node stays reachable). |
| `MapOriginX` / `MapOriginY` | `0.4304` / `0.8339` | Normalised coordinates (0–1, relative to the world‑map rectangle) of the **route origin** in the altimeter overlay. |
| `MapDestX` / `MapDestY` | `0.6672` / `0.4264` | Normalised coordinates of the **route destination**. |

!!! note "Route origin / destination"
    The overlay uses the vanilla world‑map texture. The coordinates are **normalised** (0–1, relative to the map
    rectangle), so they are independent of the texture size — but you will need to adjust them when swapping
    the texture or moving the endpoints. Values are clamped to `[0, 1]`.

> Note: `FallDuration` only takes effect in **immediate fall mode**, which is on by default and **cannot currently be turned off** (see [Fall Logic Explanation](#fall-logic-explanation)). The action's `FallDuration` overrides it.

---

## Related Actions

### Attack Aircraft: `AttackAircraft`

Triggers a critical firmware failure on the target computer's `FlightDaemon` and starts the fall.

```xml
<AttackAircraft NodeID="dair_crash" FallDuration="60" />
```

- `NodeID`: the `idName` of the target computer (must already have a `FlightDaemon` configured).
- `FallDuration` (optional): specifies the total crash duration in seconds; **overrides** the daemon's own `FallDuration`.  
  Special values:  
  - `-1`: use the daemon's own `FallDuration`  
  - **omitted**: keeps the action default of **135 seconds** (**not** the daemon's value — write `FallDuration="-1"` explicitly to inherit from the daemon)  
  - `0`: crash instantly (skip the descent, directly trigger `OnFailed` and node removal)  
  - positive number: override the daemon's `FallDuration`.

> Attack flow: generates a firmware DLL and deletes it immediately → 6 seconds later firmware reload fails → enters critical failure → the aircraft falls for the specified duration.

### Repair Aircraft: `UploadAircraftSysFile`

Writes a valid `747FlightOps.dll` into the target computer's `FlightSystems` folder.

```xml
<UploadAircraftSysFile NodeID="dair_crash" />
```

- If the target computer is already running `FlightDaemon`, the file is written directly into `FlightSystems/747FlightOps.dll`.
- Otherwise, the file is written to the path specified by the `Path` attribute.
- The player still needs to connect to that computer and manually click the **Reload Firmware** button to complete the repair (triggers `OnSaved`).

### Global Altimeter Overlay

Allows the aircraft's altimeter to be permanently displayed on the right side of the screen even without connecting to the target computer.

```xml
<!-- Show overlay -->
<ShowAircraftOverlay NodeID="dair_crash" />

<!-- Hide overlay -->
<HideAircraftOverlay />
```

!!! warning "The overlay does NOT close itself"
    After a crash it **keeps showing the wreck**; you **must** call `<HideAircraftOverlay />` yourself.
    It will **not** disappear on crash, disconnect, or story progress. Forget to close it and the
    altimeter stays pinned to the right side of the screen.

    A common slip: writing `<ShowAircraftOverlay />` without ever closing it. Easy to miss while
    testing, but players will still see the overlay after finishing the story.

- The overlay is implemented via `OverlayPatch` (Harmony patch) and does not interfere with normal gameplay.

---

## Interface & Interaction

After connecting to a computer with `FlightDaemon`, the flight instrument panel is shown:

- **Disconnect** button: disconnects from the current aircraft (hides the panel).
- **Pilot Alert** button: sends an alert to the pilot (the interface recolours) and writes a save flag on the **player account**: **`PilotAlerted_<node idName>`** (the game is saved immediately on click).
  ⚠️ **Use that exact name in story conditions** — it is **suffixed with the node name**, not a bare `PilotAlerted`. A `HasFlags` check that omits the `_<idName>` suffix **never matches**.
  Written only once per node (skipped if already present, so no repeated disk writes).ace colors).
- **Reload Firmware** button: starts the firmware reload process. If a valid `747FlightOps.dll` exists in `FlightSystems`, the fault is cleared after 6 seconds and `OnSaved` is triggered.

---

### Debug-only Actions `FlightDaemonStatus` / `FlightDaemonMap`

!!! warning "Debug only"
    These two actions **only take effect when `<Debug>true</Debug>` is set in `KE-Config.xml`**.
    In normal content they just print a notice and do nothing — they are not an API for extension authors.

- **`<FlightDaemonStatus />`** — prints every aircraft daemon's runtime state to the player terminal
  (altitude / airspeed / route progress / crashed / rescued / current map points, …)
  **without connecting to the target node**. Optional `NodeId="xxx"` filters to one daemon (matches `idName` or `ip`).
- **`<FlightDaemonMap OriginX="…" OriginY="…" DestX="…" DestY="…" />`** — changes the route endpoints on the fly for tuning
  (omitted values stay unchanged). The change lasts only for the **current run**; loading a save restores the node XML
  values — write the tuned values back into the four attributes above.

## Fall Logic Explanation

- **Immediate fall mode** (default): altitude decreases linearly according to `FallDuration` (or the action‑overridden `H`):  
  `CurrentAltitude = 38000 * (1 - timeFallingFor / H)`  
  The descent rate is fixed at `-876.9231` ft/s, but the altitude is calculated directly by the formula

  **Special meaning of `FallDuration = 0`**: **when a fall is triggered, skip the descent and crash at once**
  (same effect as the action's `FallDuration="0"`). ⚠️ The wording matters — it is *when triggered*, not
  "setting it crashes the plane":

  | Written | Result |
  |---------|--------|
  | `<FlightDaemon FallDuration="0" />` with **no fall triggered** | nothing happens — the aircraft keeps flying |
  | the same, **then hit by `AttackAircraft` / firmware reload fails** | **crashes at once**, skipping the descent animation |

  A fall is triggered by exactly two things: the `AttackAircraft` action, or a firmware-reload failure
  (the attack action creates a firmware DLL and deletes it immediately → reload fails 6 seconds later).
  In other words **`0` is not "setting it crashes the plane", it is "complete the fall in 0 seconds"**.

  **Negative / omitted** = the default of **135 seconds** (negative means "use the default", see General Rules). to ensure the total duration equals `H` seconds.

- **Non‑immediate fall mode** (`AircraftFallStartsImmediately = false`): the first 15 seconds accelerate to maximum rate via a quadratic ease‑out, then the fall continues at constant speed. In this mode `FallDuration` does not control the total time.

> ⚠️ **This mode cannot currently be enabled**: `AircraftFallStartsImmediately` is a field inherited verbatim from vanilla (spelling included) and has **no `[XMLStorage]`**, so it is permanently `true`. The `false` branch has **never been executed** (it is dead code in vanilla too). The description above is kept for reference only — **the behaviour is always immediate fall mode**.

---

## Multi‑language Support

Button labels (e.g., "Disconnect", "Pilot Alert", "Reload Firmware") use the game's built‑in locale terms and automatically switch language (Chinese, English, etc.).

## Known Limitations

- **Aircraft state is unreliable after loading a save**: the daemon's runtime state (fall progress, the
  `IsCrashed` terminal flag, etc.) is not fully persisted. The node itself recovers correctly, but the
  aircraft's position and running state may be wrong.

  In mission design, **avoid segments where the player can save/load while the aircraft is in flight**.
  If you truly need to block saving during a critical segment, rely on a third-party mod's capability
  (e.g. Stuxnet's `DenySaves`) — KE does not provide save interception itself.

---

## See Also

- [Home](./../index.md) – Return to main index
- [飞机Daemon系统 (中文)](./../../zh/systems/aircraft.md) – Chinese version

- [Actions](./../components/actions.md) – Full list of custom actions

---

> Special thanks to **April_Crystal** for providing the core implementation of this system.