# Harmony 补丁

KernelExtensions 通过 Harmony 进行了几项轻量级补丁，以增强原版游戏功能。所有补丁均在模组加载时注入，卸载时自动移除。

## 补丁列表

| 补丁类 | 作用 |
|--------|------|
| `MainMenuWatermarkPatch` | 在主菜单绘制动态彩虹流动水印。 |
| `OverlayPatches` | 在 `OS.drawModules` 末尾添加全局飞机高度计覆盖层。 |
| `CrashModuleVMAttackPatch` | 修改原版 `CrashModule` 的 `Update` 逻辑，注入自定义 VM 攻击流程，并替换错误消息。 |

## 技术细节

- 补丁统一通过静态 `Harmony` 实例注入（ID：`com.LDTchara.KernelExtensions`）。
- 模组卸载时调用 `UnpatchSelf()` 移除所有补丁，无残留。
- 补丁与其它模组的兼容性良好，使用了标准的 `Prefix` 和 `Transpiler` 技术。

---

## 另请参阅

- [首页](./../index.md) – 返回主索引
- [Patches & Harmony (English)](./../../en/components/harmony.md) – 英文版