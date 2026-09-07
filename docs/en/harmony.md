# Patches & Harmony

KernelExtensions applies several lightweight Harmony patches to enhance the vanilla game. All patches are injected when the mod loads and removed when it unloads.

## Patch List

| Patch Class | Purpose |
|-------------|---------|
| `MainMenuWatermarkPatch` | Draws a dynamic, flowing rainbow watermark on the main menu. |
| `OverlayPatches` | Adds a global aircraft altimeter overlay at the end of `OS.drawModules`. |
| `CrashModuleVMAttackPatch` | Modifies the original `CrashModule.Update` logic to inject the custom VM attack flow and replaces the error message. |

## Technical Details

- All patches are injected through a single static `Harmony` instance (ID: `com.LDTchara.KernelExtensions`).
- On mod unload, `UnpatchSelf()` is called to remove all patches cleanly.
- The patches are compatible with other mods and use standard `Prefix` and `Transpiler` techniques.

---

## See Also

- [Home](./index.md) – Return to main index
- [Harmony补丁 (中文)](./harmony.md) – Chinese version