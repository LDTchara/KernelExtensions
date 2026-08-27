# KernelExtensions

> **KernelExtensions** 是一个面向 Hacknet 扩展作者的"开箱即用"组件库：把原版风格的完整系统（自定义试炼、场景切换、飞机、心脏结局、定时器、本地化、动态颜色……）封装为 **XML 可配置**的组件，扩展作者无需编写 C# 即可搭建复杂剧情。基于 Pathfinder API 深度集成（Harmony 补丁、存档钩子、多语言）。  
> **KernelExtensions** is a drop-in component library for Hacknet extension authors: complete vanilla-styled systems (custom trials, scene switching, aircraft, heart finale, timers, localisation, dynamic colors, ...) packaged as **XML-configurable** components, so you can build rich storylines without writing C#. Deeply integrated with the Pathfinder API (Harmony patches, save hooks, multi-language).

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

- **自定义试炼 (Custom Trial)** – XML 驱动的多阶段挑战，支持特效、计时器、节点摧毁，以及**跨存档恢复被摧毁的节点**。  
  XML-driven multi-stage challenges with effects, timers, node destruction, and **cross-save restoration of destroyed nodes**.
- **PhaseSwift 场景切换 (PhaseSwift Scene Switching)** – 多场景瞬间切换，带音乐相位、动态背景与读档恢复。  
  Instant multi-scene switching with music phases, dynamic backgrounds, and save-restore support.
- **虚拟机攻击 (VM Attack)** – 可配置恢复方式的崩溃攻击（文件删除/文件校验等），强制玩家与文件系统交互以解除锁定。  
  Configurable crash attacks with multiple recovery modes (file deletion / file verification), forcing players to interact with the file system to recover.
- **飞机守护进程 (Aircraft Daemon)** – 可配置坠落时长与修复/坠毁动作的飞机系统，带全局高度计覆盖层。  
  An aircraft system with configurable fall duration and repair/crash actions, plus a global altimeter overlay.
- **心脏守护进程 (PorthackHeart Daemon)** – 原版碎心结局复刻，动画节奏全参数可配，剧情由 Action 无缝衔接。  
  A vanilla-faithful heartbreak finale with fully configurable animation timing, handed off to your story via Actions.
- **定时器 (Clock)** – 按固定间隔**循环执行 Action 序列**的叙事时钟：可配触发次数/时长上限，耗尽时执行 OnComplete，支持多实例与存档持久化。  
  A story clock that **repeatedly executes Action sequences at a fixed interval** — with trigger-count/runtime limits, an exhaustion `OnComplete`, multi-instance support, and save persistence.
- **节点图标系统 (Node Icon System)** – 自定义图片或内置预设的节点图标，可恢复原始图标并随存档保存。  
  Custom node icons from images or built-in presets, with restore-to-original and save persistence.
- **动态颜色系统 (CustomColor)** – 在主题与配置的颜色字段中使用彩虹、渐变与预设关键字。  
  Rainbow, gradient, and preset keywords for color fields in themes and configs.
- **多语言本地化 (Localisation)** – 内置 10 种语言的 KE-Locales 系统，支持外部文件覆盖与缺失条目自动补齐。  
  A 10-language KE-Locales system with external-file overrides and automatic missing-key completion.
- **用户名管理 (Username Management)** – 通过 KE-Config.xml 的 `BannedUsernames` 段阻止禁用用户名创建账号，禁用原因可配（直接/随机块）。  
  Block account creation with banned usernames via the `BannedUsernames` section of KE-Config.xml, with configurable (direct/random-pool) rejection reasons.
- **自定义 Action 与可执行程序 (Custom Actions & Executables)** – 终端交互、音效、标题横幅、自定义结局等 30 余种能力。  
  30+ capabilities including terminal interaction, sound effects, title banners, and custom endings.

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
