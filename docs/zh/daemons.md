# 自定义 Daemon

KernelExtensions 目前提供了一个自定义守护进程：**FlightDaemon**（飞机守护进程）。

- 完全替代原版的 `AircraftDaemon`，支持可配置的坠落时长、修复/坠毁动作触发，以及全局高度计覆盖层。
- 在计算机 XML 中直接声明 `<FlightDaemon FallDuration="90" OnFailed="..." OnSaved="..." />` 即可使用。

详细的配置选项、攻击与修复流程，以及覆盖层的使用方法，请参阅 **[飞机Daemon系统](./aircraft.md)** 页面。

未来计划添加 **Porthack 心脏 Daemon**，允许自定义 Porthack 完成后的动作。

---

## 另请参阅

- [首页](./index.md) – 返回主索引
- [Daemons (English)](./../en/daemons.md) – 英文版