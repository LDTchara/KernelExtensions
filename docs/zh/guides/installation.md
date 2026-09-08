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
   - `Actions/`：存放动作文件  
4. 启动 Hacknet 并加载你的扩展。如果控制台出现绿色的 `[KernelExtensions] All is well ** SUCCESS!!` 即表示安装成功。

## 卸载 KernelExtensions

- 如果 KernelExtensions 是以**扩展插件**的形式运行的（正确方式），卸载该扩展时，KernelExtensions 会被自动移除。所有 Harmony 补丁会被自动撤销，水印也会消失。
- 如果你需要完全手动移除，直接删除 `Plugins/KernelExtensions.dll` 文件，然后重启游戏即可。
- 模组**不支持全局插件模式**（即不能放在 `BepInEx/plugins/` 下），请确保它始终位于扩展的 `Plugins` 目录内。

## 常见问题

**问：控制台没有出现成功消息？**  
答：请检查 Pathfinder 和 BepInEx 是否安装正确，以及 `KernelExtensions.dll` 是否放在了正确的位置。

**问：进入游戏后主菜单没有水印？**  
答：请确认 KernelExtensions 是在扩展中加载的。如果未加载任何扩展，水印不会出现。

**问：怎么确认加载了正确版本？**  
答：主菜单水印会显示 `+ KernelExtensions x.x.x`，版本号与 DLL 版本一致。

---

## 另请参阅

- [首页](./../index.md) – 返回主索引
- [Installation (English)](./../../en/guides/installation.md) – 英文版