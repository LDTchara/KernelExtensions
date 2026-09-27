# Custom Daemons

KernelExtensions provides **two** custom daemons — that is the complete set:

| Daemon | Belongs to | In one line |
|--------|------------|-------------|
| `FlightDaemon` | [Aircraft Daemon System](./../systems/aircraft.md) | A complete replacement for vanilla `AircraftDaemon`: configurable crash duration, repair/crash callbacks, and a global altimeter overlay |
| `PorthackHeartDaemon` | [Custom Ending System](./../systems/custom-ending.md) | Extends the vanilla Porthack heart node: custom title, music, heartbreak timing, input locking, and completion/heartbreak callbacks |

The **usage and full option list** for each live on their own system page; below is a quick overview only.

## FlightDaemon

- A complete replacement for the vanilla `AircraftDaemon`, supporting configurable crash duration, actions
  on repair/crash, and a global altimeter overlay.
- Declare it directly in a computer's XML with `<FlightDaemon FallDuration="90" OnFailed="..." OnSaved="..." />`.

For detailed configuration options, attack/repair flows, and overlay usage, see the
**[Aircraft Daemon System](./../systems/aircraft.md)** page.

## PorthackHeartDaemon

- Extends the vanilla Porthack heart node: customisable title, music, heartbreak timing, and input locking,
  plus action files run on heartbreak/completion.
- Configurable fields: `Title`, `Music`, `FadeoutDelay`, `FadeoutDuration`, `AlignTime`, `HeartDuration`,
  `FlashOutTime`, `OnComplete`, `OnHeartbreak`, `LockInput`.
- The sequence can also be triggered explicitly with `<BreakHeart NodeID="heart" OnComplete="Actions/HeartBroken" />`;
  every Action parameter is an **override** — omit it to use the daemon's own config, write `NONE` or leave
  it empty to disable.

---

## See Also

- [Home](./../index.md) – Return to main index
- [自定义Daemon (中文)](./../../zh/components/daemons.md) – Chinese version
- [Executables](./executables.md) – the other kind of mountable object
- [Actions](./actions.md) – the action list
