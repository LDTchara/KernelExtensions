# Changelog

## 0.7.0 — Unreleased

> In development · 2026
>
> **Baseline**: `0.6.0` (2026-05-12) → `dev`
> Scale: **29 → 128 `.cs` files**, roughly **4,900 → 21,700 lines**; 39 new Action / Executable
> registrations (13 → 52).

### ⚠️ Breaking changes

> Changes that affect **existing extensions**. A workaround is given per item.

- **PortHackV2 / ProxyKiller flag prefixes dropped the `Custom` prefix**
  - `CustomPortHackV2_xxx` → **`PortHackV2_xxx`**
  - `CustomProxyTuner_xxx` → **`ProxyKiller_xxx`** (this program was also renamed during the
    cycle: `ProxyTuner` → `ProxyKiller`)
  - Impact: rename any flag names in your extension that use the old prefix. **Both programs are new
    in 0.7**, so this only affects usage from within this development cycle.
- **PortHackV2 removed the `<Usable>` config option**
  - Usability is now decided by **runtime prerequisites** (the `<Require*>` family plus the program's
    own lock state); the option is no longer needed.
- **PortHackV2 removed the root-level `OnFail`**
  - The root level has no randomness — once the total time elapses it always plays the success
    sequence. Whether a protocol broke is expressed by **that protocol's own `OnFail`**.
- **`ChangeThemeColor` attribute `Locate` renamed to `Fields`**
  - `<ChangeThemeColor Locate="…" />` → `<ChangeThemeColor Fields="…" />`.
- **Unified action-point syntax (planned)**
  - Action points are currently written inconsistently across systems (some use a `file` attribute,
    others use element text content). This release plans to unify them so every action point can
    **either hold inline actions or a path**.
  - The unification will **keep `file` attribute compatibility**, so existing configs **do not need to
    change**; see the "Inline actions" entry under New Features.

**Not affected** (verified item by item): all 13 registrations from `0.6.0` (`FailTrial` /
`AttackAircraft` / `TerminalWrite` …) are **still present** — none removed or renamed; the 7 moved
files were **renamed and relocated**, with their registration strings frozen.

### New Features

- **PortHackV2 Cracker (`#PORTHACKV2#`)**
  - A configurable remake of vanilla `PortHack`: cracking flow, durations, appearance, prerequisites
    and finishing actions are all driven by XML.
  - Protocols run **in parallel**, each staggered by its own `StartDelay`; every protocol rolls its own
    outcome, but both branches look identical (only the duration differs).
  - Two levels of action points — root and per-protocol (`OnStart` / `OnComplete` / `OnFail`).
  - Optional prerequisites: firewall, proxy overload, specific ports, and the number of
    **already-cracked ports** on the target (`auto` aligns with the node's own requirement).
  - Five Platonic solid wireframe animations rendered off-screen; every solid spins around its own
    rotational symmetry axis, and that axis is kept vertical.
  - See [PortHackV2 Cracker](./../systems/port-hack-v2.md).
- **ProxyKiller Resident Accelerator (`#PROXYKILLER#`)**
  - Speeds up **every shell that is currently overloading**, so proxies crash sooner. It does **not**
    overload any proxy itself — running a shell requires admin on the target, and that guard rail is
    not removed.
  - The multiplier is driven linearly by the RAM ramp (starting at 50MB); it re-scans every frame, so
    shells started after it are accelerated just the same.
  - Background is a self-drawn upward data stream; the bottom status text has three levels:
    `Spinning up...` → `Overdrive` → `Spinning down...`.
  - See [Executables](./../components/executables.md).
- **Inline actions in action points**
  - A system's action points can hold `<Actions>` / `<ConditionalActions>` directly, or point at an
    action file — and both can be combined (**file first, then inline**).
  - Currently used by PortHackV2's five action points; it will be rolled out to all systems (see the
    breaking-changes section above).
  - Unknown element or condition names log a clear warning and are skipped — they **do not** fail the
    whole action set.
- **Nine atomic target-computer Actions**
  - Ports: `PortOpen` / `PortClose` / `PortFirewallAdd` / `PortFirewallSolve` / `PortFirewallReroll` /
    `PortFirewallConfigure` / `PortProxyAdd` / `PortProxySolve` / `PortProxyReset`
  - One Action does one thing, so they compose freely inside action points.
- **`ListPorts`: list every registered port protocol**
  - Output looks like `[22] ssh - SSH (vanilla)`, so you can confirm which protocol name to use while
    writing config.
