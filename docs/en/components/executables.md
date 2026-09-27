# Executables

KernelExtensions registers **four** custom executables with Pathfinder, each bound to a `#name#`
self-replacement token. The table below is the **complete** list of what belongs to this category.

| Program | Registration | Belongs to | Status |
|---------|--------------|------------|--------|
| `CustomTrial` | `#CUSTOMTRIAL#` | [Custom Trial System](./../systems/custom-trial.md) | ✅ Available |
| `PhaseSwift` | `#PHASESWIFT#` | [Phase Swift System](./../systems/phase-swift.md) | ✅ Available |
| `EffectsPlayer` | `#EFFECTS#` | Effect playback (the underlying capability for dynamic wallpaper / video playback) | ⚠️ Owning feature unfinished |
| `WPTEST` | `#WPTEST#` | Dynamic wallpaper (test program) | ⚠️ Owning feature unfinished |

!!! info "Why the last two look like “misc”"
    `#EFFECTS#` and `#WPTEST#` belong to features that are **not finished yet** — dynamic wallpaper and
    video playback are both planned for 0.8. They register and resolve today, but have **no stable usage
    documentation**, so treat them as **experimental entry points** and don't ship them in released content.

!!! warning "The file must actually exist before it can be run"
    Registration **only** makes the token resolvable — it does **not** put the file into the node.
    For players to run these programs, the corresponding file must already exist in their own `bin/` folder;
    declare it in the content XML:

    ```xml
    <file path="bin" name="CustomTrial.exe">#CUSTOMTRIAL#</file>
    ```

    The save generator replaces `#CUSTOMTRIAL#` with the real program content, which is what makes it
    runnable; a missing file means it cannot be run at all.

## How CustomTrial is invoked

A Flag starting with `CustomTrial_` selects the configuration to load (e.g. `CustomTrial_MyTrial`).
For detailed usage, configuration options and available effects, see the
**[Custom Trial System](./../systems/custom-trial.md)** page.

---

## See Also

- [Home](./../index.md) – Return to main index
- [可执行程序 (中文)](./../../zh/components/executables.md) – Chinese version
- [Custom Daemons](./daemons.md) – the other kind of mountable object
- [Actions](./actions.md) – the action list
