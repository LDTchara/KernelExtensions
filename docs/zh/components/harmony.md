# Harmony 补丁

KernelExtensions 通过 Harmony 进行了多项补丁，以增强原版游戏功能。所有补丁均在模组加载时注入，卸载时自动移除。

## 补丁列表

目前共 **19 个**补丁类（完整清单以源码 `Patches/` 目录为准）。主要补丁：

| 补丁类 | 作用 |
|--------|------|
| `MainMenuWatermarkPatch` | 在主菜单绘制动态彩虹流动水印（可用 `KE-Config.xml` 的 `<Watermark>false</Watermark>` 关闭）。 |
| `OverlayPatch` | 在 `OS.drawModules` 末尾添加全局飞机高度计覆盖层。 |
| `CrashModuleVMAttackPatch` | 修改原版 `CrashModule` 的 `Update` 逻辑，注入自定义 VM 攻击流程，并替换错误消息。 |
| `NodeIconRenderPatch` | 节点图标（`SetNodeIcon`）的纹理替换渲染。 |
| `ScreenBleedWCCPatch` | 全屏警告特效（`StartScreenBleedEffectWCC`）的绘制与原版中止联动。 |
| `TitleBannerPatches` | 标题横幅（`ShowTitle`）的绘制。 |
| `CustomColorPatch` | 自定义动态色（CustomColor）支持。 |
| `PorthackAutoPatch` / `PorthackHeartDisplayPatch` | Porthack 心脏节点的自动补丁与显示增强。 |
| `PhaseSwift*`（6 个：`Audio` / `AudioVisualizer` / `Cleanup` / `Connection` / `Layout` / `VisualizationInjector`） | 相位穿梭系统：音频链路、可视化注入、布局保护、连接处理与清理。 |
| 其余（`IRCLogInjector`、`MusicManagerSuppressPatch`、`PatchAccountName`、`PatchStuxnetDrawFGamemodeMenu`） | 见源码 `Patches/` 目录。 |

## 技术细节

- 补丁统一通过静态 `Harmony` 实例注入（ID：`com.LDTchara.KernelExtensions`）。
- 模组卸载时调用 `UnpatchSelf()` 移除所有补丁，无残留。
- 补丁与其它模组的兼容性良好，使用了标准的 `Prefix` 和 `Transpiler` 技术。

---

## 另请参阅

- [首页](./../index.md) – 返回主索引
- [Patches & Harmony (English)](./../../en/components/harmony.md) – 英文版