- **Phase Swift scene-switching system (`#PHASESWIFT#`)** — multi-scene topology + multi-track music +
  theme / blacklist / persistence
- **Clock timer** (`ClockStart` / `ClockStop`) — run a set of Actions on an interval
- **Custom ending `StartEnding`** — title cards / speech / credits, pacing and text fully configurable
- **Title banner `ShowTitle`** and **full-screen warning `StartScreenBleedEffectWCC`**
- **Node icons `SetNodeIcon`**, **node connection control `LinkControl*` trio**, **RAM display `RamDisplay`**
- **`BreakHeart`** — a recreation of the vanilla heart ending, with `OnHeartbreak` / `OnComplete` action points
- **Effects previewer (`#EFFECTS#`)** and **live wallpaper test (`#WPTEST#`)** — two debug programs

### Improvements & Optimisations

- **Unified colour string parsing**: named colours, `#RGB` / `#RRGGBB` / `#AARRGGBB` (alpha first),
  `R,G,B[,A]`, CustomColor preset names and dynamic colours (`LDTchara` / `Rainbow` / `Gradient:…`)
  all go through one parsing chain; the scattered `ColorConverter` fallbacks that silently failed
  under FNA have been removed. See the [Custom Color System](./../systems/custom-color.md).
- **Unified configuration conventions**: string options use `NONE` (case-insensitive) or empty for
  "disabled"; numeric options treat **negative as "use the default / inherit / unlimited"** (`NaN` /
  `Infinity` likewise fall back to the default).
- **Unified log levels**: Debug (source-level troubleshooting, off by default) / Info (action-level
  results) / Warn (recoverable) / Error (should not happen); log text is plain ASCII English while
  player-facing terminal output is localised.
- **In-extension path confinement**: author-supplied paths all go through `KEPath`; `..` traversal,
  absolute paths and drive letters are rejected and logged.
- **Action layer normalisation**: all 31 Actions moved to the `KEAction` base class → XML attribute
  names are now **case-insensitive**; files are grouped by system (`Aircraft/` `PhaseSwift/`
  `CustomTrial/` `VMAttack/` `Clock/` `Ending/` `Node/` `Misc/`).

### Still under test

Implemented but not yet verified in-game; details will be filled in before release:

- PortHackV2 and ProxyKiller (**all behaviour pending in-game verification**)
- The nine atomic target-computer Actions and `ListPorts`
- The inline-action forms
- Unified colour parsing and `ChangeThemeColor`
- The node connection control action trio
- Phase Swift: configurable topology restore on stop, custom loop points for music phases
- Further work on the built-in text localisation system
- Linux support for the VM attack restore module
- Third-party mod compatibility (audio chain coexistence, etc.)
- The `PortHackCubeSequence` preview in the effects previewer (look does not yet match vanilla,
  marked **WIP**)

## 0.6.0 — Aircraft Daemon, Watermark & Stability Enhancements

> Pre‑release · 2025

### New Features
- **Aircraft Daemon System (FlightDaemon)**
  - Fully replaces the vanilla `AircraftDaemon` and is configured directly in the target computer XML: `<FlightDaemon FallDuration="90" OnFailed="Actions/failed.xml" OnSaved="Actions/saved.xml" />`.
  - Configurable crash duration (`FallDuration`) with a linear countdown in immediate fall mode.
  - Executes `OnFailed` / `OnSaved` action files on crash or repair, ideal for narrative integration.
  - Provides a global altimeter overlay; activate with `ShowAircraftOverlay` / `HideAircraftOverlay` to see the aircraft’s altitude in real time without being connected. The overlay auto‑hides on crash.
  - Added the `AttackAircraft` action (with optional `FallDuration`, overriding the daemon’s setting) and the `UploadAircraftSysFile` repair action.
  - Core implementation contributed by **April_Crystal** — special thanks!
- **Main Menu Rainbow Watermark**
  - Displays `+ KernelExtensions <version>` in the top‑left of the main menu, with colours flowing smoothly over time — no jitter or looping.
  - Positioned to the right of the ZeroDayToolKit watermark to avoid overlap.
  - The watermark disappears automatically when the extension is unloaded, and Harmony patches are cleaned up uniformly.
- **New Custom Action: `RenameNode`**
  - Renames a node by its ID; the change takes effect immediately and persists in saves.

