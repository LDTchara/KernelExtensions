# 本地化（KELoc）

KernelExtensions 内置的**文本本地化**：扩展根目录下的 `KE-Locales.xml` 语言表，覆盖原版的 10 种语言。

!!! info "谁会用这个"
    - **扩展作者** —— 想让 KE 的界面文案换成你的措辞，或为某个语言补词条。
      改 `KE-Locales.xml` 即可，**不需要写任何代码**。这是本页的主要读者。
    - **模组作者** —— 你自己的插件要做本地化，**不必依赖 KE**（用原版机制或自建语言表就行）。
      本页末尾的 API 只是给"已经在用 KE、想顺便复用它的语言表"的人准备的。

---

## 一、语言文件

`KE-Locales.xml` **内嵌在 KE 的 dll 里**，并会**导出到你扩展的根目录**：

- **首次运行** —— 扩展根目录没有该文件时，自动导出内嵌副本
- **之后** —— **优先读你扩展里的那份**，可以直接改
- **缺失 key 自动补齐** —— KE 更新后内嵌新增了词条，启动时会**物理补进你的文件**
  （不覆盖已有值；整个语言节点缺失则从内嵌复制），并记一条 Info 日志
- **删掉外部文件** —— 回退到内嵌副本，下次启动重新导出

!!! tip "所以不需要手动同步"
    升级 KE 之后不必删文件或手工合并——新的词条会在下次启动时自动补进去。

!!! warning "路径与大小写"
    文件位置是**扩展根目录**（与 `KE-Config.xml` 同级）。词条 `Key` **大小写敏感**；
    语言 `Name` 大小写不敏感。

---

## 二、词条格式

```xml
<Language Name="zh-cn">
    <Term Key="FAKE_RECOVERY_HELP" Value="帮助文档" />
    <Term Key="FAKE_RECOVERY_TERMINAL" Value="终端" />
    <Term Key="USERNAME_DEFAULT_REASON" Value="该名称不可用" />
</Language>
```

- `Language@Name` —— 语言标识（见下表；**大小写不敏感**）
- `Term@Key` —— 词条键（**大小写敏感**）
- `Term@Value` —— 文案

---

## 三、支持的语言

跟随游戏的 `Settings.ActiveLocale`，即原版 10 种：

`en-us` · `zh-cn` · `ja-jp` · `ko-kr` · `ru-ru` · `de-de` · `fr-fr` · `es-es` · `tr-tr` · `nl-nl`

---

## 四、回退链

取词条时按顺序尝试，第一个命中即返回：

1. **当前语言**（精确，如 `zh-cn`）
2. **当前语言的前缀**（如 `zh-cn` → `zh`）
3. **`en-us`**（非英语环境的兜底）
4. **代码里写死的默认文案**（内置 fallback）

所以**只补一个语言**是安全的：没补的语言会自动落到 `en-us` 或内置默认值，不会显示成空白。

---

## 五、KE 自己的词条键约定

KE 自身的 key 用**全大写下划线**、按系统分组。**扩展作者覆盖时只需照抄这些 key**，
不需要理解命名规则：

| 前缀 | 用途 |
|------|------|
| `FAKE_RECOVERY_*` | VM 攻击恢复界面（按钮、密码提示等） |
| `PORT_CRACKER_*` | 端口破解器 |
| `USERNAME_*` | 用户名管理（禁用原因等） |
| `FLIGHT_ALTITUDE_*` | 飞机高度计 |

> 想找某个界面文案对应的 key：搜索扩展根目录下 `KE-Locales.xml` 里的中文/英文文案即可。

---

## 六、在模组里复用（可选）

如果你正在写插件、且**已经在依赖 KE**，可以直接取它的词条。
命名空间 `KernelExtensions.Utilities`：

```csharp
// 取当前语言的词条；整条回退链都失败时返回第二个参数
string text = KELoc.Loc("FAKE_RECOVERY_HELP", "HELP");

// 带占位符格式化（占位符不匹配时原样返回，不抛异常）
string msg = KELoc.Format("SOME_KEY", "value={0}", value);

// 重新加载语言表；一般不必手动调用（首次 Loc 时会自动加载）
KELoc.Load();
```

!!! note "不建议为了本地化而引入 KE"
    上面这套的价值在于"复用 KE 已有语言表"。独立模组更该用原版机制或自建语言文件 ——
    多一个硬依赖不值得。

---

## 七、另请参阅

- [首页](./../index.md) – 返回主索引
- [Localization (KELoc)](./../../en/components/keloc.md) – 英文版
- [配置文件](./configuration.md) – `KE-Config.xml` 全部字段
- [工具类](./utility.md) – 其它代码级 API
