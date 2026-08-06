# KernelExtensions

> **KernelExtensions** 是一个使用 Pathfinder API 的 Hacknet 模组，旨在为扩展作者提供可配置的试炼、虚拟机攻击、飞机守护进程以及一系列自定义动作。  
> **KernelExtensions** is a Hacknet mod using the Pathfinder API, providing configurable trials, VM attacks, an aircraft daemon, and a collection of custom actions for extension authors.

---

## 📖 完整文档 / Full Documentation

所有详细文档、配置指南和动作列表已迁移至 **GitHub Wiki**。  
All detailed documentation, configuration guides, and action references have moved to the **GitHub Wiki**.

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

- **自定义试炼 (Custom Trial)** – XML 驱动的多阶段挑战，支持特效、计时器、节点摧毁等。
- **虚拟机攻击 (VM Attack)** – 可配置的崩溃攻击，强制玩家与文件系统交互以解除锁定。
- **飞机守护进程 (Aircraft Daemon)** – 可配置坠落时长与修复/坠毁动作的飞机系统，带全局高度计覆盖层。
- **十余种自定义动作 (Custom Actions)** – 包括终端交互、音效播放、节点重命名等。

→ 完整介绍请参阅 [Wiki 主页](https://github.com/LDTchara/KernelExtensions/wiki)。

---

## 🌍 多语言支持 / Multi-Language Support

KernelExtensions 内置多语言系统（机制与 [ZeroDayToolKit](https://github.com/prodzpod/ZeroDayToolKit) 一致），玩家切换游戏语言后，模组界面与文本会自动跟随。

KernelExtensions ships a built-in localization system (same mechanism as [ZeroDayToolKit](https://github.com/prodzpod/ZeroDayToolKit)); the mod's UI follows the player's in-game language automatically.

### 使用方式 / How it works

1. 把本仓库 `Locales/` 文件夹（含 `en-us.xml`、`zh-cn.xml`）复制到**扩展根目录**的 `Locales/` 文件夹；也可以放在插件目录（`Plugins/`）下的 `Locales/` 中。
   Copy the `Locales/` folder from this repo (containing `en-us.xml`, `zh-cn.xml`) into the **extension root's** `Locales/` folder, or into a `Locales/` folder next to the plugin DLL (`Plugins/Locales/`).
2. 词条使用 Hacknet 原生语言文件格式：根元素为语言代码，`<L key="KEY">值</L>` 定义词条，可选 `exact="true"` 表示仅整串匹配。
   Terms use Hacknet's native locale format: the root element is the locale code, `<L key="KEY">value</L>` defines a term; optional `exact="true"` means full-string match only.
3. 优先级：当前语言 > `en-us` > `default`；未收录的词条自动回退到内置英文表。
   Precedence: active locale > `en-us` > `default`; missing terms fall back to the built-in English table.

### 扩展作者可在文本中使用 `{{KEY}}` 语法 / Extension authors can use the `{{KEY}}` syntax

以下位置均支持 `{{KEY}}` 引用语言词条：
`{{KEY}}` is supported in all of the following places:

- 试炼配置（`Trial/*.xml`）：`<Title>`、`<Subtitle>`、`<DescriptionText>`（含文本文件内容）、`<OutroText>`、`<ResetText>`
  Trial configs: `<Title>`, `<Subtitle>`, `<DescriptionText>` (including text file contents), `<OutroText>`, `<ResetText>`
- VM 攻击配置（`VMATK/*.xml`）：`<GuideText>` 每一行、`<ButtonText>`、`<ErrorMessage>`
  VM attack configs: each `<GuideText>` line, `<ButtonText>`, `<ErrorMessage>`
- 所有经 `OS.write` 输出的终端消息（自定义 Action 的文本也可直接写 `{{KEY}}`）
  All terminal messages written via `OS.write` (custom action text may use `{{KEY}}` too)
- 带 `Language="dynamic"` 属性的 Pathfinder XML 文件（如动作文件/守护进程 XML，属性会被替换为当前语言）
  Pathfinder XML files carrying a `Language="dynamic"` attribute (e.g. action files / daemon XML; the attribute is replaced with the active locale)

示例见 `XMLExamples/Locales/` 与 `XMLExamples/ExampleTrial.xml`。
See `XMLExamples/Locales/` and `XMLExamples/ExampleTrial.xml` for examples.

---

## ❤️ 致谢 / Thanks

- **April_Crystal** – 飞机 Daemon 的核心实现与大量改进建议。  
- **HN 扩展小屋的各位朋友** – 测试、反馈与支持。

---

## 📥 下载与反馈 / Download & Feedback

- [Releases 页面](https://github.com/LDTchara/KernelExtensions/releases)  
- 如有问题或建议，欢迎到 [Issues](https://github.com/LDTchara/KernelExtensions/issues) 或 [Discussions](https://github.com/LDTchara/KernelExtensions/discussions) 提出。