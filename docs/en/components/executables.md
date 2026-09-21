# Executables

KernelExtensions provides four custom executables (all registered automatically by KE on load; `#name#` is each program's self-replacement token — there is **no** need to place anything in the player's `bin/` folder):

| Program | Registration | Purpose |
|---------|--------------|---------|
| `CustomTrial` | `#CUSTOMTRIAL#` | Runs multi-phase trials driven by XML configuration files |
| `PhaseSwift` | `#PHASESWIFT#` | Phase Swift: multi-scene topology + multi-track music |
| `EffectsPlayer` | `#EFFECTS#` | Plays vanilla effects standalone |
| `WPTEST` | `#WPTEST#` | Dynamic wallpaper test program |

- **CustomTrial**: uses a Flag starting with `CustomTrial_` to specify the configuration (e.g. `CustomTrial_MyTrial`).
  For detailed usage, configuration, and available effects, see the **[Custom Trial System](./../systems/custom-trial.md)** page.

---

## See Also

- [Home](./../index.md) – Return to main index
- [可执行程序 (中文)](./../../zh/components/executables.md) – Chinese version