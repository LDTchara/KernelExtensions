# Custom Timer System (Clock)

**Clock** is a general-purpose timer that repeatedly executes a sequence of Actions at a fixed interval.

It is positioned as a **story asset** — each Clock is its own XML file, placed anywhere in the extension (convention: a `Clocks/` directory) and referenced by file path from `ClockStart`. There is **no central registration file**.

It is a **system-level timer** (hooked to `os.UpdateSubscriptions`) and does **not** depend on a DelayHost node (unlike vanilla `Delay`).

!!! info "Applies to"
    This page covers KernelExtensions **0.7**. Related actions: `ClockStart`, `ClockStop`.

---

## Overview

- Start: `<ClockStart Filepath="Clocks/traceFlash.xml" />`
- Stop by ID: `<ClockStop ClockID="traceFlash" />` (recommended)
- Stop by path: `<ClockStop Filepath="Clocks/traceFlash.xml" />` (convenience channel, equivalent — ID wins if both are given)
- Persistence: running Clocks are saved and restored automatically — see [Data Persistence](#data-persistence)
- A `Times=1` Clock is simply a **DelayHost-free delayed execution** (`Interval` acts as the delay)

---

## Clock File Structure

```xml
<Clock ID="traceFlash" Interval="5.0" Times="3" Duration="60" OnComplete="Clocks/done.xml">
    <Actions>
        <TerminalType Text="!WARNING!" />
        <FlashScreen Color="Red" Duration="2.0" />
    </Actions>
</Clock>
```

| Attribute | Required | Description |
|-----------|:--------:|-------------|
| `ID` | ✅ | Identifier for `ClockStop` / deduplication; omitted → falls back to the file name without extension |
| `Interval` | ✅ | Trigger interval in seconds; must be `> 0` (invalid values are refused with a warning) |
| `Times` | ❌ | Loop count limit; `0` / omitted / negative = infinite; stops automatically when exhausted |
| `Duration` | ❌ | Total runtime limit in seconds; whichever of `Times` / `Duration` hits first stops the Clock |
| `OnComplete` | ❌ | Action file executed once when the Clock stops by exhaustion (supports both `<Actions>` / `<ConditionalActions>` roots) |
| `<Actions>` | ✅ | Sequence executed on **every trigger** (a pre-loaded unconditional instantly set) |

---

## Trigger Semantics

- After starting, the Clock **waits `Interval` before its first trigger** (aligned with vanilla Delay's "execute after delay"); the timer then resets to `Interval` after each trigger, keeping a steady beat
- `Times` counts **trigger count** (each trigger runs the `<Actions>` sequence once and counts as 1); the Clock stops when `TimesElapsed >= Times` or `Duration` is reached — whichever comes first
- **`OnComplete` only fires on "stopped by exhaustion"**: a manual `ClockStop` (story cancellation) does not fire it, and neither does a reset caused by a repeated `ClockStart`
- Time spent by the Actions themselves is **not counted** against `Interval` — the Clock only "initiates"; inner `Delay` is handled by the vanilla `DelayableActionSystem`, so the beat is never dragged by long-running Actions

### Execution Order

- Each Clock counts independently and never waits for another (neither parallel nor dependency-serial)
- Clocks that expire on the same frame run their Actions one by one in registration order
- A Clock started during execution does not trigger on the same frame
- Repeated `ClockStart` on the same `ID` → refresh (definition replaced, timing and count reset)

---

## Usage

### 1. Create a Clock file

```
ExtensionRoot/
├── Clocks/
│   ├── traceFlash.xml
│   └── done.xml
└── ...
```

```xml
<!-- Clocks/traceFlash.xml -->
<Clock ID="traceFlash" Interval="5.0" Times="3" OnComplete="Clocks/done.xml">
    <Actions>
        <TerminalType Text="!WARNING!" />
        <FlashScreen Color="Red" Duration="2.0" />
    </Actions>
</Clock>
```

### 2. Start / stop from story Actions

```xml
<ClockStart Filepath="Clocks/traceFlash.xml" />

<!-- Stop when the story reaches a certain point -->
<ClockStop ClockID="traceFlash" />              <!-- by ID (recommended) -->
<ClockStop Filepath="Clocks/traceFlash.xml" />  <!-- by path (equivalent; ID wins if both are given) -->
```

### 3. OnComplete file (runs only on exhaustion)

```xml
<!-- Clocks/done.xml -->
<ConditionalActions>
    <Instantly>
        <TerminalType Text="[CLOCK] completed!" />
    </Instantly>
</ConditionalActions>
```

---

## Data Persistence

Running Clocks are written to the save file and restored on load, so story timing survives save/reload:

- On save, **running** Clocks are written as `<ClockData>` nodes (exhausted or manually stopped Clocks are already removed and therefore never saved):

  ```xml
  <ClockData>
    <Clock Id="Inf" SourcePath="Extensions/MyExt/Clocks/Inf.xml"
           ExtensionRoot="Extensions/MyExt" TimesElapsed="159" Elapsed="159.72" Timer="0.64" />
  </ClockData>
  ```

- On load, the Clock file is reloaded by `SourcePath` (restoring the Actions definition), then `TimesElapsed` / `Elapsed` / `Timer` are restored, so timing and the `Times` / `Duration` checks continue seamlessly
- Manually stopped or exhausted Clocks do not come back after loading
- One-shot effects already "fired" at the trigger instant (FlashFade, TimedPrinter, ...) are not persisted (vanilla behaviour); Actions using `Delay` are persisted by the vanilla `DelayableActionSystem` itself, not by the Clock

---

## Edge Cases & Defences

| Case | Behaviour |
|------|-----------|
| `Interval <= 0` | `KELog.Warn` and refuse to start (prevents per-frame runaway triggering) |
| `Times < 0` | Treated as `0` (infinite) |
| `Duration <= 0` | No time limit |
| Empty `<Actions>` | Timer only (a misconfiguration is the extension author's responsibility) |
| Stopping an unknown `ClockID` / path | Silently ignored |
| Repeated `ClockStart` on the same ID | Reset to the new definition |

---

## Related Actions

### `ClockStart`

```xml
<ClockStart Filepath="Clocks/traceFlash.xml" />
```

| Attribute | Required | Description |
|-----------|:--------:|-------------|
| `Filepath` | ✅ | Clock file path (relative to the extension root) |

### `ClockStop`

```xml
<ClockStop ClockID="traceFlash" />
<ClockStop Filepath="Clocks/traceFlash.xml" />
```

| Attribute | Required | Description |
|-----------|:--------:|-------------|
| `ClockID` | ❌ | `ID` of the target Clock (recommended) |
| `Filepath` | ❌ | File path of the target Clock (equivalent channel; ID wins if both are given) |

---

## See Also

- [Home](./../index.md) – back to the main index
- [Clock 定时器系统（中文）](./../../zh/systems/clock.md) – Chinese version
- [Custom Actions](./../components/actions.md) – full list of custom actions
- [Full-screen flash: FlashScreen](./../components/actions.md) – a common action inside Clocks
