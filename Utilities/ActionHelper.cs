using Hacknet;
using Pathfinder.Replacements;
using Pathfinder.Util.XML;
using System.Text;
using System.Xml.Linq;
using System.Xml;

namespace KernelExtensions.Utilities
{
    /// <summary>
    /// 动作执行工具：把"一段动作 XML"跑起来。
    ///
    /// <para>
    /// **两种来源，两条执行路径**（行为等价，但实现不同，改动时两边都要看）：
    /// <list type="number">
    ///   <item><b>动作文件</b>（<see cref="ExecuteFile"/>）—— 交给 Pathfinder 的
    ///     <see cref="EventExecutor"/>：它按元素名流式解析，命中 <c>Actions</c> /
    ///     <c>ConditionalActions</c> 时回调我们注册的处理器。</item>
    ///   <item><b>内联动作</b>（<see cref="ExecuteInline"/>）—— 走
    ///     <see cref="EventExecutor"/> 会在构造/解析阶段抛异常（实机表现为"内联动作毫无反应、
    ///     连日志都没有"，异常冒到 <c>OS.Update</c> 被上层吞掉）。故改为把 XML 解析成
    ///     <see cref="ElementInfo"/> 树，直接调 Pathfinder **public** 的
    ///     <see cref="ActionsLoader.ReadAction"/> / <see cref="ActionsLoader.LoadActionSets"/> ——
    ///     与 <c>EventExecutor</c> 内部用的是同一套，行为一致但不碰解析器。</item>
    /// </list>
    /// </para>
    ///
    /// <para>
    /// 两条路支持的写法一致，因为最终都归结为同样的三种处理：
    /// <list type="bullet">
    ///   <item><c>&lt;Actions&gt;</c> —— 其中的动作**立即**逐个 <c>Trigger(os)</c>；</item>
    ///   <item><c>&lt;ConditionalActions&gt;</c> —— 加入 <c>os.ConditionalActions.Actions</c>，
    ///     由 OS 每帧 <c>Update</c> 逐个求条件，命中后**执行并从表中移除**（只触发一次）；</item>
    ///   <item>其它元素 —— 当作单个动作直接 <c>Trigger(os)</c>。</item>
    /// </list>
    /// </para>
    /// </summary>
    public static class ActionHelper
    {
        /// <summary>
        /// 执行一个动作文件（相对于扩展根目录）。
        /// </summary>
        /// <param name="os">当前 OS 实例</param>
        /// <param name="actionFilePath">相对于扩展根目录的路径</param>
        /// <param name="extensionRoot">扩展根目录</param>
        public static void ExecuteActionFile(OS os, string actionFilePath, string extensionRoot)
            => ExecuteFile(os, actionFilePath, null, extensionRoot);

        /// <summary>
        /// 执行一个动作文件（相对于扩展根目录），并带日志定位标签。
        /// </summary>
        /// <param name="os">当前 OS 实例</param>
        /// <param name="actionFilePath">相对于扩展根目录的路径</param>
        /// <param name="label">日志定位用（如 <c>MyConfig/ssh:&lt;OnStart&gt;</c>）；null 时用路径本身</param>
        /// <param name="extensionRoot">扩展根目录；null 时取当前扩展根</param>
        public static void ExecuteFile(OS os, string actionFilePath, string label, string extensionRoot = null)
        {
            if (os == null || ConfigValue.IsNone(actionFilePath)) return;

            string where = label ?? actionFilePath;

            string root = extensionRoot ?? ExtensionRoot();
            if (string.IsNullOrEmpty(root))
            {
                KELog.Warn($"[ActionHelper] '{where}': extension root unavailable; action skipped.");
                return;
            }

            // 越界（../、绝对路径、盘符）由 KEPath 拦下
            string fullPath = KEPath.ResolveInside(actionFilePath, root);
            if (fullPath == null)
            {
                KELog.Warn($"[ActionHelper] '{where}': path '{actionFilePath}' escapes the extension folder; skipped.");
                return;
            }
            fullPath = fullPath.Replace('\\', '/');

            if (!File.Exists(fullPath))
            {
                KELog.Warn($"[ActionHelper] '{where}': action file not found: {actionFilePath}");
                return;
            }

            // ⚠️ 就地兜底：EventExecutor 的构造也可能抛。异常若冒到动作点调用方
            // （通常在 OS.Update 里）会被上层吞掉、连日志都不留。
            try
            {
                Run(os, new EventExecutor(fullPath, true), where, fullPath);
            }
            catch (Exception e)
            {
                KELog.Error($"[ActionHelper] '{where}': action file failed: {e.Message}");
            }
        }

