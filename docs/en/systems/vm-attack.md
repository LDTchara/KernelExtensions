# VM Attack System

The **VM Attack** is a configurable module that simulates a system crash and forces the player to interact with the real file system to remove the lock.  
It is triggered by the custom action `<LaunchVMAttack>` and its configuration is entirely XML‑driven.

---

## Overview

- Triggered via: `<LaunchVMAttack ConfigName="MyAttack" />`.
- Configuration path: `VMATK/<ConfigName>.xml` in the extension root.
- Supports three recovery modes, combined with fake files, system logs, guide text, and interactive buttons to form a complete recovery flow.

---

## Basic XML Structure

Below is a short example. See [MyAttack.xml](https://github.com/LDTchara/KernelExtensions/blob/main/XMLExamples/MyAttack.xml) for a full example.

```xml
<VMAttackConfig>
  <ConfigName>MyAttack</ConfigName>
  <Mode>FileDeletion</Mode>
  <ErrorMessage>ERROR: Critical boot error loading "payload.dll"</ErrorMessage>
  <SystemLogFiles>
    <File>Docs/SystemLog1.txt</File>
    <File>Docs/SystemLog2.txt</File>
  </SystemLogFiles>
  <SystemLogPauseBetween>2.0</SystemLogPauseBetween>
  <GuideText>
    <Line>Hello, this is a guide.||P0.5||</Line>
    <Line>||S0.05||Follow the instructions.||P0.5||</Line>
    <Line>||SR||Good luck.</Line>
  </GuideText>
  <EnableGuideReadFlag>false</EnableGuideReadFlag>
  <ButtonText>Exit VM</ButtonText>
  <HelpFile>Docs/help.txt</HelpFile>
  <SuccessMusic>Music/Ambient/AmbientDrone_Clipped</SuccessMusic>
  <FakeFiles>
    <File Path="payload.dll" Size="512" />
  </FakeFiles>
  <CheckFilePath>payload.dll</CheckFilePath>
</VMAttackConfig>
```

---

## Global Configuration

| Element | Default | Description |
|---------|---------|-------------|
| `ConfigName` | required | Configuration name; must match the file name (without extension) and Flag suffix. |
| `Mode` | required | Recovery mode: `FileDeletion`, `FileExists`, or `Password`. |
| `Password` | `null` | Password required in Password mode. |
| `EnableHelpButton` | `false` | Whether to show a "Help" button in Password mode. |
| `ErrorMessage` | `"ERROR: Critical boot error loading \"VMBootloaderTrap.dll\""` | Custom error message displayed during the crash. |
| `SystemLogFiles` | `null` | Multiple text file paths; each is echoed line‑by‑line in monospace font on the recovery screen. |
| `SystemLogPauseBetween` | `2.0` | Pause in seconds between two system log files. |
| `GuideText` | `null` | List of guide text lines; each line is automatically prefixed with `> `. |
| `ActionOnGuideTextStart` | `null` | Action file executed when the guide text starts displaying. |
| `EnableGuideReadFlag` | `false` | If true, after the first complete read the guide text is shown in full instantly on subsequent entries. |
| `ButtonText` | `"Proceed"` | Text displayed on the interaction button. |
| `HelpFile` | `null` | Path to a help file; when the button is clicked it is copied to the save directory and opened. |
| `SuccessMusic` | `null` | Music played after the attack is successfully removed. |
| `FakeFiles` | `null` | List of fake files generated in the save base directory when the attack triggers. |
| `CheckFilePath` | `null` | Target path for file‑check modes (relative to the save base directory). |
| `CheckFilePattern` | `null` | Optional regex; in FileExists mode the file content must match this pattern. |

---

## Recovery Mode Details

### FileDeletion
- Fake files are generated. The player must **manually delete** the specified file to recover.
- The button exits the game; after deleting the file and restarting the game, the attack is automatically removed.

### FileExists
- The target file is **not** created. The player must **manually create** it to recover.
- After creating the file and restarting the game, the attack is automatically removed.

### Password
- A password input field appears on the recovery screen; the correct password must be entered.
- A help button can optionally be provided to copy a help file and open a terminal.
- On success, success music plays, the system reboots, and the infection is cleared.

---

## Guide Text Special Syntax

Guide text supports inline control markers wrapped in `||`:

| Marker | Effect | Example |
|--------|--------|---------|
| `\|\|Px.x\|\|` | Pause for x.x seconds | `\|\|P0.5\|\|` |
| `\|\|Sx.x\|\|` | Change character speed to x.x sec/char | `\|\|S0.05\|\|` (faster) |
| `\|\|SR\|\|` | Reset character speed to default (0.12 sec/char) | `\|\|SR\|\|` |

Markers can appear anywhere in a line and are never displayed. Speed is reset to default at the start of each line.

---

## Attack Flow

1. `<LaunchVMAttack ConfigName="MyAttack" />` is invoked.
2. Fake files are generated, the Flag `Kernel_VMInfected_<ConfigName>` is added and saved.
3. Pre‑crash effects (chromatic flash) are shown, then the original `crash` is triggered after a delay.
4. Vanilla blue screen → black screen → boot log → error injected at line 50 → 15‑second error state.
5. Custom recovery screen:
   - System log phase (monospace line‑by‑line output)
   - Guide text phase (character‑by‑character typing with speed control and pauses)
   - Interaction phase (button or password input)
6. After the player completes the required action, the attack is lifted and the system reboots.

---

## Related Actions

### Trigger Attack: `LaunchVMAttack`

```xml
<LaunchVMAttack ConfigName="MyAttack" />
```

`ConfigName` corresponds to the file name (without `.xml`) in the `VMATK/` directory.

### Automatic Clean‑up on Recovery

When the attack is lifted:
- The infection Flag is removed.
- The read‑flag and guide‑action‑done flag are cleared.
- All fake files are deleted.
- Success music plays (if configured).
- A black‑screen reboot restores normality.

---

## Multi‑language Support

UI texts such as password match/mismatch prompts and help button text are available in 10 languages (same list as the Custom Trial system).

---

## See Also

- [Home](./../index.md) – Return to main index
- [虚拟机攻击系统 (中文)](./../../zh/systems/vm-attack.md) – Chinese version
- [Misc](./../guides/misc.md) – Other things not mentioned on the major system pages
- [Configuration Files](./../components/configuration.md) – All configuration file references