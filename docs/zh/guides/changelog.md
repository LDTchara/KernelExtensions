# 更新日志

## 0.7.0 — 未发布

> 开发中 · 2026
>
> 本节只列出已完成文档化的内容，完整清单待发布时补齐。

### 新增功能

- **PortHackV2 破解程序（`#PORTHACKV2#`）**
  - 原版 `PortHack` 的可配置重制版：破解流程、时长、外观、前置条件与收尾动作全部由 XML 配置驱动。
  - 多协议**并行**推进，各自按 `StartDelay` 错开启动；每个协议独立掷成败，但两条分支的演出完全一样（只有时长不同）。
  - 根级与协议级两层动作点（`OnStart` / `OnComplete` / `OnFail`），一次性效果一律走动作文件。
  - 可选前置：防火墙、代理瘫痪、指定端口、以及**目标机已破解端口数量**（`auto` 对齐节点自身要求）。
  - 5 种柏拉图立体线框动画，走离屏渲染；每种立体绕自己的旋转对称轴自转且轴保持竖直。
  - 详见 [PortHackV2 破解程序](./../systems/port-hack-v2.md)。
- **ProxyKiller 常驻加速器（`#PROXYKILLER#`）**
  - 加速**所有正在过载的 Shell**，让代理更快瘫痪。它**自己不瘫痪任何代理** —— 运行 shell 需要目标机管理员权限，那道护栏不去拆。
  - 倍速由内存爬升线性驱动（起始 50MB）；每帧重新遍历，所以先运行、后开的 shell 一样被加速。
  - 背景为自绘的上升数据流，底部状态文字三级：`Spinning up...` → `Overdrive` → `Spinning down...`。
  - 详见[可执行程序](./../components/executables.md)。

### 改进与优化

- **颜色字符串解析统一**：命名色、`#RGB` / `#RRGGBB` / `#AARRGGBB`（Alpha 在首位）、`R,G,B[,A]` 与 CustomColor 预设名、动态色（`LDTchara` / `Rainbow` / `Gradient:…`）走同一条解析链；此前散落各处、在 FNA 下静默失效的 `ColorConverter` 兜底已移除。详见[自定义动态色系统](./../systems/custom-color.md)。
- **配置约定统一**：字符串配置项用 `NONE`（大小写不敏感）或留空表示禁用；数值配置项**负数 = 使用默认 / 继承 / 无限**（`NaN` / `Infinity` 同样回退默认）。
- **日志分级统一**：Debug（源码排错，默认关）/ Info（动作级结果）/ Warn（可恢复）/ Error（不该发生）；日志文本一律英文半角，玩家终端输出走本地化。

### 尚在测试中

以下内容已实施但尚未完成实机验证，发布前会补上细节：

- 节点连接控制 Action 三件套
- 相位穿梭：停止时的拓扑恢复策略可配置、音乐组自定义循环点
- 内置文本本地化系统的完善
- VM 攻击恢复模块的 Linux 支持
- 第三方模组兼容（音频链共存等）

## 0.6.0 — 飞机 Daemon、水印与稳定性增强

> Pre‑release · 2025

### 新增功能
- **飞机 Daemon 系统（FlightDaemon）**
  - 完全替代原版 `AircraftDaemon`，支持在目标计算机 XML 中直接配置 `<FlightDaemon FallDuration="90" OnFailed="Actions/failed.xml" OnSaved="Actions/saved.xml" />`。
  - 可独立设置坠落时长（`FallDuration`），支持立即坠落模式下的线性倒计时。
  - 坠毁或修复时分别执行 `OnFailed`、`OnSaved` 动作文件，完美融入剧情设计。
  - 提供全局高度计覆盖层，可通过 `ShowAircraftOverlay` / `HideAircraftOverlay` 在不连接目标的情况下实时查看飞机高度，坠毁时自动隐藏。
  - 新增攻击动作 `AttackAircraft`（支持 `FallDuration` 参数，优先级高于守护进程配置），以及修复动作 `UploadAircraftSysFile`。
  - 本系统由 **April_Crystal** 贡献核心实现，特此感谢。
- **主菜单动态彩虹水印**
  - 在主菜单左上角显示 `+ KernelExtensions <版本号>`，文字颜色随时间平滑流动，无回弹或卡顿。
  - 位置在 ZeroDayToolKit 水印右侧，避免重叠。
  - 扩展卸载时水印会自动消失，Harmony 补丁被统一清理。
- **新自定义 Action：`RenameNode`**
  - 可按节点 ID 修改节点名称，修改即时生效并自动持久化到存档。

### 改进与优化
- **Harmony 补丁管理**：统一使用一个静态 Harmony 实例，通过 `Unload()` 方法在扩展退出时干净移除所有补丁，避免残留影响。
- **飞机按钮逻辑修正**：`FlightDaemon` 界面中的 “Exit..” 按钮已被修正为 “Disconnect”，点击后正确执行断开连接。
- **文档全面更新**：将详细文档迁移至 GitHub Wiki，提供中英双语导航，并新增扩展作者指南、配置文件参考等页面；README 同步精简。
  （注：该 Wiki 已于 0.7 开发期间退役，文档现由主仓库 `docs/` 的 MkDocs 站点承载。）

