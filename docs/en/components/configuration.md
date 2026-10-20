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
| `<SkipVanillaIRCLogs>` | `false` | Skip the vanilla `BashLogs.txt` IRC log (load only your own logs — see below); falls back to vanilla if you have none, to avoid a crash |
| `<CustomIRCLogs>` | — | Extra IRC log files to load (`<File>` children, paths relative to the extension root); with none listed it falls back to `CustomIRCLogs.txt` — see below |

### Custom IRC Logs

IRC logs show up in the **file browser** (the list the vanilla `BashLogs.txt` appears in). KE can add several logs of its own on top of the vanilla ones.

#### Configuration

List the files to load in `KE-Config.xml` at the extension root (any number, any paths):

```xml
<SkipVanillaIRCLogs>false</SkipVanillaIRCLogs>

<CustomIRCLogs>
    <File>News/Archive1.txt</File>
    <File>News/Archive2.txt</File>
</CustomIRCLogs>
```

| Form | Behaviour |
|------|-----------|
| `<File>` entries listed | each of those files is loaded |
| no `<File>` at all | falls back to **`CustomIRCLogs.txt`** at the extension root (the old form keeps working) |
| `<SkipVanillaIRCLogs>false</SkipVanillaIRCLogs>` (default) | vanilla files + vanilla `BashLogs.txt` + your logs **all load** |
| `<SkipVanillaIRCLogs>true</SkipVanillaIRCLogs>` | **only** your logs load; ⚠️ if no custom log is found (including `CustomIRCLogs.txt`), it **falls back to the vanilla logs with a warning** — an empty `FileEntry` list crashes the game when loading random-file nodes |

- Paths are **relative to the extension root** and must not escape it with `..` (an out-of-bounds entry is skipped with a warning; the rest still load)
- A missing file is skipped with a warning and **does not affect the other logs**
- Restart the game after changing the config

#### File Format

Each entry starts with a **`#` at the beginning of a line**:

```
# Topic title
body line 1
body line 2
# Another topic
body...
```

- The topic may be written as **`title|source`** — the right-hand side becomes the entry's trailing `Archived Via : source`; without a `|` the source is the **extension name**
- Any `- [X]` marker and **all spaces** in the title are stripped, then `IRC_Log:` is prepended to form the filename shown in the file browser
- With no `|` and no marker, the entry is simply "`IRC_Log:title` + body + `Archived Via : extension name`"

!!! tip "Also in the template"
    This format description is also written into the auto-generated `KE-Config.xml` template — delete
    `KE-Config.xml` and run the game once to get a fully commented template.

---
xt`) |
| `<CustomImages>` | — | Custom icon registration (`<Image>` children, paths relative to the extension root, auto-registered as `@filename`) used by `SetNodeIcon` — see [Custom Node Icon System](./../systems/node-icon.md) |
| `<BannedUsernames>` | — | Username blocking when creating new accounts (direct reasons and random reason blocks) — see [Banned Usernames](#banned-usernames) |

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

### Banned Usernames

Blocks the listed usernames when a new account is created, showing the player a reason.

```xml
<BannedUsernames>
    <Reasons>
        <Block Name="Test1">
            <Reason>That name is taken - trying again will not help.</Reason>
            <Reason>Pick another one.</Reason>
        </Block>
    </Reasons>
    <Ban Name="admin" Reason="Reserved name" />
    <Ban Name="root" ReasonBlock="Test1" />
