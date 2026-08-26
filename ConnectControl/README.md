# ConnectControl Action 使用指南

## 概述

`ConnectControl` 是 KE 注册的 Pathfinder Action（`ActionManager.RegisterAction<ConnectionControlAction>("ConnectControl")`，源码见 `ConnectControl/ConnectionControlAction.cs`），用于**控制一台电脑与另一台电脑之间的网络链接**。核心概念：

- **org 基线（组织链接）**：每台电脑有一个持久化的"组织链接"集合（内存字典 `Computer_OrgLinkdComps`，存进存档 `<OrgLinks>` 标签）；
- **临时连接**：`add`/`remove` 只改当前运行时 `links`，**不碰 org 基线**；
- **reset**：把 `links` 整体恢复成 org 基线（丢弃所有临时改动）。

## XML 语法

```xml
<ConnectControl sourceComp="playerComp" targetComp="jmail" mode="reset"/>
```

### 参数

- **`sourceComp`**（必填）：源电脑的 `idName`（或 IP），作为被操作的电脑；
- **`targetComp`**：目标电脑的 `idName`（或 IP）。**仅 `add`/`remove` 需要**，`reset` 忽略；
- **`mode`**（必填）：`reset` / `add` / `remove`。

## 三种模式

### 1. `mode="add"` —— 添加临时链接

```xml
<ConnectControl sourceComp="playerComp" targetComp="jmail" mode="add"/>
```

- 在 `sourceComp.links` 中加入 `targetComp` 的节点索引；
- 已存在则不重复添加；
- **不写入 org 字典**——它是临时连接，保存/读档后不保证保留（`<links>` 标签会存，但 org 基线不包含它）；
- 任一台电脑找不到 → 抛异常（`sourceComp or targetComp unknown.`）。

### 2. `mode="remove"` —— 移除临时链接

```xml
<ConnectControl sourceComp="playerComp" targetComp="jmail" mode="remove"/>
```

- 从 `sourceComp.links` 中移除 `targetComp` 的索引（不存在则无操作）；
- **不写入 org 字典**。

### 3. `mode="reset"` —— 恢复组织基线

```xml
<ConnectControl sourceComp="playerComp" mode="reset"/>
```

- 把 `sourceComp.links` 整体替换为它的 **org 基线**（`Computer_OrgLinkdComps[sourceComp]` 转成索引），**丢弃所有临时 add/remove 的改动**；
- 查字典时先按对象匹配，读档后若对象不一致则按 `idName` 兜底查找；
- `sourceComp` 找不到 → Error 日志并跳过；
- 该电脑没有 org 基线记录 → Warn 日志（`no OrgLinks recorded for ...`）并跳过。

## org 基线的生命周期（理解 reset 的前提）

| 时机 | 行为 |
|---|---|
| **新游戏启动** | `SaveOrgLinkedComps`（OSLoaded）把每台电脑**当前 `links` 快照**存进字典 = org 基线（内容 XML 里定义的连接，包括 `<link>` 和 `<OrgLinks>`） |
| **内容 XML 的 `<OrgLinks>`** | 作为电脑子元素定义初始组织链接：`<OrgLinks>compA,compB</OrgLinks>`，加载时并入 `links`，从而进入新游戏基线 |
| **读档** | `LoadOrgLinkedComps` 读存档 `<OrgLinks>` 标签（过滤 `ALLSAVED` 标记）→ 推迟到 OSLoaded 统一解析成对象（避免加载顺序丢链接）→ 基线 = 存档内容 |
| **保存** | `SaveOrgsTofile` 把字典写回存档 `<OrgLinks>` 标签（附 `ALLSAVED` 标记） |

所以：**`add`/`remove` 是"临时"的，`reset` 回到的是"持久化的 org 基线"**——想让某条链接成为基线的一部分，用内容 XML 的 `<OrgLinks>` 定义它，而不是运行时 `add`。

## 触发方式

作为 Pathfinder Action，可用于：

- **条件动作/任务脚本**（`RunnableConditionalActions` 加载的 action 列表，如 `<ConnectControl .../>` 所在文件）；
- **Mission XML** 中的 action 节点（通过 `ActionManager` 按名解析）。

## 注意事项

1. **`reset` 不需要 `targetComp`**，写了也会被忽略；
2. **`add`/`remove` 不会改变 org 基线**——如果目标是"永久建立/断开组织链接"，请直接改内容 XML 的 `<OrgLinks>`；
3. **`sourceComp`/`targetComp` 建议用 `idName`**（`Programs.getComputer` 也支持 IP，但 `idName` 在存档/内容间更稳定）；
4. **读档后重置的兼容性**：字典按 `idName` 兜底查找，读档后能正确命中；
5. **错误行为**：未知电脑在 `reset` 时打日志并跳过，在 `add`/`remove` 时抛异常（Pathfinder 会记录错误日志，游戏不崩溃）；
6. 存档 `<OrgLinks>` 里的 `ALLSAVED` 是内部标记，**不要手动改**，读取时会被自动过滤。

## 完整示例（三个模式一起）

```xml
<!-- 临时连接 jmail 到 playerComp -->
<ConnectControl sourceComp="playerComp" targetComp="jmail" mode="add"/>

<!-- 断开 jmail 与 playerComp 的临时连接 -->
<ConnectControl sourceComp="playerComp" targetComp="jmail" mode="remove"/>

<!-- 把 playerComp 恢复为组织基线（丢弃所有临时改动） -->
<ConnectControl sourceComp="playerComp" mode="reset"/>
```
