# 可执行程序

KernelExtensions 提供了四个自定义可执行程序。KE 在加载时把它们注册到对应的 `#名称#` 自替换符上：

| 程序 | 注册名 | 作用 |
|------|--------|------|
| `CustomTrial` | `#CUSTOMTRIAL#` | 运行由 XML 配置驱动的多阶段试炼 |
| `PhaseSwift` | `#PHASESWIFT#` | 相位穿梭：多场景拓扑 + 多轨音乐 |
| `EffectsPlayer` | `#EFFECTS#` | 独立播放原版特效 |
| `WPTEST` | `#WPTEST#` | 动态壁纸测试程序 |

!!! warning "运行前必须让文件真的存在"
    注册**只**让自替换符可解析，**不会把文件放进节点**。玩家要能运行这些程序，
    必须先在自己电脑的 `bin/` 里存在对应文件——在内容 XML 中声明即可：

    ```xml
    <file path="bin" name="CustomTrial.exe">#CUSTOMTRIAL#</file>
    ```

    存档生成时 `#CUSTOMTRIAL#` 会被替换为真正的程序内容，玩家才能运行；文件缺失则无法运行。

- **CustomTrial**：通过 `CustomTrial_` 开头的 Flag 指定要加载的配置（例如 `CustomTrial_MyTrial`）。
  详细用法、配置说明和可用特效请参阅 **[自定义试炼系统](./../systems/custom-trial.md)** 页面。

---

## 另请参阅

- [首页](./../index.md) – 返回主索引
- [Executables (English)](./../../en/components/executables.md) – 英文版