# Configuration Files

KernelExtensions configuration lives on **two levels**:

- **Extension-level configuration** — `KE-Config.xml`, KE's own global switches. **Detailed on this page.**
- **Per-system configuration** — each system has its own config file or config section, **documented on its
  own system page**; this page only indexes them.

All paths are relative to the extension root.

---

## 1. Extension-Level Configuration (KE-Config.xml)

- Location: `KE-Config.xml` in the extension root (**created automatically** with a commented template if missing)
- Root element: `<KEConfig>`
- These are KE's own global switches, separate from per-system configs; re-read on every OSLoad (restart the
  game after editing)

| Option | Default | Description |
|--------|---------|-------------|
| `<Debug>` | `false` | Emit KE debug-level logs (keep `false` for releases) |
| `<Watermark>` | `true` | Whether the main-menu watermark is drawn (appearance below) |
| `<SkipVanillaIRCLogs>` | `false` | Skip the vanilla `BashLogs.txt` IRC log (load only `CustomIRCLogs.txt`) |
| `<CustomImages>` | — | Custom icon registration (`<Image>` children, paths relative to the extension root, auto-registered as `@filename`) used by `SetNodeIcon` — see [Custom Node Icon System](./../systems/node-icon.md) |
| `<BannedUsernames>` | — | Username blocking when creating new accounts (`<Ban Name="admin" Reason="Reserved" />`; `ReasonBlock` picks a random reason from a `<Reasons>` block) |

### What the main-menu watermark looks like

When enabled (the default), **`+ KernelExtensions <version>`** appears to the right of the Hacknet main-menu
title:

- It sits on the **same line, to the right of** the ZeroDayToolKit watermark, without overlapping other mods' watermarks
- Its colour **flows smoothly over time** (rainbow) with a slight per-character bob
- The `+` prefix follows the community convention for mod watermarks
- The version string follows the mod version automatically — nothing to maintain
- The watermark disappears when the extension unloads

Set `<Watermark>` to `false` to turn it off.

---

## 2. Per-System Configuration Index

Each system's configuration format and **full field list** live on that system's page; the table below only
answers "where does it go".

| System | Location | Root element | Details |
|--------|----------|--------------|---------|
| Custom Trial | `Trial/<name>.xml` | `<TrialConfig>` | [Custom Trial System](./../systems/custom-trial.md) |
| VM Attack | `VMATK/<name>.xml` | `<VMAttackConfig>` | [VM Attack System](./../systems/vm-attack.md) |
| Phase Swift | see the system page | — | [Phase Swift System](./../systems/phase-swift.md) |
| Custom Ending | see the system page | — | [Custom Ending System](./../systems/custom-ending.md) |
| Aircraft Daemon | written directly into the target computer's XML | — | [Aircraft Daemon System](./../systems/aircraft.md) |
| Custom colour presets | `CustomColor/<presetName>.xml` | `<ColorPreset>` | [Custom Color System](./../systems/custom-color.md) |
| Node icons | `<CustomImages>` in `KE-Config.xml` | — | [Custom Node Icon System](./../systems/node-icon.md) |
| Timer / banner / screen alert | given directly as Action parameters — no separate config file | — | [Actions](./actions.md) |

> Example XML files live in the repository's
> [`XMLExamples/`](https://github.com/LDTchara/KernelExtensions/tree/main/XMLExamples) directory.

!!! note "Why this page no longer lists per-system fields"
    Each system's configuration fields (names, defaults, value formats) are maintained **only on its own
    system page**. This page used to duplicate some of them, and the result was that it silently went stale
    whenever the code changed. It is now an index, so the authoritative description lives in exactly one place.

---

## 3. General Rules

- Paths in `file` attributes are **relative to the extension root**.
- Music fields accept plain filenames, relative paths, or DLC paths; resolution is automatic via
  `MusicPathResolver`.
- Colour value formats are documented in the [Custom Color System](./../systems/custom-color.md)
  (which also explains how the parsing chains differ between fields).

---

## See Also

- [Home](./../index.md) – Return to main index
- [配置文件 (中文)](./../../zh/components/configuration.md) – Chinese version
- [Custom Color System](./../systems/custom-color.md) – colour value rules
- [Actions](./actions.md) – action parameters
