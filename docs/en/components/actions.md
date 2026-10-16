# Custom Actions

KernelExtensions registers **46** custom Actions that can be invoked in any action file. This page has
three parts:

- **1. System-related Actions** — a quick reference with pointers; full parameters live on each system page
- **2. General Actions (not covered by a system)** — full parameters and examples
- **3. Target-computer atomic Actions** — ports / firewall / proxy primitives, full parameters and examples

!!! note "It is 44 when SROS is installed"
    When the **SROS** plugin is detected, `ShowTitle` and `StartEnding` are **not registered**
    (their features overlap, so KE steps aside to avoid a conflict) — such setups have 44 Actions.

All paths are relative to the extension root.

---

## 1. System-Related Actions

| System | Actions | Purpose | Details |
|--------|---------|---------|---------|
| Custom Trial | `FailTrial` · `RestoreCustomTrialNodes` | Force a trial to fail · restore destroyed nodes | [Custom Trial System](./../systems/custom-trial.md) |
| Phase Swift | `PhaseSwiftInit` · `PhaseSwiftScene` · `PhaseSwiftMusic` · `PhaseSwiftStop` · `PhaseSwiftFadeOut` · `BlockNode` · `UnblockNode` | Start/stop, scene switching, music-phase switching, runtime blocklist nodes | [Phase Swift System](./../systems/phase-swift.md) |
| VM Attack | `LaunchVMAttack` | Trigger a VM attack | [VM Attack System](./../systems/vm-attack.md) |
| Aircraft Daemon | `AttackAircraft` · `UploadAircraftSysFile` · `ShowAircraftOverlay` · `HideAircraftOverlay` | Attack · repair · altimeter overlay | [Aircraft Daemon System](./../systems/aircraft.md) |
| Node Link Control | `LinkControlReset` · `LinkControlAdd` · `LinkControlRemove` | Runtime link add/remove, restore org baseline | see the section below |
| Timer | `ClockStart` · `ClockStop` | Start/stop a custom timer | [Custom Timer System (Clock)](./../systems/clock.md) |
| Node Icons | `SetNodeIcon` | Set a node's icon | [Custom Node Icon System](./../systems/node-icon.md) |
| Custom Ending | `StartEnding` · `BreakHeart` | Trigger an ending / the heartbreak sequence | [Custom Ending System](./../systems/custom-ending.md) |
| Target-computer ops | `PortOpen` · `PortClose` · `PortControl` · `PortFirewallAdd` · `PortFirewallSolve` · `PortFirewallReroll` · `PortFirewallConfigure` · `PortProxyAdd` · `PortProxySolve` · `PortProxyReset` · `ListPorts` | Target-side port / firewall / proxy primitives · protocol-name lookup | [PortHackV2 Cracker](./../systems/port-hack-v2.md) · see part 3 |

### Node Link Control (attribute quick reference)

All three share the **org baseline**: a snapshot of the computer's links taken **at game start**
(content XML `<dlink>` enters the baseline this way), persisted to the save as `<OrgLinks>`.
`Add` / `Remove` only change runtime links and can be undone with `Reset`.

| Action | Example |
|--------|---------|
| `LinkControlReset` | `<LinkControlReset SourceComp="playerComp" />` |
| `LinkControlAdd` | `<LinkControlAdd SourceComp="playerComp" TargetComp="jmail" />` |
| `LinkControlRemove` | `<LinkControlRemove SourceComp="playerComp" TargetComp="jmail" />` |

- Attribute names are **case-insensitive** (handled by the `KEAction` base class; PascalCase is still recommended)
- Missing nodes or a missing `TargetComp` are logged as errors and skipped — never a crash
- `<OrgLinks>` only ever appears in saves; to declare initial links in content XML, use vanilla `<dlink>`

---

## 2. General Actions (Not Covered by a System)

### Terminal and Nodes

