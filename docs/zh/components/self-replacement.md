# 自替换符（`#…​#`）

Hacknet 内容里的一类文本占位符：写成 `#名称#`，**在内容加载时被替换成实际内容**。
本页汇总 **KE 相关的两类** —— 自定义程序、以及 KE 新增的节点自替换符。

> 原版自带的占位符（`#PLAYERNAME#` / `#BINARY#` / `#SSH_CRACK#` 等）属于游戏本体机制，
> 不在本页展开 —— 清单见原版的 `self-replacement-placeholder` 文档。

---

## 一、自定义程序的自替换符（KE 注册）

KE 向 Pathfinder 注册了 4 个可执行程序，每个绑定一个自替换符。**在文件内容里写它们**，
存档生成时会被替换为真正的程序数据：

| 程序 | 自替换符 | 所属功能 |
|------|----------|----------|
| `CustomTrial` | `#CUSTOMTRIAL#` | 自定义试炼系统 |
| `PhaseSwift` | `#PHASESWIFT#` | 相位穿梭系统 |
| `EffectsPlayer` | `#EFFECTS#` | 特效播放（动态壁纸 / 视频播放的底层能力） |
| `WPTEST` | `#WPTEST#` | 动态壁纸（测试程序） |

典型用法（把程序放到玩家节点的 `bin/` 下）：

```xml
<file path="bin" name="CustomTrial.exe">#CUSTOMTRIAL#</file>
```

!!! warning "运行前必须让文件真的存在"
    注册**只**让自替换符可解析，**不会把文件放进节点**。玩家要能运行，必须先在自己的
    `bin/` 里存在对应文件（如上声明即可）。

!!! note "写法限制"
    这类占位符必须**独占整个字符串**（值的前后不能再有别的字符）。
    例如 `prefix#CUSTOMTRIAL#suffix` 不会生效。

---

## 二、KE 新增：节点自替换符

原版**只有玩家侧**的占位符，没有「引用某个节点」的。KE 补上了两个：

| 自替换符 | 替换为 |
|----------|--------|
| `#IP_<节点id>#` | 该节点的 `ip` |
| `#NAME_<节点id>#` | 该节点的显示名（`name`，不是你写在 XML 里的 `idName`） |

```xml
<file path="bin" name="relay.cfg">#RELAY_IP#</file>
<!-- 换成 -->
<file path="bin" name="relay.cfg">#IP_relayNode#</file>
```

查找时**只匹配 `idName`**（即你在节点 XML 的 `<Computer id="...">` 里写的那个），且**大小写不敏感**
（所以 `#IP_RELAY#` 与 `#IP_relay#` 等价）。

!!! note "为什么不匹配 `ip` / `name`"
    这两个写法实际上没人会用（谁会写 `#IP_235.7.94.131#`？），而且节点的 `name` 本身可能含占位符
    （如 `#PLAYERNAME# 作战基地`），拿它当 key 会引入歧义。所以只认 `idName`。

!!! note "为什么是前缀式（`IP_` 在前）"
    原版已有 `#PLAYER_IP#` / `#RANDOM_IP#` / `#GIBSON_IP#` 三条以 `_IP` **结尾**的标识符。
    若把节点 id 放在前面（`#<id>_IP#`），形态就与原版重叠，安全性只剩下「原版先跑」这条隐式契约 ——
    未装 DLC 时 `#GIBSON_IP#` 不会被原版替换，反而会落进我们的匹配范围。
    前缀式在原版标识符空间里**结构上不存在冲突**（没有一条以 `IP_` / `NAME_` 开头）。

---

## 三、生效位置

自替换符走的是 `ComputerLoader.filter()`，覆盖面很广：

- **原版直接调用 `filter()`**：EOS 设备内容、HackerScript 脚本、内存转储、留言板、
  Start Actions（`SAAddAsset` / `SAAddIRCMessage` / `SAAppendToFile` / `SAStartScreenBleedEffect` 等）
- **Pathfinder 的 `.Filter()` 扩展**：电脑的 `ip` / `name` / `idName` 属性、文件 name/contents、username 等
  —— 所以**电脑属性本身也支持占位符**
- **KE 侧**：`TerminalWrite` / `TerminalType` / `StartScreenBleedEffectWCC` 等动作
- **自定义程序的文件内容**（见第一节）

---

## 四、注意事项

!!! warning "替换是「一次性」的"
    占位符在**内容加载时**被替换并固化。节点 IP 之后发生变化
    （如飞机坠机后被改成 `DCLOC:` 前缀）**不会**回溯更新已经替换好的文本。

- **节点不存在 → 保留原文**，KE 不报错也不记日志。看到 `#IP_xxx#` 原样出现，就说明 id 写错了
- **单遍替换、不递归**：替换只过一遍。若有节点的 **`name` 本身写了占位符**（如 A 的显示名就是 `#NAME_B#`），
  那 `#NAME_A#` 会替换出 `#NAME_B#` 这串字面文本，**到此为止**，不会继续展开成 B 的名字
  （与原版 `Replace` 链一致；也正因为如此，占位符互相引用不会导致无限展开）
- **引用「尚未加载的节点」会在加载完成后自动补上**：`filter()` 在节点加载时跑，那一刻 `netMap` 可能不完整
  （实测：`playerComp` 的文件引用了更晚加载的 `testNode6`）。KE 会在 **OSLoaded 时再扫一遍文件内容**，
  把这类未解析的占位符补上，所以**内容里可以自由引用别的节点，不必操心加载顺序**
- **电脑 `ip` / `name` 属性也过 `filter()`**：这些属性在加载时一次性确定，
  若其引用的是尚未加载的节点，同样按约定保留原文（**不补替换**，需要时再说）
- **`#IP_` / `#NAME_` 前缀视为 KE 的占位符命名空间**，请勿在内容里用这两个前缀表达别的含义

---

## 另请参阅

- [首页](./../index.md) – 返回主索引
- [Self-Replacement Placeholders (English)](./../../en/components/self-replacement.md) – 英文版
- [可执行程序](./executables.md) – 4 个自定义程序与它们的自替换符
- [相位穿梭系统](./../systems/phase-swift.md) · [自定义试炼系统](./../systems/custom-trial.md)