</BannedUsernames>
```

| Element / attribute | Description |
|------|------|
| `<Ban Name="…">` | The username to block. **Case-insensitive** |
| `Reason="…"` | A direct reason (one fixed message) |
| `ReasonBlock="…"` | References a `<Block Name>` inside `<Reasons>`; one of its `<Reason>` entries is picked **at random** |
| `<Reasons><Block Name="…">` | Container for reason blocks; the block name is what `ReasonBlock` refers to |
| multiple `<Reason>` in a block | The pool to pick from at random |
| **multiple `<Ban>` with the same name** | Reasons are **appended to the same pool** (not overwritten) — combine with `ReasonBlock` for several sets of random text |

- Write either `Reason` or `ReasonBlock`; if both are present the direct reason wins
- The reason is **player-facing text** and is shown through the game's localisation

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

## 4. Action Point Syntax Cheat Sheet

An **action point** is any config field meaning "run something at this moment" (`OnStart` / `OnComplete` /
`OnFail` / `OnSwitch` …). Its syntax is **not yet consistent across systems**, and writing it the wrong
way **silently does nothing** (the parser ignores unrecognised syntax without an error).
**Check the table before writing one.**

| System | Action points | Syntax | Example |
|--------|---------------|--------|---------|
| PortHackV2 | root `OnStart` / `OnComplete`; per-protocol `OnStart` / `OnComplete` / `OnFail` | **element text content**; **inline actions also supported** | `<OnStart>Actions/x.xml</OnStart>`<br>`<OnStart><Actions><TerminalWrite Text="hi" /></Actions></OnStart>` |
| Custom Trial | `OnStart` / `OnAnimationComplete` / `OnGlobalFail` / `OnComplete` / `OnPhaseStart` / `OnFail` | **`file` attribute** | `<OnStart file="Actions/x.xml" />` |
| Phase Swift | `OnSwitch` | **`file` attribute** | `<OnSwitch file="Actions/x.xml" />` |
| Clock | `OnComplete` | **attribute of `<Clock>`** | `<Clock ID="x" Interval="5" OnComplete="Clocks/done.xml">` |
| Custom Heart Daemon (`CustomHeartDaemon`) | `OnComplete` / `OnHeartbreak` | **attribute of `<CustomHeartDaemon>`** | `<CustomHeartDaemon … OnHeartbreak="Actions/x.xml">` |
| Custom Heart Daemon (Action) | `OnComplete` / `OnHeartbreak` | **attribute of `BreakHeart`** | `<BreakHeart OnComplete="Actions/x.xml" />` |
| Aircraft Daemon | `OnFailed` / `OnSaved` | **attribute of `<FlightDaemon>`** | `<FlightDaemon OnFailed="Actions/Failed" OnSaved="Actions/Saved" />` |
| Fake Recovery | `ActionOnGuideTextStart` | **element text content** | `<ActionOnGuideTextStart>Actions/x.xml</ActionOnGuideTextStart>` |

!!! note "The two families"
    - **Child elements** (PortHackV2 / Custom Trial / Phase Swift / Fake Recovery) — the action point *is* an
      element, so it **can hold inline actions**.
    - **Attributes** (Clock / Custom Heart Daemon / Aircraft Daemon) — the action point is an attribute of its
      parent element, and **an XML attribute cannot carry child elements**, so these can only take a path.
      Inline support for them is planned (it needs new child-element syntax on the parent).

### Inline actions (PortHackV2 only, for now)

PortHackV2's action points can hold Actions directly, so you do not need a file for one or two of them:

```xml
<OnStart>
    <Actions>
        <TerminalWrite Text="bypassing firewall..." />
    </Actions>
</OnStart>
```

- Actions inside `<Actions>` run **immediately**, in order; `<ConditionalActions>` waits for its
  condition (conditions work exactly as in vanilla — see the vanilla Hacknet wiki).
- You can also write **both**: put a file path as the value *and* add child elements → the **file runs
  first, then the inline part**.
- Conditions and available Actions: see [Actions](./actions.md).

!!! warning "A wrong action point will not crash — but it will not run either"
    A missing file, an out-of-bounds path, or an unrecognised element name only **logs a message and
    skips** — it never interrupts the crack / scene / trial. **So "no error" does not mean "it worked" —
    check the log when something seems not to happen.**

!!! info "Unification in progress"
    The differences between systems are **historical**. The plan is to unify them so **every action point
    supports both inline actions and a path**. The unification keeps `file` attribute compatibility, so
    **existing configs will not need changes**.

---

## See Also

- [Home](./../index.md) – Return to main index
- [配置文件 (中文)](./../../zh/components/configuration.md) – Chinese version
- [Custom Color System](./../systems/custom-color.md) – colour value rules
- [Actions](./actions.md) – action parameters