### Improvements & Optimisations
- **Harmony patch management**: uses a single static Harmony instance, and all patches are cleanly removed via `Unload()` when the extension exits.
- **Aircraft button logic**: the “Exit..” button in `FlightDaemon` has been corrected to “Disconnect” and now properly executes a disconnection.
- **Documentation overhaul**: detailed docs have moved to the GitHub Wiki, with bilingual navigation; new pages include the Extension Author Guide, Configuration Files reference, and more. The README has been streamlined accordingly.
  (Note: that Wiki was retired during 0.7 development; the docs are now served by the MkDocs site under `docs/` in the main repository.)

### Bug Fixes
- Fixed a `KeyNotFoundException` crash in `AttackAircraft` when the target computer lacks a `FlightDaemon`.
- Fixed a potential division‑by‑zero when `CrashDelay == 0`; the logic now triggers an instant crash.
- Fixed an Access Violation in `CustomTrialExe.OnComplete` caused by calling `MusicManager.stop()` during certain audio states.
- Fixed a typo: `AircraftFallStartsImmediatley` → `AircraftFallStartsImmediately`.
- Fixed static dictionary not being cleaned up after aircraft crash, which could lead to a memory leak.
- Fixed the `FlightDaemon` continuing to update altitude data after disconnecting from the target; updates are now automatically unsubscribed.

---

## 0.5.0 — VMAttack System

> Pre‑release · 2025

### New Features
- **Configurable VM Attack System**
  - Added the `LaunchVMAttack` Action to trigger custom virtual machine crash attacks from action files.
  - Supports three recovery modes: `FileDeletion` (delete a specified file), `FileExists` (create a specified file), `Password` (enter a password).
  - Attack behaviour is defined via XML configuration files in the `VMATK/` directory, including custom error messages, system log texts, guide dialogues, help files, fake file lists, success music, etc.
  - The recovery interface (`FakeRecoveryModule`) simulates terminal‑style output with scrolling system logs, character‑by‑character guide text, interactive buttons/password input, and multi‑language prompts.
  - Guide text supports inline control markers: `||Px.x||` pauses, `||Sx.x||` speed changes, `||SR||` reset to default speed, plus an optional “read‑skip” feature.
  - An action file can be executed synchronously when guide text begins playing (`ActionOnGuideTextStart`).
  - Seamlessly integrated into the vanilla crash flow: a Harmony patch injects an error state at line 50 of the system boot log, and after 15 seconds automatically transitions to the custom recovery module.
  - After the attack is lifted, a black‑screen reboot is performed and success music is played.
- **New Action: `PlaySound`**
  - Added the `PlaySound` Action for playing custom sound effects (WAV format) from the extension directory.
  - Provided the public `SoundHelper` utility class for convenient programmatic use.
- **Technical Improvements**
  - Extracted `MusicPathResolver` as a public utility class, unifying music file path resolution within extensions (with multi‑level fallback), fixing an issue where music could not be played when the extension folder name differed from the extension name.
  - Extracted `ActionHelper` as a public utility class, standardising action file execution logic shared between `CustomTrialExe` and the VM system, improving stability and maintainability.

### Other
- Adjusted various internal details and optimised coupling between modules to lay a foundation for future extensions.

---

## 0.4.6

> Pre‑release · 2025

- Fixed incorrect path resolution in music path parsing.

---

## 0.4.5 — Force Fail & Bug‑Fix Collection

> Pre‑release · 2025

### New Features
- **Force Fail Action (`FailTrial`)**
  - Added `FailTrialAction`, which can be invoked directly from an action file to force the current trial to fail immediately (entering Outro with "Failed" text). Supports `Delay` and `DelayHost` for delayed execution.

### Bug Fixes
- Fixed the title not changing on trial failure: all failure paths now correctly set `currentPhaseIdx = -1`, so the title displays "Failed".
- Fixed the title disappearing on exit: added temporary text logic for the `Exiting` state in `DrawPhaseTitle`, so the title continues to show "Complete" or "Failed" during fade‑out.
- Fixed extraneous terminal output on failed exit: the failure `Result` is now uniformly set to `Success`, and `trialSucceeded` independently controls music stopping.
- Fixed mail destruction radial lines being affected by frame rate: introduced `MAIL_LINE_INTERVAL` (1/60 s) to control line generation frequency.
- Fixed the completion sound playing when the focus effect was disabled: `glowSound` now only plays when `EnableTrialCompleteFocus = true`.

### Improvements
- Replicated the original SpinUp animation easing curves: `DrawSpinningUp` now fully matches the original DLC trial's line progress algorithm, making the animation richer and more natural.

---

## 0.4.4 — Node Persistence & Effect Restoration

> Pre‑release · 2025

