# Misc

This page collects **KernelExtensions** components that do not belong to the three major systems (Custom Trial, VM Attack, Aircraft Daemon), but are equally useful — including custom Actions, utility classes, Harmony patches, and other auxiliary features.

---

## Main Menu Watermark

The mod displays the text **`+ KernelExtensions <version>`** with a flowing rainbow effect at the top‑left corner of the Hacknet main menu.

- Positioned to the right of the ZeroDayToolKit watermark, avoiding overlap with other mods.
- Colours flow smoothly over time with no jumps or rebounds.
- Prefixed with `+` to match the community convention.
- Version number automatically follows `KernelExtensions.ModVer`; no manual updates are required.
- The watermark disappears automatically when the extension is unloaded.

Implemented in `MainMenuWatermarkPatch.cs`, using `FlowColorUtils` for colour generation.

---

## General Custom Actions

The following Actions are not specific to any single system and can be used in any action file. For detailed usage and parameters, see the **[Actions](./actions.md)** page.

| Action | Summary |
|--------|---------|
| `PlaySound` | Plays a WAV sound effect from the extension directory. |
| `TerminalWrite` | Outputs a line of text to the terminal. |
| `TerminalType` | Types text character‑by‑character into the terminal. |
| `TerminalFocus` | Plays a terminal focus effect (full‑screen darken + expanding border). |
| `RenameNode` | Renames a node by its ID; the change is immediate and persists across saves. |

Examples:

```xml
<PlaySound Path="Sounds/beep.wav" Volume="1" Pitch="0" Delay="1.5" DelayHost="cheat"/>
<TerminalWrite text="Hello, World!" />
<TerminalType text="A message typed out" CharDelay="0.04" />
<TerminalFocus Duration="5.0" BorderDuration="2.0" FadeInDuration="0.5" />
<RenameNode NodeID="dhs" NewName="Secret Base" />
```

---

## Utility Classes

KernelExtensions exposes a set of public utility classes for use by other mods or extension authors. See the **[Utility Classes](./utility.md)** page for details.

| Utility Class | Purpose |
|---------------|---------|
| `ActionHelper` | Static method for executing action files uniformly. |
| `MusicPathResolver` | Resolves music strings from config into paths recognised by `MusicManager`. |
| `SoundHelper` | Plays WAV sound effects from within the extension. |
| `FlowColorUtils` | Provides time‑based flowing rainbow colour calculations, used for the watermark and other dynamic colour needs. |

---

## Harmony Patches

The mod applies several Harmony patches to enhance the vanilla game. All patches are injected on load and removed on unload. See the **[Patches & Harmony](./harmony.md)** page for a full list.

Major patches include:
- `MainMenuWatermarkPatch`: Rainbow watermark on the main menu.
- `OverlayPatches`: Global altimeter overlay drawing for aircraft.
- `CrashModuleVMAttackPatch`: VM attack injection and error message replacement.

---

## Deprecated / Redirected Content

The following items were previously part of the miscellaneous collection but now have dedicated pages. Please visit the respective system pages:

- Trial‑related actions: `FailTrial`, `RestoreCustomTrialNodes` → see **[Custom Trial System](./custom-trial.md)**
- VM attack action: `LaunchVMAttack` → see **[VM Attack System](./vm-attack.md)**
- Aircraft actions: `AttackAircraft`, `UploadAircraftSysFile`, `ShowAircraftOverlay`, `HideAircraftOverlay` → see **[Aircraft Daemon System](./aircraft.md)**

---

## See Also

- [Home](./index.md) – Return to main index
- [杂项 (中文)](./../zh/misc.md) – Chinese version
- [Actions](./actions.md) – Full parameter reference for all custom actions
- [Utility Classes](./utility.md) – Guide to utility classes
- [Patches & Harmony](./harmony.md) – Patch list and technical details