        /// <summary>
        /// 执行**内联**的动作 XML（即配置里直接写在钩子中的内容）。
        ///
        /// <para>
        /// ⚠️ **不经过 <see cref="EventExecutor"/>**：实测内联内容走 <c>EventExecutor</c> 会在
        /// 构造/解析阶段抛异常（游戏里表现为"内联动作毫无反应、连日志都没有" ——
        /// 异常冒到 <c>OS.Update</c> 被上层吞掉）。改为把 XML 直接解析成
        /// <see cref="ElementInfo"/> 树，再交给 Pathfinder 的
        /// <see cref="ActionsLoader.ReadAction"/> / <see cref="ActionsLoader.LoadActionSets"/> ——
        /// 这两个入口是 public 的，与 <c>EventExecutor</c> 内部用的是同一套，行为一致但不碰解析器。
        /// </para>
        /// </summary>
        /// <param name="os">当前 OS 实例</param>
        /// <param name="innerXml">钩子元素的**内部** XML（可含多个并列元素）</param>
        /// <param name="label">日志定位用</param>
        public static void ExecuteInline(OS os, string innerXml, string label)
        {
            if (os == null || string.IsNullOrWhiteSpace(innerXml)) return;

            try
            {
                // 包一层根元素：XmlReader/XElement 只允许单一根，而钩子里可能并列写多个容器
                var root = XElement.Parse("<" + InlineRoot + ">" + innerXml + "</" + InlineRoot + ">");
                List<ElementInfo> nodes = BuildElementList(root);

                int ran = 0;
                foreach (ElementInfo node in nodes)
                {
                    if (node.Name == "Actions")
                    {
                        // 列表内的动作**立即**逐个执行
                        foreach (ElementInfo child in node.Children)
                        {
                            if (!TryReadAction(child, label, out SerializableAction action)) continue;
                            action.Trigger(os);
                            ran++;
                        }
                    }
                    else if (node.Name == "ConditionalActions")
                    {
                        // 交给 os 的条件动作表；<Instantly> 等条件满足后由它驱动执行
                        RunnableConditionalActions sets = ActionsLoader.LoadActionSets(node);
                        os.ConditionalActions.Actions.AddRange(sets.Actions);
                        if (!os.ConditionalActions.IsUpdating)
                            os.ConditionalActions.Update(0f, os);
                    }
                    else
                    {
                        // 兜底：顶层直接写单个动作（不带 <Actions> 容器）也支持
                        if (!TryReadAction(node, label, out SerializableAction action)) continue;
                        action.Trigger(os);
                        ran++;
                    }
                }

                KELog.Info($"[ActionHelper] '{label}': executed inline ({ran} immediate action(s)).");
            }
            catch (Exception e)
            {
                KELog.Error($"[ActionHelper] '{label}': inline action failed: {e.Message}");
            }
        }