| Action | Description | Example |
|--------|-------------|---------|
| `TerminalWrite` | Outputs a line of text to the terminal. | `<TerminalWrite Text="Hello, World!" />` |
| `TerminalType` | Types text character-by-character — **no automatic newline**; appends at the current cursor (like HackerScript's `write`, so several calls can share one line). | `<TerminalType Text="A message typed out" CharDelay="0.04" />` |
| `TerminalFocus` | Plays a terminal focus effect (full-screen darken + expanding border). | `<TerminalFocus Duration="5.0" BorderDuration="2.0" FadeInDuration="0.5" />` |
| `RenameNode` | Renames a node by its ID; takes effect immediately and persists in saves. | `<RenameNode NodeID="dhs" NewName="Secret Base" />` |

- `TerminalFocus` parameters: `Duration` (how long the full-screen shade stays, seconds, default `2.0`),
  `BorderDuration` (border-expansion animation length, default = `Duration`), `FadeInDuration` (how long the
  shade takes to fade from transparent to fully black, default = `Duration`), `DarkenAlpha` (maximum shade
  opacity `0~1`, default `0.8`) and `ExpandAmount` (maximum outward expansion in pixels, default `200`)
- `BorderDuration` / `FadeInDuration` **negative / `NaN` / `Infinity` / omitted = use `Duration`**; when either
  is shorter than `Duration` the border disappears after its animation while the shade stays until `Duration` ends

### Visuals and Sound

| Action | Description | Example |
|--------|-------------|---------|
| `PlaySound` | Plays a WAV sound effect from the extension directory. | `<PlaySound Path="Sounds/beep.wav" Volume="1" Pitch="0" Pan="0" Delay="1.5" DelayHost="cheat"/>` |
| `FlashScreen` | UI flash: pulses the interface with a colour and fades back to the current theme default. | `<FlashScreen Color="Red" Duration="2.0" />` |
| `SwitchToThemeKeepLayout` | Switches theme while **keeping the panel layout** (colour only). | `<SwitchToThemeKeepLayout ThemePathOrName="HacknetMint" FlickerInDuration="1.5" />` |

- `PlaySound` paths are **relative to the extension root** and **must end in `.wav`**; `Volume` and `Pitch`
  both default to `0.5`, and `Pan` is the **stereo position** (`-1` hard left ~ `1` hard right, default `0`
  centre). A missing file or a path that escapes the extension (`..`, absolute path) logs a `Warn` and skips
- `FlashScreen` accepts dynamic colour keywords (e.g. `LDTchara`) in `Color`; `Duration` defaults to `2.0`
  (non-positive = restore defaults immediately); `PlaySound="true"` plays the warning beep alongside the
  flash. Re-triggering **refreshes** rather than stacking.
- `SwitchToThemeKeepLayout` changes colours only; use vanilla `SASwitchToTheme` if you need the layout changed too.

### Title Banner `ShowTitle`

Remade from vanilla `IncomingConnectionOverlay` (the “machine is being connected to” overlay): a warning
banner appears in the centre of the screen with a title and multi-line body, themed diagonal stripes above
and below, and an optional icon on the left. Good for story beats, system alerts and chapter transitions.

```xml
<ShowTitle Title="WARNING" Preset="warning" Duration="6" Icon="default">
Left the LDTchara VPN
Tracking in about 60 seconds
Get back to the VPN now
</ShowTitle>
```

The body goes between the tags and **each newline is a line break**. Leading/trailing blank lines and the
common indentation of all lines are stripped, so you can lay the XML out freely (indentation never reaches
the display).

| Attribute | Required | Default | Description |
|-----------|:--------:|---------|-------------|
| `Title` | ❌ | empty | Banner title (⚠️ **ASCII only**, see below) |
| `Preset` | ❌ | `info` | Accent preset: `info` = theme highlight base (`defaultHighlightColor`); `warning` = theme warning colour (`warningColor`) |
| `Duration` | ❌ | `5` | Seconds the banner stays on screen |
| `AccentColor` | ❌ | empty | CustomColor override for the accent; `NONE` / empty = use the `Preset` theme colour |
| `Icon` | ❌ | empty | empty / `NONE` = **no icon**; `default` = built-in icon; anything else = path relative to the extension root |
| `IconTint` | ❌ | empty | empty = **auto** (built-in tinted / custom keeps its colours); `true` / `false` = force; other values = auto + warning |

When `AccentColor` is omitted the accent comes from the **current game theme**, so banner colours follow
the player's theme. Full colour-parsing rules are in the
[Custom Color System](./../systems/custom-color.md).

**Icon rules**:

| `Icon` | `IconTint` | Result |
|---|---|---|
| omitted / empty / `NONE` | — | **no icon** |
| `default` | omitted | built-in icon, **tinted** (follows the accent) |
| `default` | `false` | built-in icon, original colours |
| valid path | omitted | that icon, **original colours** |
| valid path | `true` | that icon, **tinted** |
| invalid path / load failure | any | **falls back to the built-in icon** (tinted) + `KELog.Warn` |

- The built-in icon comes from the mod's embedded resources and is **never written into your extension folder**
- Any other `IconTint` value (e.g. `yes`) is treated as **auto** and logs a `KELog.Warn`

!!! warning "Titles are ASCII-only"
    The title uses the game's **title font** (`Kremlin`), for which the developers shipped **no localised
    version in any language** — so non-ASCII characters render as `?`. **The body is not affected**: it uses
    the localised font and displays Chinese, Japanese, etc. normally. When a title contains non-ASCII
    characters KE logs a `KELog.Warn`. If you need non-ASCII text, put it in the **body**.

### Full-Screen Alert `StartScreenBleedEffectWCC`

**Written exactly like vanilla `StartScreenBleedEffect`**, with two extra colour parameters.
Suited to system alarms, emergencies and story turns.

| Attribute | Required | Default | Description |
|-----------|:--------:|---------|-------------|
| `AlertTitle` | ❌ | `EMERGENCY` | Top alert title text |
| `TotalDurationSeconds` | ❌ | `200` | Total duration of the effect in seconds |
| **`BackgroundColor`** | ❌ | dark red (`120,0,0`) | Full-screen background colour |
| **`TextBackgroundColor`** | ❌ | translucent dark red (`105,0,0,200`) | Colour behind the text |
| `CompleteAction` | ❌ | empty (not run) | ConditionalActions file loaded and run when the effect ends; `NONE` / empty = do not run |
| `Delay` / `DelayHost` | ❌ | — | Pathfinder delayed execution |

The element body is parsed line by line and **at most 3 lines are used** (short input is padded, extra
lines ignored).

```xml
<StartScreenBleedEffectWCC
    AlertTitle="WARNING"
    TotalDurationSeconds="5.0"
    BackgroundColor="#FF2020"
    TextBackgroundColor="#880000"
    CompleteAction="Actions/end.xml">
    Line 1 text
    Line 2 text
    Line 3 text
</StartScreenBleedEffectWCC>
```

- **Colour values**: go through the CustomColor parsing chain (dynamic colour / preset / numeric RGB / Hex /
  XNA named colour) — full rules in the [Custom Color System](./../systems/custom-color.md)
- **Cancelling**: use the **vanilla** action `<CancelScreenBleedEffect />`. A patch makes KE stop its own
  effect in step, so the vanilla and custom effects never stack
- WCC = **W**ith **C**ustom **C**olor

### RAM Display `RamDisplay`

Adjusts the **RAM module** text on the operation interface: `USED RAM: x / y mb`.
`Multiplier` scales both `x` and `y`; `Unit` replaces the unit string — **text only**.
The RAM bar still follows the real usage ratio.

Useful for faking a machine's specs in the story (e.g. making a small box “look” like it has lots of memory).

!!! note "Relationship with ZeroDayToolKit"
    Stacks with **ZeroDayToolKit**'s `SASetRAM`: the latter sets the RAM values, and this action
    scales them (multiplier + unit) for display. The two do not conflict — values are set first, then displayed.

| Attribute | Required | Default | Description |
|-----------|:--------:|---------|-------------|
| `Multiplier` | ❌ | `1` | Unified scale for `x` and `y`; **decimals preserved** (up to two, trailing zeros trimmed) |
| `Unit` | ❌ | `mb` | Unit string replacing `mb`; `NONE` / empty = fall back to default |
| `Delay` / `DelayHost` | ❌ | — | Pathfinder delayed-action attributes |

```xml
<RamDisplay Multiplier="0.5" Unit="GB" />
<!-- real 1024mb → shown as 512GB -->
```

- **Only explicitly given attributes are applied**; omitted ones **keep their current value** (so you can change just the multiplier, or just the unit)
- **Reset**: write `Multiplier="1" Unit="mb"` explicitly
- **Persistence**: changes live in **memory** and are written to the save on the next save; a save without this node returns to defaults

!!! note "Changes are runtime‑temporary"
    This action only changes the in‑memory display settings and **does not write to disk immediately**;
    they are persisted with the next save. So **quitting without saving loses the change**;
    once saved, it is retained.
- **No KE-Config section**: defaults live in code and are overridden by this action (extensions can set them from their own startup action)
- With neither attribute set, the action does nothing and does not trigger a redundant save

---

### Temporary Theme Colours `ChangeThemeColor`

Changes the current OS's **theme colour fields at runtime** without switching themes — panel layout
and background stay as they are. Useful for story beats that recolour the UI (e.g. everything turns
red during an intrusion); the change lapses as soon as the player switches theme.

| Attribute | Required | Default | Description |
|-----------|:--------:|---------|-------------|
| `Fields` | ✅ | `""` | Comma-separated `field:colour` list |
| `Duration` | ❌ | `-1` | Lifetime in seconds for **dynamic** colours; omitted / ≤ 0 / NaN / Infinity = indefinite (until the player switches theme). On expiry the pre-trigger colours are written back (dynamic fields only, plus `topBarColor` / `highlightColor` / `moduleColorSolid`) |
| `Delay` / `DelayHost` | ❌ | — | Pathfinder delayed-action attributes |

Colour values come in two kinds and **can be mixed inside one `Fields` list**:

| Kind | Syntax | Behaviour |
|------|--------|-----------|
| **Static** | XNA named colours (e.g. `Red`, case-insensitive), `#RGB`, `#RRGGBB`, `#AARRGGBB`, `R,G,B[,A]` | Written **once** at trigger time; then kept until the player switches theme |
| **Dynamic** | `LDTchara[:speed[:opacity[:saturation[:brightness]]]]`, `RGB` (alias of `LDTchara`), `Gradient:#from:#to:seconds`, CC preset name`[:speedMultiplier[:opacity]]` | Recomputed **every frame**; needs `Duration` to wrap up |

`NONE` / empty = leave that field unchanged. Field names must be theme colour fields such as
`defaultHighlightColor`, `warningColor`, `moduleColorSolidDefault`, `terminalTextColor`.

```xml
<ChangeThemeColor Fields="defaultHighlightColor:Red,warningColor:#FF0000" />
<!-- only the highlight and warning colours change; the rest keep the current theme -->

<ChangeThemeColor Fields="defaultHighlightColor:RGB,warningColor:#FF0000" Duration="10" />
<!-- the highlight goes rainbow and is recomputed every frame, reverting after 10s;
     the warning colour stays a static red -->
```

- **Unknown field or unparsable colour** → that pair is skipped with a `KELog.Warn`; other fields still apply
- **Revert**: only the in-memory OS values change — nothing is written to the save and the loaded theme
  object is untouched; after the player **switches theme** the OS fields are rewritten, so the change lapses
- **Dynamic fields must be wrapped up explicitly** — they are rewritten every frame and only revert to
  their pre-trigger values when `Duration` expires
- **Use `SwitchToThemeKeepLayout`** if you want to change the layout along with the colours

---

## 3. Target-Computer Atomic Actions (Ports / Firewall / Proxy)

Every action in this group works on a **target computer** and **does exactly one thing** (open a port,
close a port, add a firewall, reroll a solution…). Combine them freely into sequences — they work from
a PortHackV2 action hook as well as from any other system's action file.

- **Ports**: `PortOpen` · `PortClose` (open/close) · `PortControl` (add/remove)
- **Firewall**: `PortFirewallAdd` · `PortFirewallSolve` · `PortFirewallReroll` · `PortFirewallConfigure`
- **Proxy**: `PortProxyAdd` · `PortProxySolve` · `PortProxyReset`
- **Diagnostics**: `ListPorts` (lists every registered port protocol — use it to look up protocol names)

**Common conventions**

| Item | Description |
|------|-------------|
| `TargetComp` | Target computer `idName`; **empty / `NONE` = the currently connected node** (the player's own machine when nothing is connected) |
| `Delay` / `DelayHost` | Provided by the `KEAction` base class and available on every action in this group (see “Delayed Execution” at the end of this page) |
| Target not found | Logs a `KELog.Warn` and **skips this action** — no crash, and never a silent “it looked like it ran” |
| Port lists | Commas or semicolons both work (`ssh,ftp` / `ssh;ftp`); blanks are dropped and duplicates removed |
| Permissions | **No permission checks**: they change the target directly, like a story script. To make something happen only after the player succeeds, hang it on an action hook |
| Action file paths | Paths referenced from action hooks are **relative to the extension root** and **must match the on-disk file name case exactly** (Linux is case-sensitive) — see the [Extension Author Guide](./../guides/author-guide.md) |

### Ports

Under Pathfinder a port has two separate facts: **whether it exists on the machine** and **whether it is
open**. Adding and removing belongs to `PortControl`; opening and closing belongs to
`PortOpen` / `PortClose` — do not use one for the other.

#### `PortOpen` — open ports

Opens one or more ports that **already exist on the target**. Look the protocol names up with `ListPorts` first.

| Attribute | Required | Default | Description |
|-----------|:--------:|---------|-------------|
| `Ports` | ✅ | empty | Port protocol names, comma- or semicolon-separated; `NONE` / empty = logs a warning and skips |
| `TargetComp` | ❌ | empty | Target computer `idName`; empty / `NONE` = the currently connected node |
| `Delay` / `DelayHost` | ❌ | — | Pathfinder delayed execution |

```xml
<PortOpen Ports="ssh,ftp" />
<PortOpen Ports="ssh" TargetComp="targetNode" />
```

- Under Pathfinder, “opening” a port means **marking it as cracked** (the vanilla `openPort` is rewritten
  to set that flag, and vanilla `isPortOpen` reads it too) — the effect equals the player having cracked it
- **A port that is not on the target** → logged and skipped **one by one**; the other ports in the same call still run
- If the port does not exist yet, install it with `PortControl` first

#### `PortClose` — close ports

Closes one or more ports that **already exist on the target**.

| Attribute | Required | Default | Description |
|-----------|:--------:|---------|-------------|
| `Ports` | ✅ | empty | Port protocol names, comma- or semicolon-separated; `NONE` / empty = logs a warning and skips |
| `TargetComp` | ❌ | empty | Target computer `idName`; empty / `NONE` = the currently connected node |
| `Delay` / `DelayHost` | ❌ | — | Pathfinder delayed execution |

```xml
<PortClose Ports="ftp" TargetComp="targetNode" />
```

- It only **closes** the port, it **does not remove it** — for “this machine no longer has that port” use
  `PortControl Switch="remove"`
- A port that is not on the target is logged and skipped

#### `PortControl` — add / remove ports

**Registers and installs** a port on the target, or **removes** a port from it.

| Attribute | Required | Default | Description |
|-----------|:--------:|---------|-------------|
| `TargetID` | ✅ | empty | Target computer `idName`. ⚠️ **This action uses `TargetID`**, unlike the `TargetComp` of the other target actions; an unknown machine prints to the terminal and skips |
| `Protocol` | ✅ / ❌ | empty | Port protocol name. May be omitted for `Switch="remove"` — the protocol is then looked up from `PortNum` in the port registry |
| `DisplayName` | ✅ when adding | empty | Port display name (what the terminal and UI show) |
| `PortNum` | ❌ | `0` | Port number; `0` = no default port number. Used to look the protocol up when removing |
| `Switch` | ❌ | `add` | `add` = add (default) / `remove` = remove; any other value prints to the terminal and skips |
| `Delay` / `DelayHost` | ❌ | — | Pathfinder delayed execution |

```xml
<!-- Install an ssh port (default port number 22) on the target -->
<PortControl TargetID="targetNode" Protocol="ssh" DisplayName="SSH" PortNum="22" />

<!-- Remove it again; the following two are equivalent -->
<PortControl TargetID="targetNode" Protocol="ssh" Switch="remove" />
<PortControl TargetID="targetNode" PortNum="22" Switch="remove" />
```

- Adding **registers the protocol first** (protocol name + display name + default port number go into the
  global port registry) and then installs the port on the target; once registered, **every machine** can use
  that name
- The installed port is **closed**: the player still has to crack / open it — nothing is handed over
- **Adding twice is safe** (Pathfinder deduplicates by protocol and returns early if it is already there)
- Removal works by **protocol name** and does not require the protocol to be in the registry; when `Protocol`
  is omitted the port number is used instead (covering both vanilla and mod-registered ports)
- Removing a port the machine does not have prints `not found` on the terminal — that is not an error

### Firewall

The solution lives on the **target computer**: `PortFirewallAdd` places one, `PortFirewallSolve` solves it,
`PortFirewallReroll` replaces the answer and `PortFirewallConfigure` tunes its parameters.

#### `PortFirewallAdd` — place a new firewall

Places a **new firewall** on the target (replacing any existing one) and leaves it **unsolved**.

| Attribute | Required | Default | Description |
|-----------|:--------:|---------|-------------|
| `Solution` | ✅ | empty | The firewall **solution**: `@random` = **roll one**, any other string = use that literal solution; `NONE` / empty = logs a warning and skips |
| `Length` | ❌ | length of `Solution` | Length of the **random** solution; only meaningful with `Solution="@random"`: omitted = use `Solution`'s own length (7 for `@random`), `0` = vanilla's default 6; with a literal `Solution` this attribute does nothing |
| `AdditionalDelay` | ❌ | `0` | Extra pause **per line** while the player analyses the firewall (seconds) — higher is slower |
| `TargetComp` | ❌ | empty | Target computer `idName`; empty / `NONE` = the currently connected node |
| `Delay` / `DelayHost` | ❌ | — | Pathfinder delayed execution |

```xml
<!-- A 12-character random solution the player has to work out -->
<PortFirewallAdd Solution="@random" Length="12" AdditionalDelay="0.3" />

<!-- Solution fixed to ABCD: the player types ABCD to get through -->
<PortFirewallAdd Solution="ABCD" TargetComp="targetNode" />
```

- It **replaces** the target's existing firewall rather than stacking, and always leaves it **unsolved**
- The displayed length matches the solution: the write syncs `solutionLength` to the **final** solution, so
  you never get “type N characters” with an M-character answer (an unsolvable firewall)
- For “keep the firewall, only change the answer” use `PortFirewallReroll`; for “tune the existing firewall”
  use `PortFirewallConfigure`; to solve it outright use `PortFirewallSolve`

!!! tip "`Length` only affects random solutions"
    With `Solution="@random"` the random solution is generated at `Length` characters; with a literal
    `Solution` the answer is exactly the string you wrote and `Length` plays no part. Either way the displayed
    length is synced to the real solution, so the firewall is always solvable.

#### `PortFirewallSolve` — solve the firewall

Marks the target's firewall as **solved**, optionally after a delay.

| Attribute | Required | Default | Description |
|-----------|:--------:|---------|-------------|
| `TargetComp` | ❌ | empty | Target computer `idName`; empty / `NONE` = the currently connected node |
| `Delay` / `DelayHost` | ❌ | — | Seconds to wait **before** solving; empty = immediately |

```xml
<PortFirewallSolve />

<!-- 12 seconds after the crack the opponent "notices" the firewall is broken -->
<PortFirewallSolve Delay="12" />
```

- A target **without a firewall** → logs a warning and skips; it never invents one
- It only marks the firewall **solved** and **does not clear analysis progress**; to reset that too use
  `PortFirewallReroll` / `PortFirewallConfigure Reset="true"`
- The delay comes from the base `Delay`; this action has **no** `AdditionalDelay` (that is “how much longer
  each analysis line takes for the player”)

#### `PortFirewallReroll` — replace the solution

**The firewall stays, its answer changes** (typically the opponent countering).

| Attribute | Required | Default | Description |
|-----------|:--------:|---------|-------------|
| `Length` | ❌ | `12` | Length of the new solution; `0` = vanilla's default 6; negative / non-numeric = logs a `Warn` and uses `12` |
| `AdditionalDelay` | ❌ | `0` | Extra pause per analysis line (seconds) |
| `Reset` | ❌ | `true` | Whether to clear the solved state and analysis progress |
| `TargetComp` | ❌ | empty | Target computer `idName`; empty / `NONE` = the currently connected node |
| `Delay` / `DelayHost` | ❌ | — | Pathfinder delayed execution |

```xml
<!-- Reroll to a new 12-character random solution and clear the "solved" state -->
<PortFirewallReroll Length="12" AdditionalDelay="0.3" />
```

- Difference from `PortFirewallAdd`: that one **places a new firewall**; this one **replaces the answer of the
  existing firewall**
- A target **without a firewall** → logs a warning and skips
- Rerolling **creates a fresh firewall**, so the result is always “unsolved + analysis progress back to zero”;
  `Reset="false"` cannot bring the solved state back

#### `PortFirewallConfigure` — tune the existing firewall

Adjusts the **existing** firewall's solution length, analysis delay and solution content, optionally resetting
the solved state and analysis progress.

| Attribute | Required | Default | Description |
|-----------|:--------:|---------|-------------|
| `Length` | ❌ | current solution length | Solution length; empty = keep the current value. ⚠️ With a **literal solution** it must match that solution, see below |
| `AdditionalDelay` | ❌ | current value | Extra pause per analysis line (seconds); empty = keep the current value |
| `Solution` | ❌ | current solution | Solution content: empty = **keep the current solution** (on a target without a firewall = roll a fresh random one, which is where empty and an explicit `@random` are equivalent); `@random` = **roll a new one of `Length` characters**; any other string = use that literal solution |
| `Reset` | ❌ | `false` | `true` = clear the solved state and analysis progress |
| `TargetComp` | ❌ | empty | Target computer `idName`; empty / `NONE` = the currently connected node |
| `Delay` / `DelayHost` | ❌ | — | Pathfinder delayed execution |

```xml
<!-- Only make each analysis line slower, keep everything else as it is -->
<PortFirewallConfigure AdditionalDelay="0.5" />

<!-- Swap in a 12-character random solution and reset the progress -->
<PortFirewallConfigure Length="12" Solution="@random" Reset="true" />
```

- Omitted attributes **keep their current value** and are never pushed back to a default — leave one out to
  leave it alone
- A target **without** a firewall is handled as “add one” (same rules as `PortFirewallAdd`)
- ⚠️ `Length` versus a **literal solution**: only random solutions are generated at `Length` characters; when
  `Solution` is a specific string, give a `Length` of the **same size** (leaving it out keeps the current
  length, and a mismatch produces an **unsolvable** firewall: “type N characters” with an M-character answer).
  If you only want to change the difficulty (analysis delay), do not write `Length`

### Proxy

While a proxy is active the player must **overload** it before continuing; `PortProxyAdd` adds one,
`PortProxySolve` removes it, and `PortProxyReset` only pushes the overload progress back.

#### `PortProxyAdd` — add a proxy

| Attribute | Required | Default | Description |
|-----------|:--------:|---------|-------------|
| `Time` | ❌ | `10` | Overload / crack time for the proxy (vanilla `addProxy` ticks). ⚠️ **Must be > 0**: with `0` or a negative value vanilla does nothing (the machine is not flagged as having a proxy) and only the active flag is set |
| `TargetComp` | ❌ | empty | Target computer `idName`; empty / `NONE` = the currently connected node |
| `Delay` / `DelayHost` | ❌ | — | Pathfinder delayed execution |

```xml
<PortProxyAdd Time="10" />
<PortProxyAdd Time="30" TargetComp="targetNode" />
```

- The proxy ends up **active** (`proxyActive = true`): the player has to overload it first
- If the target already has a proxy, the time and state are overwritten — never stacked into two

#### `PortProxySolve` — remove the proxy

| Attribute | Required | Default | Description |
|-----------|:--------:|---------|-------------|
| `TargetComp` | ❌ | empty | Target computer `idName`; empty / `NONE` = the currently connected node |
| `Delay` / `DelayHost` | ❌ | — | Seconds to wait **before** removing it; empty = immediately |

```xml
<PortProxySolve />
<PortProxySolve Delay="11" />
```

- Removal also marks a running `PortHackExe` for termination (matching vanilla: the proxy is gone, so the
  crack process ends with it)
- A target **without a proxy** → logs a warning and skips
- To only zero the overload progress while the proxy **stays active**, use `PortProxyReset`

#### `PortProxyReset` — zero the overload progress

Pushes the player's **overload progress** back to the start while the **proxy stays active** (in the story:
“the opponent shoved your progress back”).

| Attribute | Required | Default | Description |
|-----------|:--------:|---------|-------------|
| `TargetComp` | ❌ | empty | Target computer `idName`; empty / `NONE` = the currently connected node |
| `Delay` / `DelayHost` | ❌ | — | Pathfinder delayed execution |

```xml
<PortProxyReset />
```

- Difference from `PortProxySolve`: that one is “the proxy is removed” (`proxyActive = false`), while this
  action **keeps the proxy**
- A target **without a proxy** → logs a warning and skips

### Diagnostics `ListPorts`

An **author tool**: it prints every **registered port protocol** (mod-registered and vanilla) to the player's
terminal, so you can look up which protocol name to write and what its default port number is. Pathfinder
exposes no public way to enumerate ports; this action fills that gap.

| Attribute | Required | Default | Description |
|-----------|:--------:|---------|-------------|
| `Filter` | ❌ | empty | Only list entries whose **protocol or display name contains** this substring (case-insensitive) |
| `Max` | ❌ | `40` | Maximum rows per call; `0` / negative = unlimited |
| `ToLog` | ❌ | `false` | `true` = also write one `KELog.Info` line (handy as a record in the log) |
| `Delay` / `DelayHost` | ❌ | — | Pathfinder delayed execution |

```xml
<ListPorts />
<ListPorts Filter="ssh" />
```

Output looks like this:

```
Registered ports: 12 (3 from mods, 9 built-in)
[22] ssh - SSH (vanilla)
[no default port] myport - My Protocol (mod)
```

- Each row is `[default port] protocol - display name (vanilla|mod)`; protocols without a default port are
  marked `no default port` explicitly
- Ordering: by display name, then by protocol — stable output that is easy to compare against
- `Filter` matching nothing → the terminal prints `No port matches filter '...'`
- Nothing listed at all (usually the Pathfinder port registry cannot be read) → logs a `KELog.Error` and asks
  the user to report it
- It is **read-only** and changes no state; running it once from a startup action is enough

### Combining them

“After the player cracks in, the target hits back”:

```xml
<PortClose Ports="ftp" />
<PortFirewallReroll Length="12" AdditionalDelay="0.3" />
<PortProxyReset />
```

These actions **perform no permission checks**, so *when* they take effect is entirely up to the hook you hang
them on — on the crack-success hook it is “counter-attack after the crack”; inside `<OnStart>` the board is set
up from the beginning.

---

## Action Name Conflicts and Fallback

Third-party mods may occupy Action names (real example: `Stuxnet.Audio` occupies `PlaySound`).
Pathfinder's `RegisterAction` throws on a duplicate name and **aborts the whole plugin load**, so
**all 46 KE Actions** are registered under **two names**: the original, and `KE` + the original.

| Original name | Alias registered alongside |
|---------------|---------------------------|
| `FailTrial` | `KEFailTrial` |
| `RestoreCustomTrialNodes` | `KERestoreCustomTrialNodes` |
| `LaunchVMAttack` | `KELaunchVMAttack` |
| `PhaseSwiftInit` | `KEPhaseSwiftInit` |
| `PhaseSwiftScene` | `KEPhaseSwiftScene` |
| `PhaseSwiftMusic` | `KEPhaseSwiftMusic` |
| `PhaseSwiftStop` | `KEPhaseSwiftStop` |
| `PhaseSwiftFadeOut` | `KEPhaseSwiftFadeOut` |
| `BlockNode` | `KEBlockNode` |
| `UnblockNode` | `KEUnblockNode` |
| `TerminalFocus` | `KETerminalFocus` |
| `TerminalWrite` | `KETerminalWrite` |
| `TerminalType` | `KETerminalType` |
| `RenameNode` | `KERenameNode` |
| `SetNodeIcon` | `KESetNodeIcon` |
| `ListPorts` | `KEListPorts` |
| `SwitchToThemeKeepLayout` | `KESwitchToThemeKeepLayout` |
| `PlaySound` | `KEPlaySound` |
| `FlashScreen` | `KEFlashScreen` |
| `StartScreenBleedEffectWCC` | `KEStartScreenBleedEffectWCC` |
| `ClockStart` | `KEClockStart` |
| `ClockStop` | `KEClockStop` |
| `AttackAircraft` | `KEAttackAircraft` |
| `UploadAircraftSysFile` | `KEUploadAircraftSysFile` |
| `ShowAircraftOverlay` | `KEShowAircraftOverlay` |
| `HideAircraftOverlay` | `KEHideAircraftOverlay` |
| `FlightDaemonStatus` | `KEFlightDaemonStatus` |
| `FlightDaemonMap` | `KEFlightDaemonMap` |
| `BreakHeart` | `KEBreakHeart` |
| `LinkControlReset` | `KELinkControlReset` |
| `LinkControlAdd` | `KELinkControlAdd` |
| `LinkControlRemove` | `KELinkControlRemove` |
| `ShowTitle` | `KEShowTitle` |
| `StartEnding` | `KEStartEnding` |
| `RamDisplay` | `KERamDisplay` |
| `ChangeThemeColor` | `KEChangeThemeColor` |
| `PortControl` | `KEPortControl` |
| `PortOpen` | `KEPortOpen` |
| `PortClose` | `KEPortClose` |
| `PortFirewallAdd` | `KEPortFirewallAdd` |
| `PortFirewallSolve` | `KEPortFirewallSolve` |
| `PortFirewallReroll` | `KEPortFirewallReroll` |
| `PortFirewallConfigure` | `KEPortFirewallConfigure` |
| `PortProxyAdd` | `KEPortProxyAdd` |
| `PortProxySolve` | `KEPortProxySolve` |
| `PortProxyReset` | `KEPortProxyReset` |

**Rules**:

- Both names point at the **same Action** — either one runs the same implementation
- **No conflict**: both names work
- **One name taken by a third party**: the other still works, and KE logs a `Warn`
- **Both taken** (very rare): the Action is not registered and KE logs an `Error`

!!! tip "Why register two names for everything"
    - **One rule to remember**: no need to learn which Actions have a `KE`-prefixed variant — **all do**
    - **The `KE`-prefixed name works in every environment**, conflicts or not
    - **Safe against future name grabs**: no conflict at release time does not mean no conflict in a
      player's setup; an extension written with the `KE`-prefixed name **won't change behaviour** if some
      mod later takes the original name
    - **Easier debugging**: on an “unknown action” error, switch to the `KE`-prefixed name to tell
      “never registered” apart from “taken by someone else”

    **Do not write both names** — they point at the same Action and it would run twice.

The full mechanism (why catching the exception is the only option, and the `Compat/` layout) is on
[Mod Compatibility](./mod-compat.md).

---

## Delayed Execution

Most actions support `Delay` and `DelayHost` attributes for delayed execution.

- `Delay`: the number of seconds to wait.
- `DelayHost`: the ID of a host that provides the delay service (must have a `FastActionHost` daemon).

If `Delay` is 0 or negative, the action runs immediately.

---

## See Also

- [Home](./../index.md) – Return to main index
- [自定义Action (中文)](./../../zh/components/actions.md) – Chinese version
- [Custom Color System](./../systems/custom-color.md) – colour value rules
- [Mod Compatibility](./mod-compat.md) – the full Action-name conflict mechanism
- [Phase Swift System](./../systems/phase-swift.md)
- [Custom Trial System](./../systems/custom-trial.md)
- [VM Attack System](./../systems/vm-attack.md)
- [Aircraft Daemon System](./../systems/aircraft.md)
- [Custom Timer System (Clock)](./../systems/clock.md)
- [Custom Node Icon System](./../systems/node-icon.md)
- [Custom Ending System (StartEnding)](./../systems/custom-ending.md)
- [PortHackV2 Cracker](./../systems/port-hack-v2.md) – the target-cracking flow and its action hooks
