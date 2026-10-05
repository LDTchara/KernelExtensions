# Utility Classes

KernelExtensions ships a set of static utility classes for **mod authors** to call from code.

!!! info "Who this page is for"
    These are **C#-level APIs** — they are for people writing plugins/mods.
    **Plain extension authors usually don't need this page**: everything on the extension side is done
    through XML (actions, configuration, file layout) with no code. Only an extension author who also
    writes their own private plugin takes on the mod-author role as well.

Every class lives in the namespace **`KernelExtensions.Utilities`**.

---

## 1. Configuration and Semantics

### ConfigValue

- Path: `KernelExtensions.Utilities.ConfigValue`
- Purpose: value-semantics helper implementing the **NONE convention** — a string option set to `NONE`
  (case-insensitive, vanilla `none` accepted) or left blank means **explicitly disabled**; omitting the
  attribute entirely means "use the default".

### ActionHelper

- Path: `KernelExtensions.Utilities.ActionHelper`
- Method: `ExecuteActionFile(OS os, string actionFilePath, string extensionRoot)`
- Purpose: loads and runs an action file; this unifies the action-execution logic shared by
  `CustomTrialExe` and the VM attack system.

---

## 2. Colour

### ColorHelper

- Path: `KernelExtensions.Utilities.ColorHelper`
- Purpose: the **shared colour utility** — centralised HSV/RGB conversion and hex colour parsing, used by
  `CustomColorPatch`, `PhaseSwiftManager`, `CustomTrialExe` and others.

<!-- ke:9.50 -->
!!! note "About colour formats"
    Colour fields do **not** all accept hex or named colours yet (see section 4 of the
    [Custom Color System](./../systems/custom-color.md)). Unifying parsing onto this class is planned.

### FlowColorHelper

- Path: `KernelExtensions.Utilities.FlowColorHelper`
- Methods: `GetFlowingRainbowColor(float position, float baseTime)`, `HslToRgbSimple(double h, double s, double l)`
- Purpose: time-based flowing rainbow colour, used by the main-menu watermark and other places that want
  a dynamic rainbow.

---

## 3. Audio

### SoundHelper

- Path: `KernelExtensions.Utilities.SoundHelper`
- Method: `PlaySound(OS os, string soundPath, float volume, float pitch, float pan)`
- Purpose: plays a **WAV** sound file inside the extension directory (path relative to the extension root,
  and it **must include the `.wav` extension**).

### MusicPathResolver

- Path: `KernelExtensions.Utilities.MusicPathResolver`
- Method: `ResolveMusicPath(string musicPath, string extensionRoot)`
- Purpose: resolves a music string from configuration into a path `MusicManager` can load. Plain-filename
  lookup order: (1) extension root, (2) the extension's `Music/`, (3) `Content/DLC/Music`,
  (4) treated as a vanilla music name (`Content/Music`).
- **When the string contains a path separator** (e.g. `Music/Bit(Ending)`): it first checks whether that
  file really exists under the extension directory — if so it returns the extension path; **if not it
  passes the value through unchanged to vanilla resolution**, so path-style values can also point at
  vanilla tracks.
- The returned value **keeps whatever extension the caller wrote** (`.ogg` is not stripped); when omitted,
  FNA's `SongReader.Normalize` fills it in.

---

## 4. Text and Localisation

### TextHelper

- Path: `KernelExtensions.Utilities.TextHelper`
- Purpose: **text rendering helper** — sanitises text against the font's actual character set.

  Hacknet loads fonts per language (`LocaleActivator.ActivateLocale` →
  `LocaleFontLoader.LoadFontConfigForLocale` reading `Locales/{locale}/Fonts/{locale}_FontXX`); a character
  the font lacks renders as a box or blank. KE therefore filters output text against the character set.

  > Practical rule: the vanilla fonts and common replacement fonts lack glyphs for Unicode symbols such as
  > `→`, so **in-game text uses ASCII** (write `->`, not `→`) — which is what this class supports.

### KELoc

- Path: `KernelExtensions.Utilities.KELoc`
- Purpose: KE's built-in **text localization**. Note it is **mainly for extension authors** (editing `KE-Locales.xml`
  text, no code required); the code API is a secondary use — see **[Localization (KELoc)](./keloc.md)** for the full picture.

---

## 5. Rendering

### KECube3D

- Path: `KernelExtensions.Utilities.KECube3D`
- Purpose: a **reflection bridge** to vanilla `Hacknet.Effects.Cube3D` (which is `internal`).
  A hand-written wireframe cube differs subtly from the vanilla look, so KE reflects into vanilla's
  `RenderWireframe` instead (vanilla's `Cube3D.Initilize` already ran at game start, so the static buffers
  are ready and the reflected call renders exactly like vanilla).

---

## 6. Logging

### KELog

- Path: `KernelExtensions.Utilities.KELog`
- Purpose: the unified logger, with a **four-level convention**:

  | Level | Use |
  |-------|-----|
  | `Debug` | KE source developers tracking internals (data/mechanism detail); off by default |
  | `Info` | Extension authors diagnosing behaviour (one line per action-level result) |
  | `Warn` | Recoverable / degraded / worth noting |
  | `Error` | Should not happen / feature failed |

  > For player-facing terminal feedback use `os.write`, not the logger.

---

## See Also

- [Home](./../index.md) – back to the main index
- [工具类（中文）](./../../zh/components/utility.md) – Chinese version
- [Custom Color System](./../systems/custom-color.md) – colour value rules (what `ColorHelper` serves)
- [Configuration Files](./configuration.md) – how the NONE convention shows up in config
