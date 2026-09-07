# Custom Daemons

KernelExtensions currently provides one custom daemon: **FlightDaemon** (the aircraft daemon).

- A complete replacement for the vanilla `AircraftDaemon`, supporting configurable crash duration, actions on repair/crash, and a global altimeter overlay.
- Declare it directly in a computer's XML with `<FlightDaemon FallDuration="90" OnFailed="..." OnSaved="..." />`.

For detailed configuration options, attack/repair flows, and overlay usage, see the **[Aircraft Daemon System](./aircraft.md)** page.

A **Porthack heart daemon** that allows custom post‑Porthack actions is planned for a future release.

---

## See Also

- [Home](./index.md) – Return to main index
- [自定义Daemon (中文)](./../zh/daemons.md) – Chinese version