### 错误修复
- 修复 `AttackAircraft` 在目标计算机缺少 `FlightDaemon` 时抛出 `KeyNotFoundException` 的崩溃问题。
- 修复 `CrashDelay == 0` 时可能出现的除零错误，逻辑已修正为直接立即坠毁。
- 修复 `CustomTrialExe.OnComplete` 在特定状态下调用 `MusicManager.stop()` 导致的音频访问冲突（Access Violation）。
- **保留** `FlightDaemon` 中 `AircraftFallStartsImmediately` 的字段名与拼写（原版 `AircraftDaemon` 同样写作 `AircraftFallStartsImmediatley`）—— 该字段是从原版逐字继承的备用接口，保持同名同拼写便于与参考源码对照；它恒为 `true` 且**不可通过 XML 配置**。
- 修复 `FlightDaemon` 坠毁后静态字典未被清理，可能引发内存泄漏的问题。
- 修复 `FlightDaemon` 固件重载后离开节点仍持续更新高度数据的问题，现在断开连接时会自动取消订阅。

---

## 0.5.0 — VMAttack 系统

> Pre‑release · 2025

### 新增功能
- **可配置虚拟机攻击系统（VM Attack）**
  - 新增 `LaunchVMAttack` Action，可从动作文件触发自定义虚拟机崩溃攻击。
  - 支持三种恢复模式：`FileDeletion`（删除指定文件）、`FileExists`（创建指定文件）、`Password`（输入密码）。
  - 通过 `VMATK/` 目录下的 XML 配置文件定义攻击行为，包括自定义错误消息、系统日志文本、引导对话、帮助文件、虚假文件列表、成功音乐等。
  - 恢复界面 (`FakeRecoveryModule`) 模拟终端式输出，具备系统日志滚动、引导文本逐字打印、交互按钮/密码输入框，支持多语言提示。
  - 引导文本支持行内控制指令：`||Px.x||` 停顿、`||Sx.x||` 变速、`||SR||` 恢复默认速度，且可启用“已读跳过”功能。
  - 引导文本开始播放时可同步执行一个动作文件（`ActionOnGuideTextStart`）。
  - 完美融入原版崩溃流程：利用 Harmony 补丁在系统启动日志第 50 行插入错误状态，15 秒后自动转入自定义恢复模块，体验无缝衔接。
  - 攻击解除后自动执行黑屏重启并播放成功音乐。
- **全新 Action：** `PlaySound`
  - 新增 `PlaySound` Action，用于播放扩展目录下的自定义音效（WAV 格式）。
  - 提供公共 `SoundHelper` 工具类，支持在代码中便捷调用。
- **技术改进**
  - 音乐路径解析公共化：提取 `MusicPathResolver` 工具类，统一处理扩展内音乐文件的路径查找（支持多级回退），修复了扩展文件夹名与扩展名不一致时无法播放音乐的问题。
  - 动作文件执行逻辑统一：新增 `ActionHelper` 公共类，将动作文件执行逻辑标准化，`CustomTrialExe` 与 VM 系统均使用同一套实现，提高稳定性与可维护性。

### 其他
- 调整了多处内部细节，优化了模块间的耦合，为后续功能扩展打下基础。

---

## 0.4.6

> Pre‑release · 2025

- 修复音乐路径解析中路径不正确的问题。

---

## 0.4.5 — 强制失败 & 修复集合

> Pre‑release · 2025

### 新增功能
- **强制失败 Action (`FailTrial`)**
  - 新增 `FailTrialAction`，可在动作文件中直接调用，强制当前试炼立即失败（进入 Outro 并显示“失败”文字）。支持 `Delay` 和 `DelayHost` 延迟执行。

### 错误修复
- 修复试炼失败时标题无变化：所有失败路径（超时、`ForceFail` 等）现在都会正确设置 `currentPhaseIdx = -1`，使标题显示“失败”文字。
- 修复退出时标题消失：在 `DrawPhaseTitle` 中为 `Exiting` 状态添加了临时文字逻辑，退出时继续显示“完成”或“失败”标题。
- 修复失败退出时终端多余信息：通过将失败时的 `Result` 统一设为 `Success`，并配合 `trialSucceeded` 独立控制音乐停止。
- 修复邮件摧毁径向线条受帧率影响：引入 `MAIL_LINE_INTERVAL`（1/60 秒）控制线条生成频率。
- 修复完成音效在未启用聚焦特效时播放：`glowSound` 现在仅在 `EnableTrialCompleteFocus = true` 时播放。

### 改进
- 复刻原版 SpinUp 动画缓动曲线：`DrawSpinningUp` 已完全对齐原版 DLC 试炼的线条进度算法，动画更丰富自然。

---

## 0.4.4 — 节点持久化与特效还原

> Pre‑release · 2025

