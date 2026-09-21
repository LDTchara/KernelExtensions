# 配置文件

KernelExtensions 使用 XML 配置文件来驱动各系统。所有路径均相对于扩展根目录。

## 试炼配置（TrialConfig）

- 存放位置：`Trial/<名称>.xml`
- 根元素：`<TrialConfig>`
- 包含全局设置（特效、时间、颜色等）和 `<Phases>` 阶段列表。
- 详细说明：[自定义试炼系统](./../systems/custom-trial.md)
- 示例XML：[Trial_Example.xml](https://github.com/LDTchara/KernelExtensions/blob/main/XMLExamples/Trial_Example.xml)

## VM 攻击配置（VMAttackConfig）

- 存放位置：`VMATK/<名称>.xml`
- 根元素：`<VMAttackConfig>`
- 包含恢复模式、系统日志、引导文本、虚假文件等设置。
- 详细说明：[VM攻击系统](./../systems/vm-attack.md)
- 示例XML：[MyAttack_Example.xml](https://github.com/LDTchara/KernelExtensions/blob/main/XMLExamples/MyAttack_Example.xml)

## 飞机 Daemon 配置

- 直接在目标计算机中添加即可。
- 可配置属性：`FallDuration`、`OnFailed`、`OnSaved`。
- 详细说明：[飞机Daemon系统](./../systems/aircraft.md)

## KE 通用配置（KE-Config.xml）

- 位置：扩展根目录的 `KE-Config.xml`（**不存在时 KE 会自动创建**一份带注释的模板）
- 根元素：`<KEConfig>`
- 这是 KE 自身的全局开关，与各系统配置分开；每次 OSLoad 重新读取（改完重启游戏生效）

| 配置项 | 默认 | 说明 |
|--------|------|------|
| `<Debug>` | `false` | 输出 KE 调试级日志（发布版请保持 `false`） |
| `<Watermark>` | `true` | 主菜单彩虹水印是否显示 |
| `<SkipVanillaIRCLogs>` | `false` | 跳过原版 `BashLogs.txt` IRC 日志（只加载 `CustomIRCLogs.txt`） |
| `<CustomImages>` | — | 自定义图标注册（`<Image>` 子元素，路径相对扩展根，自动注册为 `@文件名`），供 `SetNodeIcon` 使用——见[自定义节点图标系统](./../systems/node-icon.md) |
| `<BannedUsernames>` | — | 新账号创建时的用户名拦截（`<Ban Name="admin" Reason="保留用户名" />`；`ReasonBlock` 从 `<Reasons>` 块里随机取一条原因） |

## 通用规则

- 所有 `file` 属性中指定的路径均为**相对于扩展根目录**的路径。
- 颜色字段支持名称（`Red`）、十六进制（`#FF0000`）或我的名字。
- 音乐字段可使用纯文件名、相对路径或 DLC 路径，解析由 `MusicPathResolver` 自动完成。

---

## 另请参阅

- [首页](./../index.md) – 返回主索引
- [Configuration Files (English)](./../../en/components/configuration.md) – 英文版