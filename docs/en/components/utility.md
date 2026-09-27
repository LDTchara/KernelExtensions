# Utility Classes

KernelExtensions provides a set of static utility classes for **mod authors** to call from code.

!!! info "Who this page is for"
    These are **C#-level APIs** — they target people writing plugins/mods.
    **Pure extension authors normally never need this page**: everything on the extension side is done
    through XML (actions, configuration, file layout) and needs no code. Only an extension author who also
    builds their own private plugin takes on the mod-author role as well.

## ActionHelper

- Path: `KernelExtensions.Utilities.ActionHelper`
- Method: `ExecuteActionFile(OS os, string actionFilePath, string extensionRoot)`
- Purpose: Loads and executes an action file, unifying the action execution logic between `CustomTrialExe` and the VM attack system.

## MusicPathResolver

- Path: `KernelExtensions.Utilities.MusicPathResolver`
- Method: `ResolveMusicPath(string musicPath, string extensionRoot)`
- Purpose: resolves a music string from configuration into a path `MusicManager` can load. Plain-filename lookup order:
  (1) extension root, (2) the extension's `Music/`, (3) `Content/DLC/Music`, (4) treated as a vanilla music name (`Content/Music`).
- **When the string contains a path separator** (e.g. `Music/Bit(Ending)`): it first checks whether that file really exists
  under the extension directory — if so it returns the extension path; **if not it passes the value through unchanged to
  vanilla resolution**, so path-style values can also point at vanilla tracks.
- The returned value **keeps whatever extension the caller wrote** (`.ogg` is not stripped); when omitted, FNA's
  `SongReader.Normalize` fills it in.

## SoundHelper

- Path: `KernelExtensions.Utilities.SoundHelper`
- Method: `PlaySound(OS os, string soundPath, float volume, float pitch, float pan)`
- Purpose: Plays a WAV sound effect file from within the extension directory.

## FlowColorHelper

- Path: `KernelExtensions.Utilities.FlowColorHelper`
- Methods: `GetFlowingRainbowColor(float position, float baseTime)`, `HslToRgbSimple(double h, double s, double l)`
- Purpose: Provides time‑based flowing rainbow colour calculations, used for the main menu watermark and other dynamic rainbow colour needs.

---

## See Also

- [Home](./../index.md) – Return to main index
- [工具类 (中文)](./../../zh/components/utility.md) – Chinese version