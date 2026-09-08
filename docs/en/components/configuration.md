# Configuration Files

KernelExtensions uses XML configuration files to drive its systems. All paths are relative to the extension root.

## Trial Configuration (TrialConfig)

- Location: `Trial/<name>.xml`
- Root element: `<TrialConfig>`
- Contains global settings (effects, timers, colours, etc.) and a `<Phases>` list.
- Details: [Custom Trial System](./../systems/custom-trial.md)
- Example XML: [ExampleTrial.xml](https://github.com/LDTchara/KernelExtensions/blob/main/XMLExamples/ExampleTrial.xml)

## VM Attack Configuration (VMAttackConfig)

- Location: `VMATK/<name>.xml`
- Root element: `<VMAttackConfig>`
- Contains recovery mode, system logs, guide text, fake files, etc.
- Details: [VM Attack System](./../systems/vm-attack.md)
- Example XML: [MyAttack.xml](https://github.com/LDTchara/KernelExtensions/blob/main/XMLExamples/MyAttack.xml)

## Aircraft Daemon Configuration

- Add it directly to the target computer's XML.
- Configurable attributes: `FallDuration`, `OnFailed`, `OnSaved`.
- Details: [Aircraft Daemon System](./../systems/aircraft.md)

## General Rules

- All paths in `file` attributes are **relative to the extension root**.
- Colour fields support names (`Red`), hex (`#FF0000`), or you know who.
- Music fields can use plain filenames, relative paths, or DLC paths; resolution is handled automatically by `MusicPathResolver`.

---

## See Also

- [Home](./../index.md) – Return to main index
- [配置文件 (中文)](./../../zh/components/configuration.md) – Chinese version