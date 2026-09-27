# 工具类

KernelExtensions 提供了一组静态工具类，供**模组作者**在代码中调用。

!!! info "本页写给谁看"
    这些是 **C# 代码级 API**——面向的是写插件/模组的人。
    **纯扩展作者通常用不到本页**：扩展侧的一切都通过 XML 完成（动作、配置、文件布局），
    不需要写代码。只有当扩展作者自己做了私有插件时，才会同时扮起模组作者的角色。

全部类位于命名空间 **`KernelExtensions.Utilities`**。

---

## 一、配置与语义

### ConfigValue

- 路径：`KernelExtensions.Utilities.ConfigValue`
- 作用：配置值语义工具，实现 **NONE 约定** —— 字符串配置项写 `NONE`（大小写不敏感，兼容原版 `none`）
  或留空 = **显式禁用**；不写该属性 = 使用默认值。

### ActionHelper

- 路径：`KernelExtensions.Utilities.ActionHelper`
- 方法：`ExecuteActionFile(OS os, string actionFilePath, string extensionRoot)`
- 作用：加载并执行一个动作文件，统一了 `CustomTrialExe` 和 VM 攻击系统的动作执行逻辑。

---

## 二、颜色

### ColorHelper

- 路径：`KernelExtensions.Utilities.ColorHelper`
- 作用：**共享颜色工具** —— 集中提供 HSV/RGB 转换与十六进制颜色解析，
  由 `CustomColorPatch`、`PhaseSwiftManager`、`CustomTrialExe` 等多处共同使用。

<!-- ke:9.50 -->
!!! note "关于颜色格式"
    各颜色字段目前**并非都支持十六进制与命名色**（详见
    [自定义动态色系统](./../systems/custom-color.md) 第四节）。把解析统一到本类的工作在计划内。

### FlowColorHelper

- 路径：`KernelExtensions.Utilities.FlowColorHelper`
- 方法：`GetFlowingRainbowColor(float position, float baseTime)`、`HslToRgbSimple(double h, double s, double l)`
- 作用：基于时间流动的彩虹色计算，用于主菜单水印及其他需要动态彩虹色的场景。

---

## 三、音频

### SoundHelper

- 路径：`KernelExtensions.Utilities.SoundHelper`
- 方法：`PlaySound(OS os, string soundPath, float volume, float pitch, float pan)`
- 作用：播放扩展目录内的 **WAV** 音效文件（路径相对扩展根，**必须含 `.wav` 扩展名**）。

### MusicPathResolver

- 路径：`KernelExtensions.Utilities.MusicPathResolver`
- 方法：`ResolveMusicPath(string musicPath, string extensionRoot)`
- 作用：把配置里的音乐字符串解析成 `MusicManager` 能加载的路径。纯文件名的查找顺序：
  ① 扩展根目录 → ② 扩展 `Music/` → ③ `Content/DLC/Music` → ④ 视为原版音乐名（`Content/Music`）。
- **含路径分隔符时**（如 `Music/Bit(Ending)`）：先检查扩展目录下是否真有该文件——
  有则按扩展内路径返回；**没有则原样交还原版**，所以带路径的写法也能指向原版曲。
- 返回值**保留调用方写的扩展名**（不主动剥离 `.ogg`）；省略 `.ogg` 时由 FNA 的 `SongReader.Normalize` 补全。

---

## 四、文本与本地化

### TextHelper

- 路径：`KernelExtensions.Utilities.TextHelper`
- 作用：**文本渲染辅助** —— 按字体实际字符集清洗文本。

  Hacknet 按语言加载字体（`LocaleActivator.ActivateLocale` →
  `LocaleFontLoader.LoadFontConfigForLocale` 读取 `Locales/{locale}/Fonts/{locale}_FontXX`），
  字体缺少某个字形时该字符会渲染成方块或空白。因此 KE 在输出文本前会做一次按字符集的过滤。

  > 实务约定：原版字体与常见替换字体都缺少 `→` 等 Unicode 符号的字形，**游戏内文本统一用 ASCII**
  > （写 `->` 而不是 `→`）——这与本类的清洗逻辑配合使用。

### KELoc

- 路径：`KernelExtensions.Utilities.KELoc`
- 作用：**KE 内置文本本地化**。语言文件 `KE-Locales.xml` 内嵌于 dll（EmbeddedResource），
  首次运行时导出到扩展根目录（**仅当文件不存在才导出**，之后不覆盖，用户可自由编辑）；
  外部文件存在时优先加载，删除后回退到内嵌副本。

---

## 五、渲染

### KECube3D

- 路径：`KernelExtensions.Utilities.KECube3D`
- 作用：原版 `Hacknet.Effects.Cube3D`（`internal`）的**反射桥**。
  自实现线框立方体与原版视觉存在细微差异，改为直接反射调用原版的 `RenderWireframe`
  （原版 `Cube3D.Initilize` 已在游戏启动时调用，静态缓冲就绪，反射调用即与原版渲染一致）。

---

## 六、日志

### KELog

- 路径：`KernelExtensions.Utilities.KELog`
- 作用：统一日志，**四级约定**：

  | 级别 | 用途 |
  |------|------|
  | `Debug` | KE 源码开发者排错（数据/机制级细节），默认关闭 |
  | `Info` | 扩展作者排错（动作级结果，一次触发一条） |
  | `Warn` | 可恢复 / 降级 / 需要注意 |
  | `Error` | 不该发生 / 功能失败 |

  > 面向玩家的终端反馈请用 `os.write`，而不是日志。

---

## 另请参阅

- [首页](./../index.md) – 返回主索引
- [Utility Classes (English)](./../../en/components/utility.md) – 英文版
- [自定义动态色系统](./../systems/custom-color.md) – 颜色取值规则（`ColorHelper` 的服务对象）
- [配置文件](./configuration.md) – NONE 约定在配置里的表现
