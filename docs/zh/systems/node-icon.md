# 自定义节点图标系统

**自定义节点图标系统** 允许扩展作者为任意计算机指定图标：既可在节点 XML 中**静态声明**（0 Action），也可用 `SetNodeIcon` Action 在运行时动态切换。

支持自定义图像文件（随扩展加载）、全部原版预设图标，以及 `#RESET#` 恢复原始图标；图标状态随存档持久化。

!!! info "适用版本"
    本页对应 KernelExtensions **0.7**。相关 Action：`SetNodeIcon`。

---

## 概览

- **静态声明**：节点 XML 的 `icon` 属性，无需任何 Action（如 `<Computer id="testNode1" icon="@C" />`）
- **运行时切换**：`<SetNodeIcon NodeID="dhs" Icon="@MyIcon" />`
- 自定义纹理在 `KE-Config.xml` 的 `<CustomImages>` 中注册
- 图标状态按节点保存，读档自动恢复

---

## 零 Action 静态声明

节点 XML 的 `icon` 属性本身就是声明位置——**不需要任何 Action**，节点加载时自动应用：

```xml
<Computer id="testNode1" name="Test Node1" type="empty" icon="@C">
    ...
</Computer>
```

- `icon` 支持全部图标值：`@自定义纹理`（如 `@C`）、原版预设名（`laptop` 等）、`SEC_LEVEL_x`
- 机制：`NodeIconComputerExecutor`（节点加载钩子，自动注册）读取 `icon` 属性并写入存储（OrgIcon / CurrIcon），渲染管线自动应用；节点**没有**显式 `icon` 时，按 `security` 等级映射默认图标
- 与 `SetNodeIcon` 共存：静态声明写 `icon` 属性（0 Action），剧情运行中动态切换用 Action

---

## 运行时切换

```xml
<SetNodeIcon NodeID="dhs" Icon="@Mine" />
```

执行后节点 `dhs` 立即显示自定义图标 `Mine.png`，修改写入存档，重进游戏图标不变。

---

## 原理

Hacknet 的 `DisplayModule` 用私有字典 `compAltIcons`（`Dictionary<string, Texture2D>`）解析节点图标：节点没有图标或字典查不到时回退到按安全等级（0~5）的默认图标，否则使用字典中的纹理。

KernelExtensions 在此之上新增三层图标：

| 层级 | 图标值 | 实现 |
|------|--------|------|
| **原版预设** | `laptop`、`chip`、`tablet`、`ePhone`、`kellis` 等 | 原版 `compAltIcons` 字典 |
| **自定义纹理** | `@图标名` | Harmony Postfix 拦截 `DisplayModule.GetComputerImage()`，纹理从专用字典读取 |
| **安全等级回退** | `SEC_LEVEL_0` ~ `SEC_LEVEL_6` | 节点无显式图标时内部生成，也用于存档恢复 |

---

## 自定义图片

### 1. 准备图片文件

把图片放进扩展目录，例如：

```
ExtensionRoot/
├── Images/
│   ├── Mine.png
│   └── Server.png
└── ...
```

!!! warning "尺寸与格式"
    - 图标**按原始像素尺寸绘制（不缩放）**，请尽量贴近原版预设图标的尺寸。连接头区域可用宽度约 **160 px**——超过约 160 px 会与相邻的 "Connected to" 文字重叠（该文字从 `x + 160` 开始绘制）。
    - **推荐 PNG**（完整 alpha 透明支持）。其余由 SDL_image 运行时解码的格式（JPG/BMP/GIF/TGA/TIFF/WebP/PCX/PNM）也可用，但 JPG 等无透明通道的格式可能出现黑底。
    - 扩展名**不参与解码**（SDL_image 按内容识别），只用于生成 `@名称`。避免同名不同扩展名的文件（`Mine.png` 与 `Mine.jpg` 都会注册为 `@Mine`，后者覆盖前者）。

### 2. 注册图标（`KE-Config.xml`）

`KE-Config.xml` 不存在时 KE 会自动创建。取消 `<CustomImages>` 的注释并填入路径：

```xml
<KEConfig>
    <CustomImages>
        <Image>Images/Mine.png</Image>
        <Image>Images/Server.png</Image>
    </CustomImages>
</KEConfig>
```

**注册规则**：对每个文件，KE 去掉扩展名并加上 `@` 前缀。

| 文件 | 注册为 |
|------|--------|
| `Images/Mine.png` | `@Mine` |
| `Images/Server.png` | `@Server` |

### 3. 使用

```xml
<!-- 自定义纹理 -->
<SetNodeIcon NodeID="dhs" Icon="@Mine" />

<!-- 原版预设 -->
<SetNodeIcon NodeID="admin" Icon="laptop" />

<!-- 恢复原始图标 -->
<SetNodeIcon NodeID="compB" Icon="#RESET#" />
```

---

## 支持的图标值

| 格式 | 示例 | 来源 |
|------|------|------|
| `@文件名` | `@Mine` | `KE-Config.xml` 的 `<CustomImages>` 注册的自定义纹理 |
| 原版预设名 | `laptop`、`chip`、`tablet`、`ePhone`、`kellis` | 原版 `compAltIcons` 字典 |
| 固定安全等级图标 | `SEC_LEVEL_0` ~ `SEC_LEVEL_6` | 固定使用该等级对应纹理，不随节点实际 `security` 变化 |
| `#RESET#` | `#RESET#` | 恢复到节点首次创建时记录的原始图标 |

---

## 数据持久化

图标状态作为 `<NodeIcon>` 子元素存入每个计算机的存档 XML：

```xml
<Computer id="dhs">
  <NodeIcon org="SEC_LEVEL_2" curr="@Mine" />
  ...
</Computer>
```

- `org` = 节点首次遇到时记录的原始图标
- `curr` = 玩家或 `SetNodeIcon` 设置的当前图标
- 读档时所有计算机图标自动恢复；`#RESET#` 始终回到 `org` 的值

---

## 已知限制

- 加载自定义纹理需要图形设备（GraphicsDevice）已启动：主菜单阶段不可用，OS 初始化完成后才加载

---

## 相关 Action

### `SetNodeIcon`

```xml
<SetNodeIcon NodeID="dhs" Icon="@Mine" />
<SetNodeIcon NodeID="dhs" Icon="laptop" />
<SetNodeIcon NodeID="dhs" Icon="#RESET#" />
```

| 属性 | 必填 | 说明 |
|------|:----:|------|
| `NodeID` | ✅ | 目标计算机 ID（别名 `TargetComp`，两者等价） |
| `Icon` | ✅ | 图标值，取值见[支持的图标值](#支持的图标值) |
| `Delay` | ❌ | 延迟执行的秒数；为 0 或省略时立即执行 |
| `DelayHost` | ❌ | 提供延迟服务的主机 ID（该主机需有 `FastActionHost` 守护进程） |

---

## 另请参阅

- [首页](./../index.md) – 返回主索引
- [Custom Node Icon System (English)](./../../en/systems/node-icon.md) – 英文版
- [自定义 Action](./../components/actions.md) – 全部自定义 Action 列表
- [杂项](./../guides/misc.md) – 其他辅助功能
