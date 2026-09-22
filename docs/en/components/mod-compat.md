# Mod Compatibility

KernelExtensions is a Pathfinder extension: it runs in the **same process** as other third-party mods,
sharing Pathfinder's registries and several static entry points of the base game. This page documents the
**conflict surfaces we have verified in practice**, how KE handles them, and the conventions for adding new
compatibility code.

> Verified environment: Stuxnet 2.3.1 + Stuxnet.Audio 0.2.0 (SASS) + HnpfMcpConnector + IRCEnhancements +
> HacknetFontReplace + KernelExtensions.

## 1. Action Name Conflicts

Third-party mods may register **generic short Action names** — a real example: `Stuxnet.Audio` registers
`PlaySound`.

Pathfinder's `ActionManager.RegisterAction` writes into its registry with `Dictionary.Add`, so a duplicate
name throws `ArgumentException`. The consequence is not "one Action stops working" but **the whole KE
plugin fails to load**:

```
System.ArgumentException: An item with the same key has already been added.
  at Pathfinder.Action.ActionManager.RegisterAction(...)
  at KernelExtensions.KernelExtensions.Load()
```

Pathfinder exposes no public API to query whether a name is already registered (the `CustomActions`
dictionary is private; only `GetXmlNameFor(Type)` and `UnregisterAction(...)` are public), so there is **no
way to check before registering**, and `UnregisterAction` must not be used to make room for others.
Catching the exception is the only option.

KE's answer is `RegisterActionWithFallback<T>(xmlName, fallbackName)`: on a duplicate it falls back to an
alternate name and logs a `Warn` instead of throwing. It is applied to the **15 most conflict-prone generic
short names** (fallback name = `KE` + original); names that carry KE context (`PhaseSwift*` / `LinkControl*`
/ `LaunchVMAttack` / `CustomTrial*` / `Aircraft*` / `StartScreenBleedEffectWCC`) **do not take part in the
fallback**. Full table and authoring advice:
[Actions](./actions.md#action-name-conflicts-and-fallback).

!!! warning "Side effect when coexisting"
    In a conflicting environment KE's `PlaySound` is actually registered as `KEPlaySound`; an extension
    writing `<PlaySound>` gets the third-party one. Use the prefixed name explicitly when coexisting, and
    **never write both names** — only the one registered first takes effect.

## 2. Audio Pipeline: PhaseSwift × Stuxnet.Audio

SASS takes over `MusicManager` by default (`ReplaceMusicManager=true`) and hooks the **downstream**
`MediaPlayer.Play(Song)`; PhaseSwift intercepts the **upstream entries** (`playSong` / `playSongImmediatley`
/ `transitionToSong`). So while PS runs, third parties receive no new playback trigger — but a track
**already playing** must be stopped by PS actively.

Two implementation points, both of which overturned a paper analysis when tested on a real machine:

- **`MusicManager.stop()` must be unconditional**: earlier builds guarded it with
  `if (MusicManager.isPlaying)`, but a third party playing through **its own DSEI** never makes the vanilla
  `isPlaying` true → the guard never passes, `stop()` was never called, and both played at once. `stop()` is
  idempotent, so dropping the guard is the fix.
- **Loading a save needs a watch window**: loading goes through the same `Start()`, but the third party
  starts playback **asynchronously** (`Started song loader thread` in the log), so a one-shot `stop()` lands
  in the moment before playback begins and misses. KE queries once immediately in `Start()`; if nothing is
  playing it opens a **20-second watch window**, asking every frame, and stops precisely the moment playback
  is detected — then **closes the window** (it does not stop repeatedly).

Volume and visualiser: SASS conditionally takes over `MusicManager.getVolume()`, so PS's volume tracking may
read SASS's volume (near-zero impact). The visualiser layer is orthogonal — PS fakes `MediaPlayer.State`
while SASS rewrites the same check via IL. See
[Phase Swift System (PhaseSwift)](./../systems/phase-swift.md).

## 3. The `Compat/` Layout

New third-party compatibility code should follow this layout (modelled on ZDTK):

```
Compat/
├── ModCompats.cs              # single entry point: dispatch and summary, names no specific mod
└── Stuxnet/
    ├── StuxnetAudioCompat.cs  # SASS audio (reflective probe + precise stop)
    └── StuxnetMenuCompat.cs   # formerly Patches/PatchStuxnetDrawFGamemodeMenu.cs
```

- Per-mod implementations live in `Compat/<mod>/`; `ModCompats` only **dispatches and summarises**
- **Callers never name a specific mod** (PS uses a neutral `_conflictWatchFrames`)
- Directories group by **mod**, class names are precise about the **subsystem** (`StuxnetAudioCompat`, not a
  catch-all `StuxnetCompat`)
- Access third-party types by **reflection** only (soft dependency, zero hard reference); skip silently when
  the plugin is absent

## 4. Load and Unload Robustness

- **Whole-`Load()` containment**: on exception it logs `KELog.Error` and **returns success**. The reason is
  that Pathfinder registration is an **irreversible side effect** — if the exception escapes, BepInEx marks
  the plugin failed while the registrations it already made stay in effect, and it will **never call that
  plugin's `Unload()`**, leaving a half-initialised state forever.
- **Unload**: Pathfinder's Action / Condition / Administrator / Command / Daemon / Executable managers all
  subscribe to `onPluginUnload` and are **cleaned up per assembly**; KE does not unregister manually (doing
  so could remove someone else's entries). KE only cleans up its own global state — the volume left faded by
  the ending module, and the node-icon texture cache.

## 5. Known Limitations

- A third party playing through its own DSEI **cannot be observed** via vanilla `MusicManager.isPlaying`;
  KE can only probe specific mods.
- When coexisting with a same-named Action, extensions must use the explicit `KE`-prefixed name (section 1).
- KE does not patch defects on the third party's side.

---

## See Also

- [Home](./../index.md) – back to the main index
- [与第三方模组兼容（中文）](./../../zh/components/mod-compat.md) – Chinese version
- [Actions](./actions.md) – full table for Action name conflicts and fallback
- [Phase Swift System (PhaseSwift)](./../systems/phase-swift.md) – coexistence boundary and playback control
- [Patches & Harmony](./harmony.md) – patch inventory
