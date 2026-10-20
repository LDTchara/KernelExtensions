# 🔧 KernelExtensions 文档（简体中文）

**KernelExtensions** 是一个基于 **Pathfinder API** 的 Hacknet 模组。它为扩展作者提供了一整套可复用的系统与组件：
相位穿梭、自定义试炼、PortHackV2 破解程序、VM 攻击、飞机守护进程、自定义定时器、节点图标、自定义结局、动态色引擎，
以及一系列可拼装的自定义 Action / Executable / Daemon。

!!! warning "部分内容由 AI 辅助生成"
    本项目的部分文档与代码由 **AI 辅助生成**，可能存在不准确或不完整之处 ——
    尤其是配置项说明与示例。请在使用前自行评估；发现问题欢迎通过
    [Issue](https://github.com/LDTchara/KernelExtensions/issues) 或 PR 指出，会及时修正。

> 本文档对应 **KernelExtensions 0.7**。

## 📖 系统导航

| 系统 | 页面 |
|------|------|
| 相位穿梭 | [相位穿梭系统（PhaseSwift）](./systems/phase-swift.md) |
| 自定义试炼 | [自定义试炼系统](./systems/custom-trial.md) |
| PortHackV2 | [PortHackV2 破解程序](./systems/port-hack-v2.md) |
| 虚拟机攻击 | [VM攻击系统](./systems/vm-attack.md) |
| 飞机 Daemon | [飞机Daemon系统](./systems/aircraft.md) |
| 定时器 | [自定义定时器系统（Clock）](./systems/clock.md) |
| 节点图标 | [自定义节点图标系统](./systems/node-icon.md) |
| 结局 | [自定义结局系统（StartEnding）](./systems/custom-ending.md) |
| 心脏 | [自定义心脏 Daemon（CustomHeartDaemon）](./systems/custom-heart.md) |
| 动态色 | [自定义动态色系统（CustomColor）](./systems/custom-color.md) |

## 🧩 组件种类索引

| 分类 | 页面 |
|------|------|
| Actions / 自定义Action | [自定义Action](./components/actions.md) |
| 可执行程序 | [可执行程序](./components/executables.md) |
| 守护进程 | [自定义Daemon](./components/daemons.md) |
| 配置文件 | [配置文件](./components/configuration.md) |
| 工具类 | [工具类](./components/utility.md) |
| 本地化 | [本地化（KELoc）](./components/keloc.md) |
| 自替换符 | [自替换符](./components/self-replacement.md) |
| 补丁与 Harmony | [Harmony补丁](./components/harmony.md) |
| 第三方兼容 | [与第三方模组兼容](./components/mod-compat.md) |

## 📚 其他

| 页面 | 说明 |
|------|------|
| [扩展作者指南](./guides/author-guide.md) | 扩展开发指引 |
| [安装与卸载](./guides/installation.md) | 安装说明 |
| [更新日志](./guides/changelog.md) | 版本历史 |

## ⚙️ 前置要求

- **Hacknet** + DLC **Labyrinths**
- **Pathfinder** 5.3.4+

> **注意**：本模组必须作为扩展的一部分运行，不支持全局插件模式。

## ❤️ 特别感谢

- **April_Crystal**：制作了飞机 Daemon 相关部分，提出了大量宝贵建议，对模组的完善贡献巨大。
- **ZQG**、**HN 扩展小屋的朋友们**：积极测试、反馈问题，为模组的稳定性提供了重要帮助。

---

📥 源码与问题反馈：[GitHub 仓库](https://github.com/LDTchara/KernelExtensions)　·　[English](./../en/index.md)
