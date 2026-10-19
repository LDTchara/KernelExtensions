# 安装与卸载

*LDTchara：这个界面有必要存在吗？但不管怎么说多少有点用还是留着吧*

## 前提条件

- 已安装 **Hacknet** 及 DLC **Labyrinths**。
- 已安装 **Pathfinder** 框架 5.3.4 或更高版本。
- 确认你的扩展文件夹（例如 `Extensions/你的扩展名/`）已经存在且包含了基本的 `extension.xml`。

## 安装 KernelExtensions

1. 从 [GitHub Releases](https://github.com/LDTchara/KernelExtensions/releases) 下载最新版的 `KernelExtensions.dll`。
2. 将 `KernelExtensions.dll` 放入你的扩展文件夹中的 `Plugins` 子文件夹：  
   `Extensions/你的扩展名/Plugins/KernelExtensions.dll`
3. （可选，但推荐）在扩展根目录下创建以下文件夹用于存放配置文件：  
   - `Trial/`：存放试炼配置 XML  
   - `VMATK/`：存放虚拟机攻击配置 XML  
   - `PhaseSwift/`：存放相位穿梭配置 XML  
   - `Actions/`：存放动作文件  
   - `KE-Config.xml`：KE 自身的全局开关，**不存在时自动创建**，无需手动准备（见[配置文件](./../components/configuration.md)）
4. 启动 Hacknet 并加载你的扩展。如果控制台出现绿色的 `[KernelExtensions] All is well ** SUCCESS!!` 即表示安装成功。

## 卸载 KernelExtensions

- 如果 KernelExtensions 是以**扩展插件**的形式运行的（正确方式），卸载该扩展时，KernelExtensions 会被自动移除。所有 Harmony 补丁会被自动撤销，水印也会消失。
- 如果你需要完全手动移除，直接删除 `Plugins/KernelExtensions.dll` 文件，然后重启游戏即可。
- 模组**不支持全局插件模式**（即不能放在 `BepInEx/plugins/` 下），请确保它始终位于扩展的 `Plugins` 目录内。
  放进全局插件目录时，启动会检测到"非扩展模式"并**拒绝加载**（BepInEx 日志会给出明确错误，不会半初始化）。

## 许可证

模组以 **MIT-0（MIT No Attribution）**发布 —— 即去掉了署名条款的 MIT：使用、修改、再分发均**无需注明出处**。

许可证全文见仓库根的 [`LICENSE`](https://github.com/LDTchara/KernelExtensions/blob/dev/LICENSE)，第三方组件声明见 [`NOTICE.md`](https://github.com/LDTchara/KernelExtensions/blob/dev/NOTICE.md)。

> 许可证**不**内嵌进 `KernelExtensions.dll` —— MIT-0 不要求二进制携带声明，所以没有往里面塞任何东西。
> 想标明出处的话非常欢迎，但那是情分，不是义务。

## 常见问题

**问：控制台没有出现成功消息？**  
答：请检查 Pathfinder 和 BepInEx 是否安装正确，以及 `KernelExtensions.dll` 是否放在了正确的位置。

**问：进入游戏后主菜单没有水印？**  
答：请确认 KernelExtensions 是在扩展中加载的。如果未加载任何扩展，水印不会出现。  
另外检查 `KE-Config.xml` 的 `<Watermark>` 是否被设成了 `false`（该开关默认 `true`）。

**问：怎么确认加载了正确版本？**  
答：主菜单水印会显示 `+ KernelExtensions x.x.x`，版本号与 DLL 版本一致。

---

## 另请参阅

- [首页](./../index.md) – 返回主索引
- [Installation (English)](./../../en/guides/installation.md) – 英文版