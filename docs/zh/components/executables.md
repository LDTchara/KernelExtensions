# 可执行程序

KernelExtensions 提供了四个自定义可执行程序（均由 KE 在加载时自动注册，`#名称#` 即其自替换符，**无需**手动放入玩家 `bin/` 文件夹）：

| 程序 | 注册名 | 作用 |
|------|--------|------|
| `CustomTrial` | `#CUSTOMTRIAL#` | 运行由 XML 配置驱动的多阶段试炼 |
| `PhaseSwift` | `#PHASESWIFT#` | 相位穿梭：多场景拓扑 + 多轨音乐 |
| `EffectsPlayer` | `#EFFECTS#` | 独立播放原版特效 |
| `WPTEST` | `#WPTEST#` | 动态壁纸测试程序 |

- **CustomTrial**：通过 `CustomTrial_` 开头的 Flag 指定要加载的配置（例如 `CustomTrial_MyTrial`）。
  详细用法、配置说明和可用特效请参阅 **[自定义试炼系统](./../systems/custom-trial.md)** 页面。

---

## 另请参阅

- [首页](./../index.md) – 返回主索引
- [Executables (English)](./../../en/components/executables.md) – 英文版