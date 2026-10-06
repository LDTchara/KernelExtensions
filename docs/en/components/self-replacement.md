# Self-Replacement Placeholders (`#…​#`)

Text placeholders in Hacknet content, written as `#name#`, **replaced with real content when the
content is loaded**. This page covers the **two KE-related** sources: custom programs, and the node
placeholders KE adds.

> Vanilla placeholders (`#PLAYERNAME#` / `#BINARY#` / `#SSH_CRACK#`, …) are base-game mechanics and
> are not expanded here — see the vanilla `self-replacement-placeholder` document for the list.

---

## 1. Custom program placeholders (registered by KE)

KE registers 4 executables with Pathfinder, each bound to one placeholder. **Write them in file
contents** and they are replaced with the actual program data when the save is generated:

| Program | Placeholder | Belongs to |
|---------|-------------|------------|
| `CustomTrial` | `#CUSTOMTRIAL#` | Custom Trial system |
| `PhaseSwift` | `#PHASESWIFT#` | Phase Swift system |
| `EffectsPlayer` | `#EFFECTS#` | Effect playback (the basis for wallpaper / video) |
| `WPTEST` | `#WPTEST#` | Dynamic wallpaper (test program) |

Typical use (put the program into the player node's `bin/`):

```xml
<file path="bin" name="CustomTrial.exe">#CUSTOMTRIAL#</file>
```

!!! warning "The file must actually exist before it can run"
    Registering only makes the placeholder resolvable; it **does not place the file into the node**.
    For the player to run it, the file must exist in their `bin/` (declare it as above).

!!! note "Writing restriction"
    These placeholders must **occupy the whole string** (nothing before or after).
    `prefix#CUSTOMTRIAL#suffix` will not work.

---

## 2. Added by KE: node placeholders

Vanilla only has **player-side** placeholders — nothing to reference another node. KE adds two:

| Placeholder | Replaced with |
|-------------|---------------|
| `#IP_<nodeId>#` | That node's `ip` |
| `#NAME_<nodeId>#` | That node's display name (`name`, not the `idName` you write in XML) |

```xml
<file path="bin" name="relay.cfg">#RELAY_IP#</file>
<!-- becomes -->
<file path="bin" name="relay.cfg">#IP_relayNode#</file>
```

A node matches by **`idName` only** (the id you write in the node XML's `<Computer id="...">`), and matching is
**case-insensitive** (so `#IP_RELAY#` and `#IP_relay#` are equivalent).

!!! note "Why not match `ip` / `name`"
    Nobody would actually write those (`#IP_235.7.94.131#`?), and a node's `name` may itself contain
    placeholders (e.g. `#PLAYERNAME# base`), which would make it an ambiguous key. So only `idName` is accepted.

!!! note "Why the prefix form (`IP_` first)"
    Vanilla already has `#PLAYER_IP#` / `#RANDOM_IP#` / `#GIBSON_IP#` — three identifiers **ending**
    in `_IP`. Putting the node id first (`#<id>_IP#`) would overlap that shape, leaving safety
    resting on the implicit “vanilla runs first” contract — and without the DLC, `#GIBSON_IP#` is not
    replaced by vanilla at all, so it would fall into our matching range. The prefix form is
    **structurally free of collisions** in the vanilla identifier space (nothing starts with `IP_` / `NAME_`).

---

## 3. Where they take effect

Placeholders go through `ComputerLoader.filter()`, which is used widely:

- **Vanilla calls `filter()` directly**: EOS device content, HackerScript scripts, memory dumps,
  message boards, Start Actions (`SAAddAsset` / `SAAddIRCMessage` / `SAAppendToFile` / `SAStartScreenBleedEffect`, …)
- **Pathfinder's `.Filter()` extension**: a computer's `ip` / `name` / `idName` properties, file
  name/contents, username — so **computer properties themselves support placeholders too**
- **KE side**: actions such as `TerminalWrite` / `TerminalType` / `StartScreenBleedEffectWCC`
- **Custom program file contents** (see section 1)

!!! warning "Do not make nodes reference each other"
    Typical mistake: A's `name` is `#NAME_B#` and B's `name` is `#NAME_A#`. Because replacement is
    **single-pass** and order-dependent, you won't get the names you want — the earlier node keeps the
    literal text, and the later one resolves to that literal.

    **Observed case (`AAA` ↔ `BBB`):**

    - `AAA.name = #NAME_BBB#` — BBB is not loaded yet → kept as the literal `#NAME_BBB#`
    - `BBB.name = #NAME_AAA#` — AAA is loaded by then, so it resolves to AAA's name (which *is* `#NAME_BBB#`) → also becomes `#NAME_BBB#`
    - Result: **both nodes end up named `#NAME_BBB#`**

    **Do this instead**: give nodes a literal display name, with no placeholders inside it.
    Use the `RenameNode` action if you need to change a name at runtime.

---

## 4. Caveats

!!! warning "Replacement is one-shot"
    Placeholders are replaced and frozen **when the content is loaded**. If a node's IP later changes
    (e.g. an aircraft gets a `DCLOC:` prefix after crashing), already-replaced text is **not** updated.

- **Unknown node → the original text is kept**; KE neither errors nor logs. If you see `#IP_xxx#`
  still in place, the id is wrong
- **Single pass, non-recursive**: replacement runs once. If a node's **`name` itself contains a placeholder**
  (e.g. A's display name is literally `#NAME_B#`), then `#NAME_A#` resolves to the text `#NAME_B#` and **stops there** —
  it is not expanded into B's name (matching vanilla's `Replace` chain; this is also why mutually referencing
  placeholders cannot loop forever)
- **References to not-yet-loaded nodes are resolved after loading**: `filter()` runs while a node is loading, when
  `netMap` may be incomplete (observed: `playerComp`'s file referenced the later-loaded `testNode6`). KE **sweeps file
  contents again on OSLoaded**, so content can freely reference other nodes without worrying about load order
- **Computer `ip` / `name` properties also go through `filter()`**: those are fixed at load time; if they reference a
  not-yet-loaded node the original text is kept (**no post-load sweep** for properties — add later if needed)
- **`#IP_` / `#NAME_` are KE's placeholder namespace** — don't use these prefixes for anything else

---

## See Also

- [Home](./../index.md) – Return to main index
- [自替换符（中文）](./../../zh/components/self-replacement.md) – Chinese version
- [Executables](./executables.md) – the 4 custom programs and their placeholders
- [Phase Swift](./../systems/phase-swift.md) · [Custom Trial](./../systems/custom-trial.md)
