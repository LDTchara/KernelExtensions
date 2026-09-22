# 与第三方模组兼容

KernelExtensions 是 Pathfinder 扩展，与其它第三方模组运行在**同一个进程**里，共享 Pathfinder 的
注册表与游戏本体的若干静态入口。本页说明**已实测的冲突面**、KE 的处理方式，以及新增兼容代码的约定。

> 实测环境：Stuxnet 2.3.1 + Stuxnet.Audio 0.2.0（SASS）+ HnpfMcpConnector + IRCEnhancements +
> HacknetFontReplace + KernelExtensions。

## 一、Action 注册名冲突

第三方模组可能注册**通用的短 Action 名**——实例：`Stuxnet.Audio` 注册了 `PlaySound`。

Pathfinder 的 `ActionManager.RegisterAction` 用 `Dictionary.Add` 写入注册表，**重名会抛
`ArgumentException`**。后果不是「某个 Action 失效」，而是**整个 KE 插件加载失败**：

```
System.ArgumentException: An item with the same key has already been added.
  at Pathfinder.Action.ActionManager.RegisterAction(...)
  at KernelExtensions.KernelExtensions.Load()
```

Pathfinder 没有「按名查询是否已注册」的公开 API（`CustomActions` 字典是私有的，公开的只有
`GetXmlNameFor(Type)` 与 `UnregisterAction(...)`），因此**无法「先查再注册」**，也不可用
`UnregisterAction` 去给别人腾位——只能靠异常兜底。

KE 的对策是 `RegisterActionWithFallback<T>(xmlName, fallbackName)`：重名时退回备用名并记一条 `Warn`，
不再抛出。它对 **15 个最易被占用的通用短名**启用（备用名 = `KE` + 原名）；带 KE 语境的独特名
（`PhaseSwift*` / `LinkControl*` / `LaunchVMAttack` / `CustomTrial` 系 / `Aircraft` 系 /
`StartScreenBleedEffectWCC`）**不参与回退**。完整对照表与写作建议见
[自定义 Action](./actions.md#action-名冲突与回退)。

!!! warning "共存时的副作用"
    在重名环境下，KE 的 `PlaySound` 实际注册为 `KEPlaySound`；扩展若写 `<PlaySound>`，拿到的是
    第三方那一个。与第三方共存时请显式使用带前缀的名字，两种名字**不要同时写**。

## 二、音频播放链：PhaseSwift × Stuxnet.Audio

SASS 默认接管 `MusicManager`（`ReplaceMusicManager=true`），把转发挂在**下游**的
`MediaPlayer.Play(Song)`；PhaseSwift 拦的是**上游入口**（`playSong` / `playSongImmediatley` /
`transitionToSong`）。因此 PS 运行期间第三方收不到新的播放触发——但**已经在播**的音轨需要 PS 主动停掉。

两处实现要点都由实机测试推翻过纸面结论：

- **必须无条件 `MusicManager.stop()`**：早期版本带 `if (MusicManager.isPlaying)` 守卫，而使用
  **自有 DSEI** 播放的第三方不会让原版 `isPlaying` 为真 → 守卫不成立、`stop()` 从未被调用 → 两套同响。
  `stop()` 本身幂等，去掉守卫即可。
- **读档场景需要观察窗口**：读档走同一个 `Start()`，但第三方的起播是**异步的**
  （日志中可见 `Started song loader thread`），一次性 `stop()` 会落在「还没开始播」的瞬间而漏掉。
  KE 的做法是 `Start()` 立即查一次，未在播则开 **20 秒观察窗口**，每帧询问一次，检测到在播就精确
  停掉并**收束窗口**（不是反复 stop）。

音量与可视化层：SASS 会条件性接管 `MusicManager.getVolume()`，PS 的音量跟随可能读到 SASS 的音量
（后果接近零）；可视化层两者正交——PS 伪造 `MediaPlayer.State`，SASS 用 IL 替换同一处判断。
细节见 [相位穿梭系统（PhaseSwift）](./../systems/phase-swift.md)。

## 三、`Compat/` 架构

新增第三方兼容请遵循以下布局（参照 ZDTK）：

```
Compat/
├── ModCompats.cs              # 总入口：分发与汇总，不点名具体模组
└── Stuxnet/
    ├── StuxnetAudioCompat.cs  # SASS 音频（反射探测 + 精确停止）
    └── StuxnetMenuCompat.cs   # 原 Patches/PatchStuxnetDrawFGamemodeMenu.cs
```

- 各模组的兼容实现放 `Compat/<模组名>/`；`ModCompats` 只做**分发与汇总**
- **调用方不点名具体模组**（例如 PS 用的是 `_conflictWatchFrames` 这类中性命名）
- 目录按**模组**分组，类名按**子系统**精确（`StuxnetAudioCompat`，而非笼统的 `StuxnetCompat`）
- 对第三方类型的访问一律走**反射**（软依赖、零硬引用）；插件不存在时静默跳过

## 四、加载与卸载的健壮性

- **`Load()` 整体兜底**：捕获异常后记 `KELog.Error` 并**返回成功**。原因是 Pathfinder 的注册是
  **不可回滚的副作用**——若异常逃出，BepInEx 会把这个插件标记为失败，但已完成的注册仍然生效，
  而且**不会再调用它的 `Unload()`**，半初始化状态将永久残留。
- **卸载**：Pathfinder 的 Action / Condition / Administrator / Command / Daemon / Executable 各
  Manager 都订阅了 `onPluginUnload`，**按程序集自动清理**；KE 不手动反注册（手动清可能清到别人的）。
  KE 只额外处理自己的全局状态——结局淡出残留的音量、节点图标纹理缓存。

## 五、已知限制

- 使用自有 DSEI 播放的第三方模组，其播放状态**无法通过原版 `MusicManager.isPlaying` 观察**；
  KE 只能针对具体模组做探测。
- 与第三方同名 Action 共存时，扩展需显式使用 `KE` 前缀名（见第一节）。
- 第三方自身的缺陷 KE 不会代为修补。

---

## 另请参阅

- [首页](./../index.md) – 返回主索引
- [Mod Compatibility (English)](./../../en/components/mod-compat.md) – 英文版
- [自定义 Action](./actions.md) – Action 名冲突与回退的完整对照表
- [相位穿梭系统（PhaseSwift）](./../systems/phase-swift.md) – 共存边界与播放控制
- [Harmony 补丁](./harmony.md) – 补丁清单
