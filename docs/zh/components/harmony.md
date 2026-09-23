# Harmony 补丁

KernelExtensions 通过 Harmony 补丁增强原版游戏。所有补丁在模组加载时注入、卸载时自动移除。

## 补丁列表

目前共 **20 个**补丁类：19 个位于 `Patches/` 目录（其中 `PhaseSwiftLayoutPatch.cs` 内含 2 个类），
1 个位于 `Compat/Stuxnet/`。

下表按**目标方法**列出。与其他模组共存时，可以据此判断双方是否可能作用于同一个方法。

| 补丁类 | 目标方法 | 类型 | 作用 |
|--------|----------|------|------|
| `MainMenuWatermarkPatch` | `MainMenu.DrawBackgroundAndTitle` | Prefix | 主菜单彩虹水印（`<Watermark>false</Watermark>` 可关闭） |
| `OverlayPatch` | `OS.drawModules` / `OS.Update` | Postfix | 全局飞机高度计覆盖层 |
| `CrashModuleVMAttackPatch` | `CrashModule.Update`<br>`CrashModule.Draw` | Prefix<br>Transpiler | 注入自定义 VM 攻击流程并替换错误消息 |
| `NodeIconRenderPatch` | `DisplayModule.GetComputerImage` | Postfix | 节点图标（`SetNodeIcon`）的纹理替换 |
| `ScreenBleedWCCPatch` | `OS.Update`<br>`ActiveEffectsUpdater.CancelScreenBleedEffect` | Postfix<br>Postfix | WCC 全屏警告的计时与绘制、与原版中止联动 |
| `TitleBannerPatches` | `OS.LoadContent` / `OS.Update` / `OS.Draw` | Postfix | 标题横幅（`ShowTitle`）的创建与绘制 |
| `CustomColorPatch` | `ThemeManager.Update` | Prefix | 动态色（CustomColor）每帧刷新 |
| `MusicManagerSuppressPatch` | `MusicManager.playSong`<br>`MusicManager.playSongImmediatley`<br>`MusicManager.transitionToSong` | Prefix | PS 运行期间吞掉原版播放入口 |
| `PhaseSwiftAudioPatch` | `OS.Update` | Postfix | PS 音频缓冲推进与交叉淡入 |
| `PhaseSwiftAudioVisualizerPatch` | `AudioVisualizer.Draw` | Prefix + Postfix | 可视化层接入 |
| `PhaseSwiftCleanupPatch` | `MainMenu.resetOS` | Postfix | PS 运行时状态与预设缓存清理 |
| `PhaseSwiftConnectionPatch` | `Programs.connect` | Prefix | PS 受控节点下的连接处理 |
| `PhaseSwiftLayoutPatch` | `ThemeManager.switchThemeLayout` | Prefix | 布局保护（切场景时抑制布局重置） |
| `PhaseSwiftLayoutResetPatch` | `OS.Update` | Prefix | 布局重置时机跟踪 |
| `PhaseSwiftVisualizationInjector` | `MediaPlayer.GetVisualizationData` | Prefix | 注入 PS 的可视化数据 |
| `PorthackAutoPatch` | `Hacknet.PortHackExe.Update` | Postfix（**反射安装**） | `AutoOnPorthack` 的心碎触发 |
| `PorthackHeartDisplayPatch` | `DisplayModule.doCommandModule` | Prefix | 心脏节点显示增强 |
| `IRCLogInjector` | `FileEntry.init` | Prefix | 注入自定义 IRC 日志（`<SkipVanillaIRCLogs>` 可跳过原版） |
| `PatchAccountName` | `SavefileLoginScreen.Advance` | Prefix | 登录界面的账号名处理 |
| `PatchStuxnetDrawFGamemodeMenu` | `SavefileLoginScreen.ResetForNewAccount`<br>`SavefileLoginScreen.Draw` | Postfix<br>Prefix（**条件安装**） | Stuxnet 兼容：游戏模式菜单绘制；仅 Stuxnet 存在时安装 |

## 安装方式

绝大多数补丁用 `[HarmonyPatch]` 特性声明，由主入口的 `_harmony.PatchAll()` 统一安装。
两类例外走**手动安装**：

- **internal 类型**（如原版 `PortHackExe`）：KE 无法在编译期引用，改为运行时反射
  （`AccessTools.TypeByName` + `AccessTools.Method` + `harmony.Patch`）
- **条件安装**（如 Stuxnet 兼容补丁）：仅当对应第三方插件存在时才安装，插件不存在则跳过

## 共存提示

- **`MusicManagerSuppressPatch`** 会吞掉原版播放入口——与第三方音频模组同用时需注意，见
  [与第三方模组兼容](./mod-compat.md)
- **`PhaseSwift*` 系列**补丁常驻，但只在 PS 运行时生效（`PhaseSwiftManager.IsRunning` 开关）
- **`PatchStuxnetDrawFGamemodeMenu`** 对 Stuxnet 是**软依赖**，零硬引用
- **`PorthackAutoPatch`** 的目标类型是 internal，因此用反射安装

## 技术细节

- 补丁统一通过静态 `Harmony` 实例注入（ID：`com.LDTchara.KernelExtensions`）
- 模组卸载时调用 `UnpatchSelf()` 移除所有补丁（含手动安装的那些），无残留
- 与其它模组的冲突面、以及兼容层的组织约定，见[与第三方模组兼容](./mod-compat.md)

---

## 另请参阅

- [首页](./../index.md) – 返回主索引
- [Patches & Harmony (English)](./../../en/components/harmony.md) – 英文版
- [与第三方模组兼容](./mod-compat.md) – 冲突面与 `Compat/` 架构
- [自定义动态色系统](./../systems/custom-color.md) – `CustomColorPatch` 背后的颜色引擎
