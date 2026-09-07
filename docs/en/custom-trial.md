# Custom Trial System

The **Custom Trial** is a flexible, XML‑driven multi‑phase challenge framework.  
It replaces the original hardcoded DLC trial and provides fully configurable visual effects, missions, timers, and dynamic UI element sequences.

---

## Overview

- Activated by a Flag starting with `CustomTrial_` (e.g. `CustomTrial_MyTrial`).
- Loads its configuration from `Trial/<ConfigName>.xml` in the extension root.
- The executable `CustomTrial` must be present in the player's `bin/` folder.

---

## Basic XML Structure

Below is a short example configuration file. For a more complete configuration file, please refer to [ExampleTrial.xml](https://github.com/LDTchara/KernelExtensions/blob/main/XMLExamples/ExampleTrial.xml).

```xml
<TrialConfig>
  <ProgramName>My Trial</ProgramName>
  <SpinUpDuration>5.0</SpinUpDuration>
  <EnableFlickering>true</EnableFlickering>
  ...
  <Phases>
    <Phase id="0">
      <Title>Phase One</Title>
      <Subtitle>Get root and erase logs</Subtitle>
      <DescriptionText>Docs/phase1_desc.txt</DescriptionText>
      <MissionFile>Missions/phase1_mission.xml</MissionFile>
      <Timeout>120</Timeout>
      <Music>Music/Ambient/dark_drone_008</Music>
      <OnPhaseStart file="Actions/phase1_start.xml" />
      <OnComplete file="Actions/phase1_complete.xml" />
      <OnFail file="Actions/phase1_fail.xml" />
      <EnableResetOnFail>true</EnableResetOnFail>
      <ResetText>Docs/phase1_reset.txt</ResetText>
    </Phase>
    <!-- more phases -->
  </Phases>
</TrialConfig>
```

---

## Global Configuration

| Element | Default | Description |
|---------|---------|-------------|
| `ProgramName` | `"CustomTrial"` | Display name of the executable. |
| `SpinUpDuration` | `13.8` | Duration of the spin‑up animation (seconds). |
| `EnableFlickering` | `true` | Whether to enable UI flickering and node destruction effects after the spin‑up animation. |
| `FlickeringDuration` | `10` | Duration of the flickering phase (seconds). |
| `EnableNodeDestruction` | `true` | Whether to allow random node destruction during flickering. |
| `PostDestructionDelay` | `0` | Wait time (seconds) between the end of node destruction and the mail explosion (if enabled). |
| `EnableMailIconDestroy` | `true` | Whether to enable the mail icon explosion effect. |
| `MailIconDestroyDuration` | `3.82` | Duration of the mail explosion (seconds). |
| `MailPhaseDarkenEnabled` | `true` | Whether to darken the area outside the terminal during the mail explosion. |
| `EnablePhaseStartFocus` | `true` | Whether to show the terminal focus overlay when a phase starts. |
| `EnableTrialCompleteFocus` | `true` | Whether to show the terminal focus overlay when the trial is completed. |
| `ThemeToSwitch` | `null` | A preset theme name to switch to (e.g. `HacknetMint`) or a custom theme file path. |
| `ThemeFlickerDuration` | `2` | Flicker duration when switching themes (seconds). |
| `BackgroundColor` / `GlobalTimerColor` / `PhaseTimerColor` / `SpinUpColor` | `null` | Custom colours (supports names, `#RRGGBB`, or put my name in there). |
| `RamReductionDelay` | `5` | Delay in seconds before RAM reduction begins after phases start. |
| `RamReductionDuration` | `3` | Total duration of the RAM reduction process (seconds). |
| `DynamicRamReduction` | `false` | If true, `RamReductionDelay` and `RamReductionDuration` are ignored; `ramCost` is dynamically adjusted based on the height of currently displayed UI controls. It is recommended to set this to `true` to avoid potential visual issues. |
| `GlobalTimeout` | `0` | Total time limit for the entire trial (seconds). 0 means no limit. |
| `EnableGlobalTimer` | `false` | Whether to show the global countdown bar. |
| `OnGlobalFail` | `null` | Action file to execute when the global timer runs out. |
| `StartMusic` | `null` | Background music played before clicking "Begin Trial". |
| `TrialStartMusic` | `null` | Music played after clicking "Begin Trial". |
| `OnStart` | `null` | Action file to execute immediately after clicking "Begin Trial". |
| `OnAnimationComplete` | `null` | Action file to execute after all opening animations are completed. |
| `OnComplete` | `null` | Action file to execute after all phases are successfully completed. |
| `OutroText` | `null` | Text displayed in the terminal at the end of the trial (file path or inline text). Supports `%` for short pauses and `%%` for long pauses. |
| `ConnectTarget` | `null` | ID of the target node to automatically connect to after the trial. |
| `StopMusicOnConnect` | `true` | Whether to stop music before connecting. |
| `EnablePhaseTimer` | `true` | Whether to show per‑phase countdown bars. |

---

## Phase Configuration

Each `<Phase>` element can contain the following settings:

| Element | Default | Description |
|---------|---------|-------------|
| `id` (attribute) | required | Phase number. |
| `Title` | required | Phase title displayed in the program window. |
| `Subtitle` | optional | Subtitle below the title. |
| `DescriptionText` | required | Description text displayed character by character (file path or inline text). Supports `%` and `%%` pauses. |
| `MissionFile` | required | Path to the Hacknet mission XML. |
| `Timeout` | `0` | Phase time limit (seconds). 0 means no limit. |
| `Music` | `null` | Phase‑specific background music (overrides the global music). |
| `OnPhaseStart` | `null` | Action file to execute when the phase starts. |
| `OnComplete` | `null` | Action file to execute when the phase is completed. |
| `OnFail` | `null` | Action file to execute when the phase fails (timeout or trace timeout). |
| `EnableResetOnFail` | `false` | If true, resets the current phase on failure rather than ending the trial. |
| `ResetText` | `null` | Extra text displayed when the phase is reset (shown before the phase description). |
| `ExecuteOnPhaseStartOnReset` | `false` | If true, re‑executes the `OnPhaseStart` action when the phase is reset. |

---

## Related Actions

The following are custom actions related to the trial system. They are recommended for standalone invocation, but may also be placed at any action file invocation point (such as `OnStart`, `OnPhaseStart`, `OnFail`, etc.) to be called.

### Force Trial Failure: `FailTrial`

Immediately causes the currently running `CustomTrialExe` to enter a failure ending. If no trial is running, nothing happens.

```xml
<FailTrial />
```

Supports delayed execution:

```xml
<FailTrial Delay="3.0" DelayHost="cheat" />
```

### Restore Destroyed Nodes: `RestoreCustomTrialNodes`

Restores nodes destroyed in a previous trial one by one with animated effects. Requires `ConfigName`, which is the configuration name used when the `CustomTrial_` Flag was originally set.

```xml
<RestoreCustomTrialNodes ConfigName="ExampleTrial" />
```

Nodes reappear in sequence over 3 seconds, accompanied by highlight flashes and expanding circles.

### Internal Trial Action Hooks

Action files can be directly referenced in the trial configuration XML. These hooks fire at specific moments:

| Hook | Trigger Timing |
|------|----------------|
| `OnStart` | Executed immediately after the player clicks "Begin Trial". |
| `OnAnimationComplete` | Executed after all opening animations (spin‑up, flickering, node destruction, mail explosion, etc.) are completed. |
| `OnComplete` (global) | Executed after all phases are successfully completed. |
| `OnGlobalFail` | Executed when the global timer runs out. |
| `OnPhaseStart` | Executed at the start of each phase. |
| `OnComplete` (phase) | Executed when the current phase's mission is completed. |
| `OnFail` | Executed when the current phase times out or its trace timer runs out. |

All hooks specify the path to the action file via the `file` attribute (relative to the extension root), for example:

```xml
<OnPhaseStart file="Actions/MyPhaseStart.xml" />
```

---

## Special Effects & Visuals

- **Flickering and Node Destruction**  
  When enabled, nodes are randomly removed with impact effects. Destroyed nodes are persisted and can later be restored using the `RestoreCustomTrialNodes` action.

- **Mail Icon Explosion**  
  Completely reproduces the original effect, including radial lines, colour shifts, and a double explosion.

- **Terminal Focus Overlay**  
  Darkens the entire screen except the terminal and draws an expanding border. Can be enabled at phase start and trial completion.

- **Theme Switch**  
  At the start of the trial (after the spin‑up animation), the theme can be switched to a preset or custom file.

- **Dynamic RAM Reduction**  
  If enabled, the window height shrinks to just fit the remaining UI (title, timers, etc.), thereby reducing memory usage.

---

## Persistence

- Deleted node indices are saved in the global `CustomTrialNodeStorage` and written to save files.
- `CustomTrialSaveExecutor` reloads them when a save is loaded.
- Use the `RestoreCustomTrialNodes` action to restore these nodes with effects.

---

## Multi‑language Support

Buttons and labels (such as "Begin Trial", "Trial Locked", "Initializing", "Complete", "Failed", "Exit") are dynamically switched based on `Settings.ActiveLocale`.  
Currently supported languages: Chinese, Japanese, Korean, Russian, German, French, Spanish, Turkish, Dutch, English.  
Language‑file‑based localisation will be added in the future.

---

## See Also

- [Home](./index.md) – Return to main index
- [自定义试炼系统 (中文) ](./../zh/custom-trial.md) – 中文版
- [Misc](./misc.md) – Other things not mentioned on the major system pages
- [Configuration Files](./configuration.md) – Collection of all configuration files