### Core System Enhancements
- **Node Deletion Persistence & Restoration**
  - Added `CustomTrialNodeStorage` global storage to record deleted node indices during trials.
  - Save/load automatically persists/restores the deletion state (via `SaveEvent` and `CustomTrialSaveExecutor`).
  - Provided the `RestoreCustomTrialNodes` custom Action to restore nodes at any time with highlight flashes and effects.
- **Action File Execution Mechanism Optimisation**
  - `ExecuteActionFile` now supports both `ConditionalActions` and `Actions` standard formats, compatible with vanilla and Pathfinder conventions.
  - Uses `EventExecutor` + `ActionsLoader` to ensure custom Actions execute correctly.
- **Terminal Interaction Actions**
  - `TerminalWriteAction`: writes text to the terminal (supports `text` attribute or element content, and `Delay`).
  - `TerminalFocusAction`: full‑screen terminal focus (darken mask + expanding border) with independent `Duration`, `BorderDuration`, `FadeInDuration` parameters.
- **Theme Switch Configuration**
  - XML config items `ThemeToSwitch` (preset name or custom path) and `ThemeFlickerDuration`, automatically switching the theme before animations begin.

### Visual & UI Polish
- **Mail explosion effect enhancement**: added radial line generation and multi‑stage delayed red circle expansion.
- **Node destruction effect restoration**: added shockwave and red fade circles when nodes disappear; higher security levels produce stronger effects.
- **Status title display**: Flickering / WaitAfterDestruction / MailIconDestroy phases show localised "Initializing" text.
- **Terminal focus effect**: optional full‑screen mask + expanding terminal border at phase start / trial completion.
- **Exit fade animation**: all UI elements fade out smoothly with the `fade` value on exit.
- **Background grid dynamic effect**: `HexGridBackground` dynamically updates colour intensity over time.

### Feature & Flow Optimisation
- `OnStart` and `OnAnimationComplete` coexist: `OnStart` fires immediately after clicking the button; `OnAnimationComplete` fires after all animations finish.
- **Configurable program name**: added `ProgramName` field in XML.
- **Post‑trial auto‑connect**: added `ConnectTarget` and `StopMusicOnConnect` config items.
- **Phase reset custom text**: `PhaseConfig` gains `ResetText` and `ExecuteOnPhaseStartOnReset`.
- **Dynamic RAM reduction**: added `DynamicRamReduction` option, which automatically calculates the minimum window height based on visible controls.

---

## 0.4.3

> Pre‑release · 2025

- Reverted to `MusicManager` for audio playback.

---

## 0.4.2

> Pre‑release · 2025

- Applied Harmony patch to fix relative path issues with `MusicManager`.

---

## 0.4.1

> Pre‑release · 2025

- Music now automatically stops when a trial succeeds or fails.

---

## 0.4.0 — Node Persistence & Recovery

> Pre‑release · 2025

### New Features
- **Node deletion persistence & recovery**: nodes destroyed during a trial are saved with the game save and can be restored at any time with animated effects via the custom Action `RestoreCustomTrialNodes`.
- **Theme switch configuration**: supports automatic theme switching at the start of a trial, with a configurable flicker duration and support for preset names or custom theme files.
- **Post‑destruction delay**: added the `PostDestructionDelay` config item to insert a custom wait time between node destruction and mail icon explosion.
- **Action after animation complete**: the original `OnStart` was renamed to `OnAnimationComplete`, now executing after all animations finish.
- **Locked screen exit button**: when the trial is locked, a right‑click‑closable "Exit" button is displayed.
- **Music restoration on early exit**: if the player kills the program in `NotStarted` state, the previous background music is automatically restored.

### Improvements
- Fixed the top‑bar icon colour not being restored after mail icon explosion.
- Optimised theme switch and top‑bar colour save order.
- Adjusted the trigger timing of `OnAnimationComplete`.

---

## 0.3.5

> Pre‑release · 2025

- **Dynamic RAM Reduction**: after entering the cracking phase, ramCost can be linearly reduced from 190 to 88 over a configurable delay and duration. UI elements scale smoothly during the reduction.
- Simplified reflection checks with pattern matching; removed unused parameters and variables.

---

## 0.3.4

> Pre‑release · 2025

- Phase titles and subtitles are now displayed only in the centre of the program window and no longer output to the terminal.
- Extra failure information is no longer displayed when a trial fails.

---

## 0.1.0 ~ 0.3.3

> Pre‑release · 2025

- Early development build.

---

## See Also

- [Home](./../index.md) – Return to main index
- [更新日志 (中文)](./../../zh/guides/changelog.md) – Chinese version