# Utility Classes

KernelExtensions provides a set of static utility classes for use by extension developers in their code.

## ActionHelper

- Path: `KernelExtensions.Utility.ActionHelper`
- Method: `ExecuteActionFile(OS os, string actionFilePath, string extensionRoot)`
- Purpose: Loads and executes an action file, unifying the action execution logic between `CustomTrialExe` and the VM attack system.

## MusicPathResolver

- Path: `KernelExtensions.Utility.MusicPathResolver`
- Method: `ResolveMusicPath(string musicPath, string extensionRoot)`
- Purpose: Resolves a music string from configuration into a path recognised by `MusicManager.transitionToSong`. Supports plain filenames, extension directories, DLC music, and vanilla music.

## SoundHelper

- Path: `KernelExtensions.Utility.SoundHelper`
- Method: `PlaySound(OS os, string soundPath, float volume, float pitch, float pan)`
- Purpose: Plays a WAV sound effect file from within the extension directory.

## FlowColorUtils

- Path: `KernelExtensions.Utility.FlowColorUtils`
- Methods: `GetFlowingRainbowColor(float position, float baseTime)`, `HslToRgbSimple(double h, double s, double l)`
- Purpose: Provides time‑based flowing rainbow colour calculations, used for the main menu watermark and other dynamic rainbow colour needs.

---

## See Also

- [Home](./../index.md) – Return to main index
- [工具类 (中文)](./../../zh/components/utility.md) – Chinese version