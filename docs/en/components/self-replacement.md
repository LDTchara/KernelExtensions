# Self-Replacement Placeholders (`#…​#`)

Text placeholders in Hacknet content, written as `#name#`, **replaced with real content when the
content is loaded**. This page gathers the **three** sources: custom programs, vanilla mechanics,
and the node placeholders KE adds.

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

## 2. Vanilla placeholders

The vanilla `ComputerLoader.filter()` handles these with a fixed replacement chain (same in 0.6 / 0.7):

**Content / player**

| Placeholder | Replaced with |
|-------------|---------------|
| `#PLAYERNAME#` | The player's account name |
| `#PLAYER_IP#` | The player computer's IP |
| `#PLAYER_ACCOUNT_PASSWORD#` | The player account password |
| `#RANDOM_IP#` | A random IP |
| `#BINARY#` / `#BINARYSMALL#` | Random binary strings (long / short) |

**Program identifiers**: `#SSH_CRACK#`, `#FTP_CRACK#` and the rest of the crackExeData placeholders
(see the vanilla `self-replacement-placeholder` document for the full list).

---

## 3. Added by KE: node placeholders

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

A node matches if **any of `ip` / `idName` / `name`** matches, and matching is
**case-insensitive** (so `#IP_RELAY#` and `#IP_relay#` are equivalent).

!!! note "Why the prefix form (`IP_` first)"
    Vanilla already has `#PLAYER_IP#` / `#RANDOM_IP#` / `#GIBSON_IP#` — three identifiers **ending**
    in `_IP`. Putting the node id first (`#<id>_IP#`) would overlap that shape, leaving safety
    resting on the implicit “vanilla runs first” contract — and without the DLC, `#GIBSON_IP#` is not
    replaced by vanilla at all, so it would fall into our matching range. The prefix form is
    **structurally free of collisions** in the vanilla identifier space (nothing starts with `IP_` / `NAME_`).

---

## 4. Where they take effect

Placeholders go through `ComputerLoader.filter()`, which is used widely:

- **Vanilla calls `filter()` directly**: EOS device content, HackerScript scripts, memory dumps,
  message boards, Start Actions (`SAAddAsset` / `SAAddIRCMessage` / `SAAppendToFile` / `SAStartScreenBleedEffect`, …)
- **Pathfinder's `.Filter()` extension**: a computer's `ip` / `name` / `idName` properties, file
  name/contents, username — so **computer properties themselves support placeholders too**
- **KE side**: actions such as `TerminalWrite` / `TerminalType` / `StartScreenBleedEffectWCC`
- **Custom program file contents** (see section 1)

---

## 5. Caveats

!!! warning "Replacement is one-shot"
    Placeholders are replaced and frozen **when the content is loaded**. If a node's IP later changes
    (e.g. an aircraft gets a `DCLOC:` prefix after crashing), already-replaced text is **not** updated.

- **Unknown node → the original text is kept**; KE neither errors nor logs. If you see `#IP_xxx#`
  still in place, the id is wrong
- **Single pass, non-recursive**: if text produced by `#NAME_A#` itself contains `#IP_B#`, it is not
  expanded again (matching vanilla's `Replace` chain)
- **Computer `ip` / `name` properties also go through `filter()`**: while a computer is loading, other
  nodes may **not be loaded yet** — the original text is kept in that case. That is expected, not a bug
- **`#IP_` / `#NAME_` are KE's placeholder namespace** — don't use these prefixes for anything else

---

## See Also

- [Home](./../index.md) – Return to main index
- [自替换符（中文）](./../../zh/components/self-replacement.md) – Chinese version
- [Executables](./executables.md) – the 4 custom programs and their placeholders
- [Phase Swift](./../systems/phase-swift.md) · [Custom Trial](./../systems/custom-trial.md)
