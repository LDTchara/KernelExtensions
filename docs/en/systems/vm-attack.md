# VM Attack System

The **VM Attack** is a configurable module that simulates a system crash and forces the player to interact with the real file system to remove the lock.  
It is triggered by the custom action `<LaunchVMAttack>` and its configuration is entirely XML‑driven.

---

## Overview

- Triggered via: `<LaunchVMAttack ConfigPath="VMATK/MyAttack.xml" />`.
- Configuration path: given directly by `ConfigPath`, **relative to the extension root** (any sub‑directory, not just `VMATK/`).
- Supports three recovery modes, combined with fake files, system logs, guide text, and interactive buttons to form a complete recovery flow.

---

## Basic XML Structure

Below is a short example. See [MyAttack_Example.xml](https://github.com/LDTchara/KernelExtensions/blob/main/XMLExamples/MyAttack_Example.xml) for a full example.

```xml
<VMAttackConfig>
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
| `Mode` | ❌ | Recovery mode: `FileDeletion` (default), `FileExists`, or `Password`. |
| `Password` | `null` | Password required in Password mode. |
| `EnableHelpDocButton` | `false` | Whether to show a "Help document" button in Password mode (Notepad on Windows / in‑screen text on Linux). |
| `EnableTerminalButton` | `false` | Whether to show a "Terminal" button in Password mode. |
| `EnableHelpButton` | `false` | **Split apart, kept for compatibility only**: `true` equals enabling both of the above (old behaviour). |
| `ErrorMessage` | `"ERROR: Critical boot error loading \"VMBootloaderTrap.dll\""` | Custom error message displayed during the crash. |
| `SystemLogFiles` | `null` | Multiple text file paths; each is echoed line‑by‑line in monospace font on the recovery screen. |
| `SystemLogPauseBetween` | `2.0` | Pause in seconds between two system log files. |
| `GuideText` | `null` | List of guide text lines; each line is automatically prefixed with `> `. |
| `ActionOnGuideTextStart` | `null` | Action file executed when the guide text starts displaying. |
| `EnableGuideReadFlag` | `false` | If true, after the first complete read the guide text is shown in full instantly on subsequent entries. |
| `ButtonText` | `"Proceed"` | Text displayed on the interaction button. |
| `HelpFile` | `null` | Path to a help file; when the button is clicked it is copied to the save directory and opened. |
| `SuccessMusic` | `null` | Music played after the attack is successfully removed. |
| `FakeFiles` | `null` | List of fake files generated in the save base directory when the attack triggers. Each entry takes: `Path` (relative to the save base directory), `Size` (byte size of the generated zero-filled file), `Source` (optional; copy this file from the extension instead). |
| `CheckFilePath` | `null` | Target path for file‑check modes (relative to the save base directory). |
| `CheckFilePattern` | `null` | Optional, in FileExists mode: a **path relative to the extension root of a reference file**; the target file must match it **byte for byte** (not a regex). |

!!! note "NONE convention"
    String config fields follow the `NONE` convention: `NONE` or an empty value = disabled / default fallback; omitting the element = use the default value.

---

## Paths and Naming

`ConfigPath`, together with every file path inside the configuration (`HelpFile`, `SystemLogFiles`,
`FakeFiles[].Source`, `CheckFilePattern`, …), is **relative to the extension root**.

!!! warning "Case must match the file on disk"
    Linux file systems are **case‑sensitive**: `VMATK/MyAttack.xml` and `vmattk/myattack.xml` are
    two different files. Always match the on‑disk casing—it happens to work on Windows,
    but the same content will fail to find the file on Linux.

!!! warning "Paths must stay inside the extension folder"
    `..` traversal and absolute paths (including Windows drive letters) are **rejected**; such a path
    is treated as invalid — reads are skipped and logged, and **deletions are not performed**
    (nothing outside the extension is touched).

!!! note "How the infection Flag is derived"
    The Flag is derived from `ConfigPath` and **keeps the path structure intact**:

    - `VMATK/MyAttack.xml` → `Kernel_VMInfected_VMATK/MyAttack.xml`

    This keeps the Flag **losslessly reversible** back into the config path after a crash.
    Replacing slashes with underscores would make `VMATK/A_B.xml` and `VMATK/A/B.xml` collide into
    the same Flag and misjudge each other as already infected, so no replacement is done.
    Backslashes are normalised to forward slashes, and a leading `./` or `/` is stripped.

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
- On success, success music plays, the system reboots, and the infection is cleared.
- Two optional helper buttons:
    - **Help document** (`EnableHelpDocButton`) — on Windows the help file is copied to the save
      directory and opened in Notepad; **on Linux no external program is launched** — the help text
      is appended line by line to the recovery screen instead (matching vanilla Unix behaviour).
    - **Terminal** (`EnableTerminalButton`) — opens a system terminal, with the attack directory as its working directory.

!!! note "How the terminal is chosen on Linux"
    Linux has no single "default terminal" standard (the `gsettings` key the vanilla game relies on
    was deprecated after GNOME 3), so KE probes the following in order, **using only what exists**:

    `$TERMINAL` env var → `xdg-terminal-exec` → `x-terminal-emulator` → desktop‑shipped terminals
    (gnome-terminal / konsole / xfce4-terminal / mate-terminal / lxterminal / cinnamon-terminal)
    → common third‑party ones (tilix / terminator / alacritty / kitty / wezterm / foot / st / urxvt) → `xterm`.

    The vanilla implementation is used only as a last resort. With `<Debug>true` the log records which one was used.

### Button Layout by Platform

| Platform | Recovery mode | Buttons |
|----------|---------------|---------|
| Windows | Password | Submit + Help doc + Terminal (last two gated by switches) |
| Windows | FileDeletion / FileExists | Single primary button (help copy + Notepad + terminal + crash) |
| Linux | Password | Submit + Help doc + Terminal |
| Linux | FileDeletion / FileExists | Three: README (help shown in‑screen) / Terminal / Crash VM |

---

## Guide Text Special Syntax

Guide text supports inline control markers wrapped in `||`:

- `||Px.x||` — Pause for x.x seconds (e.g. `||P0.5||`)
- `||Sx.x||` — Change character speed to x.x sec/char (e.g. `||S0.05||` for faster)
- `||SR||` — Reset character speed to default (0.12 sec/char)

Markers can appear anywhere in a line and are never displayed. Speed is reset to default at the start of each line.

---

## Attack Flow

1. `<LaunchVMAttack ConfigPath="VMATK/MyAttack.xml" />` is invoked.
2. Fake files are generated, the Flag `Kernel_VMInfected_<relative path>` is added and saved (e.g. `Kernel_VMInfected_VMATK/MyAttack.xml`).
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
<LaunchVMAttack ConfigPath="VMATK/MyAttack.xml" />
```

`ConfigPath` is the configuration file path **relative to the extension root** (e.g. `VMATK/MyAttack.xml`).
A separate `ConfigName` is no longer needed—the path itself is the identity, and the infection Flag is
derived from it.

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

- [Configuration Files](./../components/configuration.md) – All configuration file references