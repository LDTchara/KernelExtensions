# 配置文件

KernelExtensions 的配置分**两层**：

- **扩展级配置** —— `KE-Config.xml`，KE 自身的全局开关。**本页详述**。
- **各系统配置** —— 每个系统有自己的配置文件或配置段，**详见对应系统页**；本页只给索引。

所有路径均相对于扩展根目录。

---

## 一、扩展级配置（KE-Config.xml）

- 位置：扩展根目录的 `KE-Config.xml`（**不存在时 KE 会自动创建**一份带注释的模板）
- 根元素：`<KEConfig>`
- 这是 KE 自身的全局开关，与各系统配置分开；每次 OSLoad 重新读取（改完重启游戏生效）

| 配置项 | 默认 | 说明 |
|--------|------|------|
| `<Debug>` | `false` | 输出 KE 调试级日志（发布版请保持 `false`） |
| `<Watermark>` | `true` | 主菜单水印是否显示（外观见下） |
| `<SkipVanillaIRCLogs>` | `false` | 跳过原版 `BashLogs.txt` IRC 日志（只加载 `CustomIRCLogs.txt`） |
| `<CustomImages>` | — | 自定义图标注册（`<Image>` 子元素，路径相对扩展根，自动注册为 `@文件名`），供 `SetNodeIcon` 使用——见[自定义节点图标系统](./../systems/node-icon.md) |
| `<BannedUsernames>` | — | 新账号创建时的用户名拦截（`<Ban Name="admin" Reason="保留用户名" />`；`ReasonBlock` 从 `<Reasons>` 块里随机取一条原因） |

### 主菜单水印长什么样

开启时（默认），Hacknet 主菜单标题右侧会出现 **`+ KernelExtensions <版本号>`**：

- 位置在 ZeroDayToolKit 水印**右侧、同一行**，与其他模组水印不重叠
- 文字颜色**随时间平滑流动**（彩虹色），并带逐字轻微的上下晃动
- 前缀 `+` 是社区模组水印的通用风格
- 版本号自动跟随模组版本，无需手动维护
- 扩展卸载时水印自动消失

把 `<Watermark>` 设为 `false` 即可关闭。

---

## 二、各系统配置索引

各系统的配置格式与**全部字段**都在它们各自的系统页里维护；下表只回答「配置放在哪」。

| 系统 | 配置位置 | 根元素 | 详情 |
|------|----------|--------|------|
| 自定义试炼 | `Trial/<名称>.xml` | `<TrialConfig>` | [自定义试炼系统](./../systems/custom-trial.md) |
| VM 攻击 | `VMATK/<名称>.xml` | `<VMAttackConfig>` | [VM攻击系统](./../systems/vm-attack.md) |
| 相位穿梭 | 见系统页 | — | [相位穿梭系统](./../systems/phase-swift.md) |
| 自定义结局 | 见系统页 | — | [自定义结局系统](./../systems/custom-ending.md) |
| 飞机 Daemon | 直接写进目标计算机的 XML | — | [飞机Daemon系统](./../systems/aircraft.md) |
| 自定义动态色预设 | `CustomColor/<预设名>.xml` | `<ColorPreset>` | [自定义动态色系统](./../systems/custom-color.md) |
| 节点图标 | `KE-Config.xml` 的 `<CustomImages>` | — | [自定义节点图标系统](./../systems/node-icon.md) |
| 定时器 / 横幅 / 全屏警告 | 由 Action 参数直接指定，无独立配置文件 | — | [自定义 Action](./actions.md) |

> 示例 XML 统一放在仓库的
> [`XMLExamples/`](https://github.com/LDTchara/KernelExtensions/tree/main/XMLExamples) 目录。

!!! note "为什么本页不再罗列各系统的字段"
    各系统的配置字段（名称、默认值、取值格式）**只在它们的系统页维护**。
    本页过去复制过其中一部分，结果是代码更新后本页悄悄过期——现在改为索引，
    让「配置的权威说明」只有一处。

---

## 三、通用规则

- `file` 类属性中的路径一律**相对扩展根目录**。
- 音乐字段可用纯文件名、相对路径或 DLC 路径，由 `MusicPathResolver` 自动解析。
- 颜色字段的取值格式见[自定义动态色系统](./../systems/custom-color.md)
  （其中说明了不同字段所用解析链的差异）。

---

## 另请参阅

- [首页](./../index.md) – 返回主索引
- [Configuration Files (English)](./../../en/components/configuration.md) – 英文版
- [自定义动态色系统](./../systems/custom-color.md) – 颜色取值规则
- [自定义 Action](./actions.md) – 动作参数
