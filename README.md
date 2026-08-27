# KernelExtensions

> **KernelExtensions** 是一个使用 Pathfinder API 的 Hacknet 模组，旨在为扩展作者提供可配置的试炼、场景切换、虚拟机攻击、飞机守护进程、心脏守护进程以及一系列自定义动作，全部由 XML 驱动。  
> **KernelExtensions** is a Hacknet mod using the Pathfinder API, providing configurable trials, scene switching, VM attacks, aircraft daemons, a heart daemon, and a collection of XML-driven custom actions for extension authors.

**当前版本 / Current Version**: 0.7.0

---

## 📖 完整文档 / Full Documentation

所有详细文档、配置指南和动作列表已迁移至 **GitHub Wiki**（中英双语页面）。  
All detailed documentation, configuration guides, and action references have moved to the **GitHub Wiki** (bilingual Chinese/English pages).

👉 **[前往 Wiki / Go to Wiki](https://github.com/LDTchara/KernelExtensions/wiki)** 👈

---

## ⚙️ 快速安装 / Quick Install

1. 确保已安装 Hacknet、Labyrinths DLC 和 Pathfinder 5.3.4+。  
   Ensure Hacknet, the Labyrinths DLC, and Pathfinder 5.3.4+ are installed.
2. 将 `KernelExtensions.dll` 放入扩展的 `Plugins` 文件夹中（例如 `Extensions/你的扩展名/Plugins/`）。  
   Place `KernelExtensions.dll` into an extension's `Plugins` folder (e.g., `Extensions/YourExtension/Plugins/`).
3. 启动游戏并加载扩展，主菜单出现彩虹水印即表示安装成功。  
   Launch the game and load the extension; a rainbow watermark on the main menu indicates success.

> ⚠️ 本模组必须作为扩展的一部分运行，不支持全局插件模式。  
> ⚠️ This mod must run as part of an extension; global plugin mode is not supported.

---

## 🧩 主要功能概览 / Feature Overview

- **自定义试炼 (Custom Trial)** – XML 驱动的多阶段挑战，支持特效、计时器、节点摧毁与存档持久化。  
  XML-driven multi-stage challenges with effects, timers, node destruction, and save persistence.
- **PhaseSwift 场景切换 (PhaseSwift Scene Switching)** – 多场景瞬间切换，带音乐相位、动态背景与读档恢复。  
  Instant multi-scene switching with music phases, dynamic backgrounds, and save-restore support.
- **虚拟机攻击 (VM Attack)** – 可配置的崩溃攻击，强制玩家与文件系统交互以解除锁定。  
  Configurable crash attacks that force players to interact with the file system to recover.
- **飞机守护进程 (Aircraft Daemon)** – 可配置坠落时长与修复/坠毁动作的飞机系统，带全局高度计覆盖层。  
  An aircraft system with configurable fall duration and repair/crash actions, plus a global altimeter overlay.
- **心脏守护进程 (PorthackHeart Daemon)** – 原版碎心结局复刻，动画节奏全参数可配，剧情由 Action 无缝衔接。  
  A vanilla-faithful heartbreak finale with fully configurable animation timing, handed off to your story via Actions.
- **定时器 (Clock)** – 可配置的倒计时器，支持多实例与存档持久化。  
  Configurable countdown timers with multi-instance and save persistence.
- **节点图标系统 (Node Icon System)** – 自定义图片或内置预设的节点图标。  
  Custom node icons from images or built-in presets.
- **动态颜色系统 (CustomColor)** – 在主题与配置的颜色字段中使用彩虹、渐变与预设关键字。  
  Rainbow, gradient, and preset keywords for color fields in themes and configs.
- **多语言本地化 (Localisation)** – 内置 10 种语言的 KE-Locales 系统，缺失条目自动补齐。  
  A 10-language KE-Locales system with automatic missing-key completion.
- **自定义 Action 与可执行程序 (Custom Actions & Executables)** – 终端交互、音效、标题横幅、自定义结局等 20 余种能力。  
  20+ capabilities including terminal interaction, sound effects, title banners, and custom endings.

→ 完整介绍请参阅 [Wiki 主页](https://github.com/LDTchara/KernelExtensions/wiki)。  
→ See the [Wiki home](https://github.com/LDTchara/KernelExtensions/wiki) for full details.

---

## 🤝 贡献 / Contributing

我们欢迎任何形式的贡献：功能、修复、文档或反馈。  
Contributions of all kinds are welcome: features, fixes, docs, or feedback.

- 开发前请阅读 [贡献指南 / Contributing Guide](./CONTRIBUTING.md) 与 [行为准则 / Code of Conduct](./CODE_OF_CONDUCT.md)。  
  Please read the [Contributing Guide](./CONTRIBUTING.md) and [Code of Conduct](./CODE_OF_CONDUCT.md) first.

---

## ❤️ 致谢 / Thanks

- **April_Crystal** – 飞机 Daemon 和自定义节点图标的核心实现与大量改进建议（以及大量的麻烦），KE 早期开发者之一。  
  Core implementation of the Aircraft Daemon and node icons, plus lots of improvement suggestions (and lots of trouble). One of KE's earliest developers.
- **ZQG** – 第一个使用 KE 的扩展作者，提供了宝贵的测试反馈。  
  The first extension author to use KE, providing invaluable testing feedback.
- **HN 扩展小屋的各位朋友** – 测试、反馈与支持。  
  Friends in the HN extension community – testing, feedback, and support.

---

## 📥 下载与反馈 / Download & Feedback

- [Releases 页面](https://github.com/LDTchara/KernelExtensions/releases)  
- 如有问题或建议，欢迎到 [Issues](https://github.com/LDTchara/KernelExtensions/issues) 或 [Discussions](https://github.com/LDTchara/KernelExtensions/discussions) 提出。  
  For issues or suggestions, open an [Issue](https://github.com/LDTchara/KernelExtensions/issues) or start a [Discussion](https://github.com/LDTchara/KernelExtensions/discussions).
