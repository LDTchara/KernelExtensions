# Extension Author Guide

This guide is for creators who want to use **KernelExtensions** features in their own Hacknet extensions.  
You will learn how to set Flags, write configuration files, invoke actions, and follow general conventions.

---

## 1. Overview

KernelExtensions currently provides three major configurable systems, all triggered via the Flag mechanism and driven by XML configuration files:

- **Custom Trial (CustomTrial)**: multi‑phase mission challenges with effects, timers, music, node destruction, etc.
- **VM Attack (VM Attack)**: simulates a virtual machine crash attack, forcing the player to interact with the real file system.
- **Aircraft Daemon (FlightDaemon)**: an aircraft daemon with configurable crash duration and repair/crash actions.

All systems **must** run as part of an extension; global plugin mode is not supported. All paths for configuration files, action files, etc. are relative to the **extension root**.

---

## 2. Using Flags

Both the trial and VM attack systems rely on Hacknet's native Flag mechanism. Generally, Flags can only be added/removed via the Action system — not through console commands.

### Custom Trial

In a mission or action file, set the Flag like this:

```xml
<RunFunction FunctionName="addFlags:CustomTrial_MyTrial" />
```

The Flag must start with `CustomTrial_`, followed by the configuration file name (without `.xml`).  
When the `CustomTrial` program runs, it finds and loads the corresponding `Trial/<name>.xml`. The Flag is automatically removed when the trial completes successfully.

### VM Attack

Use the custom action `LaunchVMAttack` directly, passing the config file name (without `.xml`):

```xml
<LaunchVMAttack ConfigName="MyAttack" />
```

The system loads `VMATK/<ConfigName>.xml` and triggers the attack. The Flag is managed automatically — no manual removal needed.

### Aircraft Daemon

No extra Flags are required. Just add `<FlightDaemon>` to the target computer's XML (see [Aircraft Daemon System](./aircraft.md)). Attack and repair are triggered via `AttackAircraft` and `UploadAircraftSysFile` actions.

---

## 3. Paths & File Conventions

- All paths in `file` attributes are **relative to the extension root**.  
  For example, `Actions/MyAction.xml` resolves to `Extensions/YourExtension/Actions/MyAction.xml`.
- Description text (`DescriptionText`) and guide text (`GuideText`) can be either a file path or inline text (inline text is recommended for GuideText).  
  If the value ends with `.txt` or another file extension, the system attempts to read it as a file; if the file is not found, it is treated as plain text.
- Supports `%` for short pauses and `%%` for long pauses (in trial descriptions and guide text, though for guide text it is recommended to use identifiers like `||PX.X||` and `||SX.X||` to control pauses and speed), and they can be used anywhere in the text.

---

## 4. Writing Mission Files

The `MissionFile` in a trial points to a standard Hacknet mission XML.  
**Important settings**:

```xml
<mission id="test" activeCheck="true" shouldIgnoreSenderVerification="false">
  ... objectives, targets, conditions ...
  <nextMission IsSilent="true"></nextMission>
  <!-- Note: the IsSilent attribute controls the *current* mission, but must be placed in the nextMission element (Matt's wonderful design) -->
</mission>
```

- `activeCheck="true"`: allows the mission to track completion status even when the player is not connected to the target. (Cases where this is set to `false` have not been tested at all, so it is strongly recommended to set it to `true` to avoid potential issues.)
- `<nextMission IsSilent="true">`: prevents automatic loading of the next mission (the trial controls its own flow). **Do not** put actual content inside `<nextMission>`.

---

## 5. Custom Colours

All colour configuration fields (e.g. `BackgroundColor`, `GlobalTimerColor`) support:

- Standard colour names (`Red`, `Blue`, `Cyan`, etc.)
- Hexadecimal format (`#FF0000`, `#0F0`)
- My name `LDTchara`: try it yourself.

If left empty or an unrecognised value is used, the game's current theme highlight colour will be used.

---

## 6. Music Paths

Music configuration fields (e.g. `StartMusic`, `TrialStartMusic`, `Music`, `SuccessMusic`) support the following formats, automatically resolved by `MusicPathResolver`:

1. **Plain filename** (e.g. `AmbientDrone_Clipped`):
   - Searches the extension root
   - Then the extension's `Music/` folder (the only difference from the original music handling mechanism; a retained fallback that LDTchara kept on a whim)
   - Then the DLC Music folder
   - Finally treats it as a vanilla music name
2. **Relative path** (e.g. `Music/MySong.ogg`): resolved relative to the extension root.
3. **DLC music**: e.g. `DLC/Music/snidelyWhiplash`, used directly.
4. The `.ogg` extension can be omitted.

---

## 7. Delayed Execution

Many custom Actions support `Delay` and `DelayHost` attributes for delayed execution.

- `Delay`: number of seconds to wait (positive).
- `DelayHost`: the ID of a host providing the delay service; that host must have a `FastActionHost` daemon.
- If `Delay` is 0 or omitted, the action runs immediately and no `DelayHost` is needed.
- Exception: `AddIRCMessage` supports `Delay` without `DelayHost`, and can be negative.

Example:
```xml
<ConditionalActions>
    <Instantly>
        <FailTrial />
    </Instantly>
</ConditionalActions>
```

---

## 8. Localisation

KernelExtensions currently has hardcoded multi‑language support for buttons and prompt text (Chinese, Japanese, Korean, Russian, German, French, Spanish, Turkish, Dutch, English).  
Language‑file‑based localisation is planned for a future release, which will allow extension authors to provide custom translations.

---

## 9. Quick Reference Links

- [Custom Trial System](./custom-trial.md)
- [VM Attack System](./vm-attack.md)
- [Aircraft Daemon System](./aircraft.md)
- [Actions](./actions.md)
- [Configuration Files](./configuration.md)

---

> Good luck with your extension! If you have any questions, feel free to start a Discussion.