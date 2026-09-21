# Configuration Files

KernelExtensions uses XML configuration files to drive its systems. All paths are relative to the extension root.

## Trial Configuration (TrialConfig)

- Location: `Trial/<name>.xml`
- Root element: `<TrialConfig>`
- Contains global settings (effects, timers, colours, etc.) and a `<Phases>` list.
- Details: [Custom Trial System](./../systems/custom-trial.md)
- Example XML: [Trial_Example.xml](https://github.com/LDTchara/KernelExtensions/blob/main/XMLExamples/Trial_Example.xml)

## VM Attack Configuration (VMAttackConfig)

- Location: `VMATK/<name>.xml`
- Root element: `<VMAttackConfig>`
- Contains recovery mode, system logs, guide text, fake files, etc.
- Details: [VM Attack System](./../systems/vm-attack.md)
- Example XML: [MyAttack_Example.xml](https://github.com/LDTchara/KernelExtensions/blob/main/XMLExamples/MyAttack_Example.xml)

## Aircraft Daemon Configuration

- Add it directly to the target computer's XML.
- Configurable attributes: `FallDuration`, `OnFailed`, `OnSaved`.
- Details: [Aircraft Daemon System](./../systems/aircraft.md)

## KE Global Configuration (KE-Config.xml)

- Location: `KE-Config.xml` in the extension root (**created automatically** with a commented template if missing)
- Root element: `<KEConfig>`
- These are KE's own global switches, separate from per-system configs; re-read on every OSLoad (restart the game after editing)

| Option | Default | Description |
|--------|---------|-------------|
| `<Debug>` | `false` | Emit KE debug-level logs (keep `false` for releases) |
| `<Watermark>` | `true` | Whether the main-menu rainbow watermark is drawn |
| `<SkipVanillaIRCLogs>` | `false` | Skip the vanilla `BashLogs.txt` IRC log (load only `CustomIRCLogs.txt`) |
| `<CustomImages>` | — | Custom icon registration (`<Image>` children, paths relative to the extension root, auto-registered as `@filename`) used by `SetNodeIcon` — see [Custom Node Icon System](./../systems/node-icon.md) |
| `<BannedUsernames>` | — | Username blocking when creating new accounts (`<Ban Name="admin" Reason="Reserved" />`; `ReasonBlock` picks a random reason from a `<Reasons>` block) |

## General Rules

- All paths in `file` attributes are **relative to the extension root**.
- Colour fields support names (`Red`), hex (`#FF0000`), or you know who.
- Music fields can use plain filenames, relative paths, or DLC paths; resolution is handled automatically by `MusicPathResolver`.

---

## See Also

- [Home](./../index.md) – Return to main index
- [配置文件 (中文)](./../../zh/components/configuration.md) – Chinese version