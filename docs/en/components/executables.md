# Executables

KernelExtensions provides four custom executables. KE registers them against the corresponding `#name#` self-replacement tokens on load:

| Program | Registration | Purpose |
|---------|--------------|---------|
| `CustomTrial` | `#CUSTOMTRIAL#` | Runs multi-phase trials driven by XML configuration files |
| `PhaseSwift` | `#PHASESWIFT#` | Phase Swift: multi-scene topology + multi-track music |
| `EffectsPlayer` | `#EFFECTS#` | Plays vanilla effects standalone |
| `WPTEST` | `#WPTEST#` | Dynamic wallpaper test program |

!!! warning "The file must actually exist before it can be run"
    Registration **only** makes the token resolvable — it does **not** put the file into the node.
    For players to run these programs, the corresponding file must already exist in their own `bin/` folder;
    declare it in the content XML:

    ```xml
    <file path="bin" name="CustomTrial.exe">#CUSTOMTRIAL#</file>
    ```

    The save generator replaces `#CUSTOMTRIAL#` with the real program content, which is what makes it runnable;
    a missing file means it cannot be run at all.

- **CustomTrial**: uses a Flag starting with `CustomTrial_` to specify the configuration (e.g. `CustomTrial_MyTrial`).
  For detailed usage, configuration, and available effects, see the **[Custom Trial System](./../systems/custom-trial.md)** page.

---

## See Also

- [Home](./../index.md) – Return to main index
- [可执行程序 (中文)](./../../zh/components/executables.md) – Chinese version