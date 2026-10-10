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
    /// 两种来源共用同一套解析路径 —— 动作**文件**与配置里的**内联**动作点，
    /// 都走 Pathfinder 的 <see cref="EventExecutor"/>，因此支持的写法完全一致：
    /// <list type="bullet">
    ///   <item><c>&lt;Actions&gt;</c> —— 其中的动作**立即**逐个执行；</item>
    ///   <item><c>&lt;ConditionalActions&gt;</c> —— 交给 <c>os.ConditionalActions</c>，
    ///     由条件决定何时执行（<c>&lt;Instantly&gt;</c> 在加载后立即触发，
    ///     <c>&lt;OnConnect&gt;</c> / <c>&lt;HasFlags&gt;</c> 等则等待条件满足）；</item>
    ///   <item>其它元素 —— 按 Pathfinder 的 <c>SerializableAction</c> 直接执行。</item>
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
                            SerializableAction action = ActionsLoader.ReadAction(child);
                            if (action == null) continue;
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
                        SerializableAction action = ActionsLoader.ReadAction(node);
                        if (action == null) continue;
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