        /// <summary>
        /// 把一个元素读成动作；**失败时记一条可读日志并返回 false，而不是抛异常**。
        ///
        /// <para>
        /// 为什么需要这层：<see cref="ActionsLoader.ReadAction"/> 对未知元素名会
        /// <c>throw new KeyNotFoundException</c>。内联路径里一个名字写错，
        /// 整段内联就都会失败（只留一条笼统的 Error），作者无从判断是哪个元素的问题。
        /// 这里先判断名字是否可能合法，不认识就点名报出来。
        /// </para>
        ///
        /// <para>
        /// 判断顺序：内置动作名 → 交给 <see cref="ActionsLoader.ReadAction"/>
        /// （KE 与第三方注册的动作都在里面，Pathfinder 没提供枚举接口，无法预先判断）→ 都不中就警告。
        /// </para>
        /// </summary>
        /// <param name="node">要读取的元素。</param>
        /// <param name="label">日志定位用。</param>
        /// <param name="action">读出的动作；失败时为 null。</param>
        private static bool TryReadAction(ElementInfo node, string label, out SerializableAction action)
        {
            action = null;

            if (!IsBuiltInActionName(node.Name) && !IsLikelyRegisteredAction(node.Name))
            {
                KELog.Warn($"[ActionHelper] '{label}': unknown action or container <{node.Name}>; skipped."
                    + HintFor(node.Name));
                return false;
            }

            try
            {
                action = ActionsLoader.ReadAction(node);
                return action != null;
            }
            catch (KeyNotFoundException)
            {
                // Pathfinder 对"未注册的元素名"就是抛 KeyNotFoundException。
                // 走到这里说明名字长得合法（PascalCase）但并不存在 ——
                // 最典型的是**拼写错误**。此时把名字点出来，比让异常原文糊一整段更有用。
                KELog.Warn($"[ActionHelper] '{label}': no action or container named <{node.Name}>; skipped."
                    + HintFor(node.Name));
                return false;
            }
            catch (Exception e)
            {
                // 名字存在但装载失败（缺必需属性、值非法等）
                KELog.Warn($"[ActionHelper] '{label}': <{node.Name}> could not be loaded: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 原版与 Pathfinder 认识的<b>内置</b>动作元素名（共 26 个）。
        ///
        /// <para>
        /// 抄自 <c>Hacknet.SerializableAction.Deserialize</c> 的字典键与
        /// Pathfinder <c>ActionsLoader.ReadAction</c> 的 switch 分支 —— 这两处是**完全一致的 26 个**。
        /// 之所以要抄一份：Pathfinder 没提供"这个名字是不是合法动作"的公开查询，
        /// 只能自己拿内置名单做前置校验，才能对拼错的名字给出可读提示。
        /// </para>
        ///
        /// <para>
        /// ⚠️ 升级 Pathfinder 时若它新增内置动作，这份名单需要同步（漏了只是多一条 Warn，不会误伤）。
        /// </para>
        /// </summary>
        private static readonly HashSet<string> BuiltInActionNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "LoadMission", "RunFunction", "AddAsset", "AddMissionToHubServer",
            "RemoveMissionFromHubServer", "AddThreadToMissionBoard", "AddIRCMessage",
            "AddConditionalActions", "CopyAsset", "CrashComputer", "DeleteFile",
            "LaunchHackScript", "SwitchToTheme", "StartScreenBleedEffect",
            "CancelScreenBleedEffect", "AppendToFile", "KillExe", "ChangeAlertIcon",
            "HideNode", "GivePlayerUserAccount", "ChangeIP", "ChangeNetmapSortMethod",
            "SaveGame", "HideAllNodes", "ShowNode", "SetLock",
        };

        private static bool IsBuiltInActionName(string name) => BuiltInActionNames.Contains(name);

        /// <summary>
        /// 名字**看起来**像已注册的动作（KE / 第三方）。
        ///
        /// <para>
        /// Pathfinder 没给枚举接口，无法精确判断；但动作元素名都是 PascalCase 标识符，
        /// 所以用"首字母大写 + 只含字母数字下划线"作为放行条件，把它交给
        /// <see cref="ActionsLoader.ReadAction"/> 去定夺。这样既不会误伤第三方动作，
        /// 又能让小写 / 怪异名字得到明确提示。
        /// </para>
        /// </summary>
        private static bool IsLikelyRegisteredAction(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            if (!char.IsUpper(name[0])) return false;
            foreach (char ch in name)
            {
                if (!char.IsLetterOrDigit(ch) && ch != '_') return false;
            }
            return true;
        }

        /// <summary>对几个已知易错写法给出针对性提示。</summary>
        private static string HintFor(string name)
        {
            // <Action> 是 Faction 的容器（Hacknet.Factions.CustomFactionAction 的硬编码元素名），
            // 只在 <CustomFaction> 里被解析，不能用于通用动作点。
            if (string.Equals(name, "Action", StringComparison.Ordinal))
            {
                return " Did you mean <Actions>? <Action> (singular) is a Faction-only container"
                     + " and is not parsed outside <CustomFaction>.";
            }
            return string.Empty;
        }

        /// <summary>内联内容的临时根元素名（只用于让 XML 有唯一根）。</summary>
        private const string InlineRoot = "KEInlineAction";

        /// <summary>把一个 XML 元素的子元素转成 <see cref="ElementInfo"/> 列表。</summary>
        private static List<ElementInfo> BuildElementList(XElement parent)
        {
            var list = new List<ElementInfo>();
            foreach (XElement child in parent.Elements()) list.Add(ToElementInfo(child, null));
            return list;
        }

        /// <summary>
        /// <see cref="XElement"/> → <see cref="ElementInfo"/>。
        ///
        /// <para>
        /// <c>ElementInfo</c> 没有公开的 <c>FromXElement</c>，只有 <c>ConvertToXElement</c>（反向），
        /// 所以这里手工建：属性取 <c>XAttribute</c>、内容取 "有子元素则为空、否则为文本"，
        /// 与 Pathfinder 自己解析时保持一致。
        /// </para>
        /// </summary>
        private static ElementInfo ToElementInfo(XElement el, ElementInfo parent)
        {
            var info = new ElementInfo
            {
                Name = el.Name.LocalName,
                Parent = parent,
                Content = el.HasElements ? string.Empty : el.Value
            };
            foreach (XAttribute a in el.Attributes()) info.Attributes[a.Name.LocalName] = a.Value;
            foreach (XElement child in el.Elements()) info.Children.Add(ToElementInfo(child, info));
            return info;
        }

        /// <summary>
        /// 注册处理器并解析执行。文件与内联两条路都汇到这里，保证行为一致。
        /// </summary>
        /// <param name="os">当前 OS 实例</param>
        /// <param name="executor">已按来源构造好的解析器</param>
        /// <param name="label">日志定位用</param>
        /// <param name="fallbackSource">解析失败时回退用的 XML 文本（文件路径或内联文本）</param>
        private static void Run(OS os, EventExecutor executor, string label, string fallbackSource)
        {
            try
            {
                // 注册 ConditionalActions 处理器：条件满足后由 os 驱动执行
                executor.RegisterExecutor("ConditionalActions", (exec, info) =>
                {
                    var sets = ActionsLoader.LoadActionSets(info);
                    os.ConditionalActions.Actions.AddRange(sets.Actions);
                    if (!os.ConditionalActions.IsUpdating)
                        os.ConditionalActions.Update(0f, os);
                }, ParseOption.ParseInterior);

                // 注册 Actions 处理器（标准动作列表，立即执行）
                executor.RegisterExecutor("Actions", (exec, info) =>
                {
                    foreach (var child in info.Children)
                    {
                        var action = ActionsLoader.ReadAction(child);
                        action?.Trigger(os);
                    }
                }, ParseOption.ParseInterior);

                if (!executor.TryParse(out var ex))
                {
                    // 回退：我们自己注册的处理器没接住时，按 SerializableAction 逐个来
                    if (ex != null)
                        KELog.Warn($"[ActionHelper] '{label}': primary parse reported: {ex.Message}");
                    ParseFallback(os, fallbackSource, label);
                    return;
                }

                KELog.Info($"[ActionHelper] '{label}': executed.");
            }
            catch (Exception e)
            {
                KELog.Error($"[ActionHelper] '{label}': {e.Message}");
            }
        }

        /// <summary>
        /// 回退解析：把每个顶层元素当 <see cref="SerializableAction"/> 直接触发。
        /// <paramref name="xmlOrPath"/> 是文件路径时读文件，否则当作 XML 文本。
        /// </summary>
        private static void ParseFallback(OS os, string xmlOrPath, string label)
        {
            try
            {
                string text = xmlOrPath;
                if (File.Exists(text)) text = File.ReadAllText(text);

                using var sr = new StringReader(text);
                using XmlReader reader = XmlReader.Create(sr);
                reader.MoveToContent();
                while (reader.Read())
                {
                    if (reader.NodeType != XmlNodeType.Element) continue;
                    SerializableAction action = SerializableAction.Deserialize(reader);
                    action?.Trigger(os);
                }
            }
            catch (Exception e)
            {
                KELog.Error($"[ActionHelper] '{label}' fallback parse failed: {e.Message}");
            }
        }

        /// <summary>当前扩展根（已把反斜杠统一为正斜杠）；拿不到时返回 null。</summary>
        private static string ExtensionRoot()
        {
            try
            {
                return Hacknet.Extensions.ExtensionLoader.ActiveExtensionInfo?.FolderPath?.Replace('\\', '/');
            }
            catch
            {
                return null;
            }
        }
    }
}
