# 配置文件

KernelExtensions 使用 XML 配置文件来驱动各系统。所有路径均相对于扩展根目录。

## 试炼配置（TrialConfig）

- 存放位置：`Trial/<名称>.xml`
- 根元素：`<TrialConfig>`
- 包含全局设置（特效、时间、颜色等）和 `<Phases>` 阶段列表。
- 详细说明：[自定义试炼系统](./../systems/custom-trial.md)
- 示例XML：[ExampleTrial.xml](https://github.com/LDTchara/KernelExtensions/blob/main/XMLExamples/ExampleTrial.xml)

## VM 攻击配置（VMAttackConfig）

- 存放位置：`VMATK/<名称>.xml`
- 根元素：`<VMAttackConfig>`
- 包含恢复模式、系统日志、引导文本、虚假文件等设置。
- 详细说明：[VM攻击系统](./../systems/vm-attack.md)
- 示例XML：[MyAttack.xml](https://github.com/LDTchara/KernelExtensions/blob/main/XMLExamples/MyAttack.xml)

## 飞机 Daemon 配置

- 直接在目标计算机中添加即可。
- 可配置属性：`FallDuration`、`OnFailed`、`OnSaved`。
- 详细说明：[飞机Daemon系统](./../systems/aircraft.md)

## 通用规则

- 所有 `file` 属性中指定的路径均为**相对于扩展根目录**的路径。
- 颜色字段支持名称（`Red`）、十六进制（`#FF0000`）或我的名字。
- 音乐字段可使用纯文件名、相对路径或 DLC 路径，解析由 `MusicPathResolver` 自动完成。

---

## 另请参阅

- [首页](./../index.md) – 返回主索引
- [Configuration Files (English)](./../../en/components/configuration.md) – 英文版