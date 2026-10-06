# 可执行程序

KernelExtensions 向 Pathfinder 注册了 **4 个**自定义可执行程序，每个绑定一个 `#名称#` 自替换符。
下表就是归这一类的**全部**内容。

| 程序 | 注册名 | 所属功能 | 状态 |
|------|--------|----------|------|
| `CustomTrial` | `#CUSTOMTRIAL#` | [自定义试炼系统](./../systems/custom-trial.md) | ✅ 可用 |
| `PhaseSwift` | `#PHASESWIFT#` | [相位穿梭系统](./../systems/phase-swift.md) | ✅ 可用 |
| `EffectsPlayer` | `#EFFECTS#` | 特效播放（动态壁纸 / 视频播放的底层能力） | ⚠️ 所属功能未完成 |
| `WPTEST` | `#WPTEST#` | 动态壁纸（测试程序） | ⚠️ 所属功能未完成 |

!!! info "后两个为什么看着像「杂项」"
    `#EFFECTS#` 与 `#WPTEST#` 属于**尚未完成**的功能 —— 动态壁纸与视频播放（都排在 0.8 计划内）。
    它们现在就能注册、自替换符也能解析，但**没有配套的稳定用法文档**，因此请把它们当作
    **实验性入口**看待，不要写进正式发布的内容。

!!! warning "运行前必须让文件真的存在"
    注册**只**让自替换符可解析，**不会把文件放进节点**。玩家要能运行这些程序，
    必须先在自己电脑的 `bin/` 里存在对应文件——在内容 XML 中声明即可：

    ```xml
    <file path="bin" name="CustomTrial.exe">#CUSTOMTRIAL#</file>
    ```

    存档生成时 `#CUSTOMTRIAL#` 会被替换为真正的程序内容，玩家才能运行；文件缺失则无法运行。

## CustomTrial 的调用方式

通过 `CustomTrial_` 开头的 Flag 指定要加载的配置（例如 `CustomTrial_MyTrial`）。
详细用法、配置说明和可用特效请参阅 **[自定义试炼系统](./../systems/custom-trial.md)** 页面。

## PhaseSwift 的调用方式

通过 `PhaseSwift_` 开头的 Flag 指定要加载的配置（例如 `PhaseSwift_MyConfig`）。
详细用法、场景配置与音乐组说明请参阅 **[相位穿梭系统](./../systems/phase-swift.md)** 页面。

---

## 另请参阅

- [首页](./../index.md) – 返回主索引
- [Executables (English)](./../../en/components/executables.md) – 英文版
- [自定义 Daemon](./daemons.md) – 另一类可挂载对象
- [自定义 Action](./actions.md) – 动作清单
