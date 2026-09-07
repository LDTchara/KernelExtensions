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
  - Automatic cleanup of static dictionaries to avoid memory leaks

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
| `FallDuration` | `135` | Total seconds for the aircraft to fall from 38,000 feet to the ground (in immediate fall mode). |
| `OnFailed` | `null` | Action file executed when the aircraft crashes (altitude reaches 0). |
| `OnSaved` | `null` | Action file executed when the aircraft is repaired (firmware reload succeeds and the DLL is restored). |

> Note: `FallDuration` only takes effect in immediate fall mode (`AircraftFallStartsImmediately = true`), which is enabled by default. The daemon initialises the runtime variable `H` from `FallDuration` on start‑up.

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
  - `-1` (or omitted): use the daemon's current fall duration (determined by its own configuration)  
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

Allows the aircraft's altimeter to be permanently displayed on the left side of the screen even without connecting to the target computer.

```xml
<!-- Show overlay -->
<ShowAircraftOverlay NodeID="dair_crash" />

<!-- Hide overlay -->
<HideAircraftOverlay />
```

- The overlay is automatically hidden when the aircraft crashes (if it was being displayed).
- The overlay is implemented via `OverlayPatches` (Harmony patch) and does not interfere with normal gameplay.

---

## Interface & Interaction

After connecting to a computer with `FlightDaemon`, the flight instrument panel is shown:

- **Disconnect** button: disconnects from the current aircraft (hides the panel).
- **Pilot Alert** button: sends an alert to the pilot (sets the `PilotAlerted` flag, changing the interface colors).
- **Reload Firmware** button: starts the firmware reload process. If a valid `747FlightOps.dll` exists in `FlightSystems`, the fault is cleared after 6 seconds and `OnSaved` is triggered.

---

## Fall Logic Explanation

- **Immediate fall mode** (default): altitude decreases linearly according to `FallDuration` (or the action‑overridden `H`):  
  `CurrentAltitude = 38000 * (1 - timeFallingFor / H)`  
  The descent rate is fixed at `-876.9231` ft/s, but the altitude is calculated directly by the formula to ensure the total duration equals `H` seconds.

- **Non‑immediate fall mode** (`AircraftFallStartsImmediately = false`): the first 15 seconds accelerate to maximum rate via a quadratic ease‑out, then the fall continues at constant speed. In this mode `FallDuration` does not control the total time.

> Typically, only the immediate fall mode needs attention; its crash duration is completely determined by `FallDuration` or the value specified by the action. The non‑immediate fall mode is not currently in use.

---

## Multi‑language Support

Button labels (e.g., "Disconnect", "Pilot Alert", "Reload Firmware") use the game's built‑in locale terms and automatically switch language (Chinese, English, etc.).

---

## See Also

- [Home](./../index.md) – Return to main index
- [飞机Daemon系统 (中文)](./../../zh/systems/aircraft.md) – Chinese version
- [Misc](./../guides/misc.md) – Other things not covered in the major system pages
- [Actions](./../components/actions.md) – Full list of custom actions

---

> Special thanks to **April_Crystal** for providing the core implementation of this system.