# 自定义 Daemon

KernelExtensions 提供了两个自定义守护进程：**FlightDaemon**（飞机）与 **PorthackHeartDaemon**（心脏结局）。

## FlightDaemon

- 完全替代原版的 `AircraftDaemon`，支持可配置的坠落时长、修复/坠毁动作触发，以及全局高度计覆盖层。
- 在计算机 XML 中直接声明 `<FlightDaemon FallDuration="90" OnFailed="..." OnSaved="..." />` 即可使用。

详细的配置选项、攻击与修复流程，以及覆盖层的使用方法，请参阅 **[飞机Daemon系统](./../systems/aircraft.md)** 页面。

## PorthackHeartDaemon

- 扩展原版 Porthack 心脏节点的行为：可自定义标题、音乐、心碎时序与输入锁定，并在心碎/完成时执行指定动作文件。
- 可配置项：`Title`、`Music`、`FadeoutDelay`、`FadeoutDuration`、`AlignTime`、`HeartDuration`、`FlashOutTime`、`OnComplete`、`OnHeartbreak`、`LockInput`。
- 也可由 `<BreakHeart NodeID="heart" OnComplete="Actions/HeartBroken" />` 显式触发心碎序列；
  Action 参数均为**覆盖项**——不写 = 用 daemon 自身配置，写 `NONE` 或留空 = 显式禁用。

---

## 另请参阅

- [首页](./../index.md) – 返回主索引
- [Daemons (English)](./../../en/components/daemons.md) – 英文版