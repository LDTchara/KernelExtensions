# Custom Actions

KernelExtensions provides a set of custom Actions that can be invoked in any action file. All paths are relative to the extension root.

## General Actions

| Action | Description | Example |
|--------|------------|---------|
| `PlaySound` | Plays a WAV sound effect from the extension directory. | `<PlaySound Path="Sounds/beep.wav" Volume="1" Pitch="0" Delay="1.5" DelayHost="cheat"/>` |
| `TerminalWrite` | Outputs a line of text to the terminal. | `<TerminalWrite Text="Hello, World!" />` |
| `TerminalType` | Types text character‑by‑character into the terminal — **no automatic newline**; appends at the current cursor (like HackerScript's `write`, so several calls can share one line). | `<TerminalType Text="A message typed out" CharDelay="0.04" />` |
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

## Node Link Control Actions

| Action | Description | Example |
|--------|-------------|---------|
| `LinkControlReset` | Restore the computer's links to the org baseline (discards all runtime add/remove). | `<LinkControlReset SourceComp="playerComp" />` |
| `LinkControlAdd` | Temporarily add a link at runtime (does not write to the baseline). | `<LinkControlAdd SourceComp="playerComp" TargetComp="jmail" />` |
| `LinkControlRemove` | Temporarily remove a link at runtime (does not write to the baseline). | `<LinkControlRemove SourceComp="playerComp" TargetComp="jmail" />` |

> All three share the org baseline: a snapshot of the computer's links taken **at game start**
> (content XML `<dlink>` enters the baseline this way), persisted to the save as `<OrgLinks>`.
> `Add`/`Remove` only change runtime links and can be undone with `Reset`.
> Attribute names are **case-insensitive** (handled by the `KEAction` base class; PascalCase is still recommended).
> Missing nodes or a missing `TargetComp` are logged as errors and skipped — never a crash.
> Note: `<OrgLinks>` only ever appears in saves; to declare initial links in content XML, use vanilla `<dlink>`.

---

## Other Actions

| Action | Description | Example |
|--------|-------------|---------|
| `FlashScreen` | UI flash: pulses the interface with a colour and fades back to the current theme's default. | `<FlashScreen Color="Red" Duration="2.0" />` |
| `SwitchToThemeKeepLayout` | Switches theme while **keeping the panel layout** (colour only). | `<SwitchToThemeKeepLayout ThemePathOrName="HacknetMint" FlickerInDuration="1.5" />` |
| `BreakHeart` | Explicitly triggers the PorthackHeartDaemon heartbreak sequence on a node. | `<BreakHeart NodeID="heart" OnComplete="Actions/HeartBroken" />` |
| `BlockNode` | Adds a runtime-blocklisted node to the current (or a specified) PhaseSwift scene. | `<BlockNode NodeId="A" SceneIndex="0" />` |
| `UnblockNode` | Removes a runtime-blocklisted node. | `<UnblockNode NodeId="A" />` |
| `PhaseSwiftInit` / `Scene` / `Music` / `Stop` / `FadeOut` | Starting, scene switching, music-phase switching and exiting the Phase Swift system. | See [Phase Swift System](./../systems/phase-swift.md) |

- `FlashScreen` takes Hex, numeric RGB, named colours, or dynamic colours (e.g. `LDTchara`) in `Color`; `Duration` defaults to `2.0` (non-positive = restore defaults immediately); `PlaySound="true"` plays the warning beep alongside the flash. Re-triggering **refreshes** rather than stacking.
- `SwitchToThemeKeepLayout` changes colours only; use vanilla `SASwitchToTheme` if you need the layout changed too.
- Every `BreakHeart` parameter except `NodeID` is an **override**: omit it to use the daemon's own config, or write `NONE` / leave it empty to explicitly disable (e.g. `Music="NONE"` skips the track change).

---

## Delayed Execution

Most actions support `Delay` and `DelayHost` attributes for delayed execution.  
- `Delay`: the number of seconds to wait.  
- `DelayHost`: the ID of a host that provides the delay service (must have a `FastActionHost` daemon).  
If `Delay` is 0 or negative, the action runs immediately.

---

## See Also

- [Home](./../index.md) – Return to main index
- [自定义Action (中文)](./../../zh/components/actions.md) – Chinese version
- [Phase Swift System](./../systems/phase-swift.md)
- [Custom Trial System](./../systems/custom-trial.md)  
- [VM Attack System](./../systems/vm-attack.md)  
- [Aircraft Daemon System](./../systems/aircraft.md)
- [Custom Timer System (Clock)](./../systems/clock.md)
- [Custom Title Banner (ShowTitle)](./../systems/title-banner.md)
- [Custom ScreenBleed Effect](./../systems/screen-bleed.md)
- [Custom Ending System (StartEnding)](./../systems/custom-ending.md)