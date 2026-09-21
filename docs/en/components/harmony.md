# Patches & Harmony

KernelExtensions applies many Harmony patches to enhance the vanilla game. All patches are injected when the mod loads and removed when it unloads.

## Patch List

There are currently **19** patch classes (the full list is the `Patches/` directory in the source). Major ones:

| Patch Class | Purpose |
|-------------|---------|
| `MainMenuWatermarkPatch` | Draws a dynamic, flowing rainbow watermark on the main menu (can be turned off with `<Watermark>false</Watermark>` in `KE-Config.xml`). |
| `OverlayPatch` | Adds a global aircraft altimeter overlay at the end of `OS.drawModules`. |
| `CrashModuleVMAttackPatch` | Modifies the original `CrashModule.Update` logic to inject the custom VM attack flow and replaces the error message. |
| `NodeIconRenderPatch` | Texture replacement rendering for node icons (`SetNodeIcon`). |
| `ScreenBleedWCCPatch` | Drawing for the ScreenBleed effect (`StartScreenBleedEffectWCC`) and its interaction with vanilla cancellation. |
| `TitleBannerPatches` | Drawing for the title banner (`ShowTitle`). |
| `CustomColorPatch` | Custom dynamic colour (CustomColor) support. |
| `PorthackAutoPatch` / `PorthackHeartDisplayPatch` | Auto-patching and display enhancement for the Porthack heart node. |
| `PhaseSwift*` (6: `Audio`, `AudioVisualizer`, `Cleanup`, `Connection`, `Layout`, `VisualizationInjector`) | Phase Swift system: audio chain, visualiser injection, layout protection, connection handling, cleanup. |
| Remaining (`IRCLogInjector`, `MusicManagerSuppressPatch`, `PatchAccountName`, `PatchStuxnetDrawFGamemodeMenu`) | See the `Patches/` directory in the source. |

## Technical Details

- All patches are injected through a single static `Harmony` instance (ID: `com.LDTchara.KernelExtensions`).
- On mod unload, `UnpatchSelf()` is called to remove all patches cleanly.
- The patches are compatible with other mods and use standard `Prefix` and `Transpiler` techniques.

---

## See Also

- [Home](./../index.md) – Return to main index
- [Harmony补丁 (中文)](./../../zh/components/harmony.md) – Chinese version