### 核心系统增强
- **节点删除持久化与恢复**
  - 新增 `CustomTrialNodeStorage` 全局存储，记录试炼过程中被删除的节点索引。
  - 存档/读档时自动保存/恢复删除状态（通过 `SaveEvent` 和 `CustomTrialSaveExecutor`）。
  - 提供 `RestoreCustomTrialNodes` 自定义 Action，可在任意时机恢复节点（带高亮闪烁和特效）。
- **动作文件执行机制优化**
  - `ExecuteActionFile` 现在支持 `ConditionalActions` 和 `Actions` 两种标准格式，兼容原版及 Pathfinder 规范。
  - 使用 `EventExecutor` + `ActionsLoader` 确保自定义 Action 正确执行。
- **终端交互 Action**
  - `TerminalWriteAction`：向终端写入文本（支持 `text` 属性或元素内容，支持 `Delay` 延迟）。
  - `TerminalFocusAction`：全屏聚焦终端（遮罩变暗 + 边框扩散），支持独立的 `Duration`、`BorderDuration`、`FadeInDuration` 等参数。
- **主题切换配置**
  - XML 配置项 `ThemeToSwitch`（预设名或自定义路径）和 `ThemeFlickerDuration`，在动画开始前自动切换主题。

### 视觉效果与 UI 完善
- **邮件爆炸特效增强**：添加径向线条生成，爆炸阶段持续产生散射射线；爆炸后多段延迟红色圆圈扩散效果。
- **节点摧毁特效还原**：节点消失时添加冲击波和红色淡入淡出圆圈，安全等级越高效果越强。
- **状态标题显示**：Flickering / WaitAfterDestruction / MailIconDestroy 阶段显示本地化“正在初始化”文字。
- **终端聚焦特效**：阶段开始时 / 试炼完成时可选显示全屏遮罩 + 终端边框扩散动画。
- **退出淡出动画**：程序退出时所有界面元素随 `fade` 值平滑淡出。
- **背景网格动态效果**：`HexGridBackground` 随时间动态更新颜色强度。

### 功能与流程优化
- `OnStart` 与 `OnAnimationComplete` 并存：`OnStart` 在点击按钮后立即执行，`OnAnimationComplete` 在所有动画完成后执行。
- **可配置程序名称**：XML 中添加 `ProgramName` 字段。
- **试炼完成转连**：新增 `ConnectTarget` 和 `StopMusicOnConnect` 配置项。
- **阶段重置自定义文本**：`PhaseConfig` 新增 `ResetText` 和 `ExecuteOnPhaseStartOnReset`。
- **动态内存缩减**：新增 `DynamicRamReduction` 选项，启用后根据当前显示控件自动计算最小窗口高度。

---

## 0.4.3

> Pre‑release · 2025

- 还原至 `MusicManager` 处理音频播放。

---

## 0.4.2

> Pre‑release · 2025

- 使用 Harmony 补丁修复 `MusicManager` 的相对路径问题。

---

## 0.4.1

> Pre‑release · 2025

- 试炼成功或失败后自动停止当前音乐。

---

## 0.4.0 — 节点持久化与恢复

> Pre‑release · 2025

### 新增功能
- **节点删除持久化与恢复**：试炼过程中被摧毁的节点会随存档保存，可通过自定义 Action `RestoreCustomTrialNodes` 在任意时机以动画特效逐个恢复。
- **主题切换配置**：支持在试炼开始时自动切换主题，可指定预设名称或自定义主题文件路径，并配置切换时的闪烁时长。
- **节点摧毁后等待时间**：新增 `PostDestructionDelay` 配置项，可在节点摧毁完成后插入自定义等待时间。
- **动画完成后执行 Action**：原 `OnStart` 更名为 `OnAnimationComplete`，执行时机移至所有动画结束后。
- **锁定界面退出按钮**：试炼锁定时显示右键可关闭的“Exit”按钮。
- **未开始时退出恢复音乐**：若玩家在 `NotStarted` 状态 kill 程序，会自动恢复进入前的背景音乐。

### 改进
- 修复邮件图标爆炸后顶部栏颜色未恢复的问题。
- 优化主题切换与顶部栏颜色的保存顺序。
- 调整 `OnAnimationComplete` 的触发时机。

---

## 0.3.5

> Pre‑release · 2025

- **内存动态缩减（RAM Reduction）**：进入破解阶段后，可配置延迟时间和缩减时长，将程序内存占用从 190 线性降低至 88。缩减期间 UI 元素按比例平滑缩放。
- 使用模式匹配简化反射判断，移除未使用的参数和变量。

---

## 0.3.4

> Pre‑release · 2025

- 阶段标题和副标题仅显示在程序窗口中央，不再输出到终端。
- 试炼失败时不再显示额外的失败信息。

---

## 0.1.0 ~ 0.3.3

> Pre‑release · 2025

- 早期开发版本。

---

## 另请参阅

- [首页](./../index.md) – 返回主索引
- [Changelog (English)](./../../en/guides/changelog.md) – 英文版