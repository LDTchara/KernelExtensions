# Patches & Harmony

KernelExtensions applies Harmony patches to enhance the vanilla game. All patches are injected when the mod
loads and removed automatically when it unloads.

## Patch List

There are currently **20** patch classes: 19 under `Patches/` (one file, `PhaseSwiftLayoutPatch.cs`, holds
two classes) and 1 under `Compat/Stuxnet/`.

The table below is organised by **target method** — when coexisting with other mods, use it to judge whether
both sides might touch the same method.

| Patch class | Target method(s) | Kind | Purpose |
|-------------|------------------|------|---------|
| `MainMenuWatermarkPatch` | `MainMenu.DrawBackgroundAndTitle` | Prefix | Main-menu rainbow watermark (turn off with `<Watermark>false</Watermark>`) |
| `OverlayPatch` | `OS.drawModules` / `OS.Update` | Postfix | Global aircraft altimeter overlay |
| `CrashModuleVMAttackPatch` | `CrashModule.Update`<br>`CrashModule.Draw` | Prefix<br>Transpiler | Injects the custom VM attack flow and replaces the error message |
| `NodeIconRenderPatch` | `DisplayModule.GetComputerImage` | Postfix | Texture replacement for node icons (`SetNodeIcon`) |
| `ScreenBleedWCCPatch` | `OS.Update`<br>`ActiveEffectsUpdater.CancelScreenBleedEffect` | Postfix<br>Postfix | WCC screen-alert timing and drawing; links up with vanilla cancellation |
| `TitleBannerPatches` | `OS.LoadContent` / `OS.Update` / `OS.Draw` | Postfix | Creation and drawing of the title banner (`ShowTitle`) |
| `CustomColorPatch` | `ThemeManager.Update` | Prefix | Per-frame refresh of dynamic colours (CustomColor) |
| `MusicManagerSuppressPatch` | `MusicManager.playSong`<br>`MusicManager.playSongImmediatley`<br>`MusicManager.transitionToSong` | Prefix | Swallows vanilla playback entries while PS runs |
| `PhaseSwiftAudioPatch` | `OS.Update` | Postfix | PS audio buffer advance and crossfade |
| `PhaseSwiftAudioVisualizerPatch` | `AudioVisualizer.Draw` | Prefix + Postfix | Visualiser layer hook-up |
| `PhaseSwiftCleanupPatch` | `MainMenu.resetOS` | Postfix | Clears PS runtime state and preset cache |
| `PhaseSwiftConnectionPatch` | `Programs.connect` | Prefix | Connection handling for PS-controlled nodes |
| `PhaseSwiftLayoutPatch` | `ThemeManager.switchThemeLayout` | Prefix | Layout protection (suppresses layout resets on scene switch) |
| `PhaseSwiftLayoutResetPatch` | `OS.Update` | Prefix | Tracks layout-reset timing |
| `PhaseSwiftVisualizationInjector` | `MediaPlayer.GetVisualizationData` | Prefix | Injects PS visualisation data |
| `PorthackAutoPatch` | `Hacknet.PortHackExe.Update` | Postfix (**reflective install**) | Triggers the heartbreak sequence for `AutoOnPorthack` |
| `PorthackHeartDisplayPatch` | `DisplayModule.doCommandModule` | Prefix | Heart-node display enhancement |
| `IRCLogInjector` | `FileEntry.init` | Prefix | Injects custom IRC logs (`<SkipVanillaIRCLogs>` can skip the vanilla ones) |
| `PatchAccountName` | `SavefileLoginScreen.Advance` | Prefix | Account-name handling on the login screen |
| `PatchStuxnetDrawFGamemodeMenu` | `SavefileLoginScreen.ResetForNewAccount`<br>`SavefileLoginScreen.Draw` | Postfix<br>Prefix (**conditional install**) | Stuxnet compatibility: gamemode-menu drawing; installed only when Stuxnet is present |

## How They Are Installed

Almost every patch is declared with a `[HarmonyPatch]` attribute and installed by `_harmony.PatchAll()` in the
main entry point. Two categories are installed **manually**:

- **internal types** (such as the vanilla `PortHackExe`): KE cannot reference them at compile time, so it uses
  runtime reflection (`AccessTools.TypeByName` + `AccessTools.Method` + `harmony.Patch`)
- **conditional patches** (such as the Stuxnet compatibility one): installed only when the corresponding
  third-party plugin is present; skipped otherwise

## Coexistence Notes

- **`MusicManagerSuppressPatch`** swallows the vanilla playback entries — relevant when running alongside
  third-party audio mods, see [Mod Compatibility](./mod-compat.md)
- The **`PhaseSwift*`** patches stay installed but only take effect while PS is running
  (gated by `PhaseSwiftManager.IsRunning`)
- **`PatchStuxnetDrawFGamemodeMenu`** treats Stuxnet as a **soft dependency** — zero hard references
- **`PorthackAutoPatch`** targets an internal type, hence the reflective install

## Technical Details

- All patches are injected through a single static `Harmony` instance (ID: `com.LDTchara.KernelExtensions`)
- On mod unload, `UnpatchSelf()` removes every patch (including the manually installed ones) — no residue
- The conflict surface with other mods, and the layout convention for the compatibility layer, are covered on
  [Mod Compatibility](./mod-compat.md)

---

## See Also

- [Home](./../index.md) – Return to main index
- [Harmony补丁 (中文)](./../../zh/components/harmony.md) – Chinese version
- [Mod Compatibility](./mod-compat.md) – conflict surfaces and the `Compat/` layout
- [Custom Color System](./../systems/custom-color.md) – the colour engine behind `CustomColorPatch`
