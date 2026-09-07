# Custom Actions

KernelExtensions provides a set of custom Actions that can be invoked in any action file. All paths are relative to the extension root.

## General Actions

| Action | Description | Example |
|--------|------------|---------|
| `PlaySound` | Plays a WAV sound effect from the extension directory. | `<PlaySound Path="Sounds/beep.wav" Volume="1" Pitch="0" Delay="1.5" DelayHost="cheat"/>` |
| `TerminalWrite` | Outputs a line of text to the terminal. | `<TerminalWrite text="Hello, World!" />` |
| `TerminalType` | Types text character‑by‑character into the terminal. | `<TerminalType text="A message typed out" CharDelay="0.04" />` |
| `TerminalFocus` | Plays a terminal focus effect (full‑screen darken + expanding border). | `<TerminalFocus Duration="5.0" BorderDuration="2.0" FadeInDuration="0.5" />` |
| `RenameNode` | Renames a node by its ID; the change takes effect immediately and persists in saves. | `<RenameNode NodeID="dhs" NewName="Secret Base" />` |

## Trial‑Related Actions

| Action | Description | Example |
|--------|------------|---------|
| `FailTrial` | Forces the currently running CustomTrialExe trial to fail immediately. | `<FailTrial />` or `<FailTrial Delay="3.0" DelayHost="cheat" />` |
| `RestoreCustomTrialNodes` | Restores nodes destroyed in a previous trial with animated effects. | `<RestoreCustomTrialNodes ConfigName="ExampleTrial" />` |

## VM Attack Actions

| Action | Description | Example |
|--------|------------|---------|
| `LaunchVMAttack` | Triggers a specified VM attack. | `<LaunchVMAttack ConfigName="MyAttack" />` |

## Aircraft Daemon Actions

| Action | Description | Example |
|--------|------------|---------|
| `AttackAircraft` | Attacks the specified aircraft, causing a critical firmware failure and crash. | `<AttackAircraft NodeID="dair_crash" FallDuration="60" />` |
| `UploadAircraftSysFile` | Writes a valid `747FlightOps.dll` to the target computer for repairing the aircraft. | `<UploadAircraftSysFile NodeID="dair_crash" />` |
| `ShowAircraftOverlay` | Activates the global altimeter overlay for the specified aircraft. | `<ShowAircraftOverlay NodeID="dair_crash" />` |
| `HideAircraftOverlay` | Deactivates the global altimeter overlay. | `<HideAircraftOverlay />` |

---

## Delayed Execution

Most actions support `Delay` and `DelayHost` attributes for delayed execution.  
- `Delay`: the number of seconds to wait.  
- `DelayHost`: the ID of a host that provides the delay service (must have a `FastActionHost` daemon).  
If `Delay` is 0 or negative, the action runs immediately.

---

## See Also

- [Home](./index.md) – Return to main index
- [自定义Action (中文)](./../zh/actions.md) – Chinese version
- [Custom Trial System](./custom-trial.md)  
- [VM Attack System](./vm-attack.md)  
- [Aircraft Daemon System](./aircraft.md)