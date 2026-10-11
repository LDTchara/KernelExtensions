using KernelExtensions.Configs;
using System.Reflection;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace KernelExtensions.Utilities
{
    /// <summary>
    /// 配置类反序列化的统一入口 + **未知写法诊断**。
    ///
    /// <para>
    /// <b>为什么要诊断</b>：<see cref="XmlSerializer"/> 对**不认识的元素与属性一律静默忽略**。
    /// 于是作者把甲系统的写法用到乙系统（例如在 Trial 里写元素文本内容、在定时器里写子元素），
    /// 结果是"配置没生效、但一个错都不报"。KE 现有 8 个系统的动作点写法各不相同，
    /// 这类静默失效是最容易踩的坑。
    /// </para>
    ///
    /// <para>
    /// <b>做法</b>：先用 <see cref="XmlSerializer"/> 正常反序列化，再拿配置类的**声明元数据**
    /// （<see cref="XmlElementAttribute"/> / <see cref="XmlArrayAttribute"/> /
    /// <see cref="XmlArrayItemAttribute"/> / <see cref="XmlAttributeAttribute"/>）逐层比对源文档，
    /// 把对不上的名字报到日志里。**只读，不改任何解析行为。**
    /// </para>
    ///
    /// <para>
    /// <b>为什么不改解析行为</b>：宽容解析是既有契约 —— 老配置多写了无关字段不该因此失效。
    /// 所以这里只补"可见性"，不做拒绝。
    /// </para>
    ///
    /// <para>
    /// <b>已知不做的地方</b>：Pathfinder 的 <c>[XMLStorage]</c> 类（Action / Daemon）走
    /// <c>XMLStorageAttribute.ReadFromElement</c>，它**按字段名精确查属性表**，
    /// 与 <see cref="XmlSerializer"/> 的元数据无关，无法用同一套反射比对覆盖；
    /// 那一路的诊断由 <c>ActionHelper</c> 在解析失败时点名到具体元素来完成。
    /// </para>
    /// </summary>
    public static class XmlConfigLoader
    {
        /// <summary>
        /// 反序列化一个配置对象，并按需做未知写法诊断。
        /// </summary>
        /// <typeparam name="T">配置类型（须带 <see cref="XmlRootAttribute"/> 或可推导的根名）。</typeparam>
        /// <param name="path">配置文件的**完整路径**（调用方已完成越界校验与存在性检查）。</param>
        /// <param name="label">日志定位用，如 <c>PortHackV2/ssh</c>。</param>
        /// <returns>反序列化结果；失败时抛异常（保持调用方原有的 try/catch 语义）。</returns>
        public static T Load<T>(string path, string label) where T : class
        {
            object cfg;
            using (var fs = new FileStream(path, FileMode.Open))
            {
                cfg = new XmlSerializer(typeof(T)).Deserialize(fs);
            }

            if (ConfigLoader.Debug && cfg != null)
            {
                try
                {
                    WarnAboutUnknownNames(path, typeof(T), label);
                }
                catch (Exception e)
                {
                    // 诊断本身绝不能影响加载
                    KELog.Debug($"[XmlConfig] '{label}': diagnostics skipped ({e.GetType().Name}).");
                }
            }

            return cfg as T;
        }

        /// <summary>逐层比对"源文档"与"类型声明的名字"，把对不上的报出来。</summary>
        private static void WarnAboutUnknownNames(string path, Type rootType, string label)
        {
            var unknown = CollectUnknownNames(path, rootType);
            if (unknown.Count == 0) return;

            // 去重后输出，避免同一处写法在多行重复刷屏
            var distinct = unknown.Distinct().Take(12).ToList();
            KELog.Warn($"[XmlConfig] '{label}': {unknown.Count} unrecognised name(s) — "
                + "these are SILENTLY IGNORED by the parser, so a typo here means the setting never applies: "
                + string.Join("; ", distinct)
                + (unknown.Count > distinct.Count ? $" (+{unknown.Count - distinct.Count} more)" : "")
                + ". Note the action-point syntax differs per system (see the docs).");
        }

        /// <summary>
        /// 收集"源文档里出现、但类型没声明"的元素名与属性名。
        /// 形如 <c>&lt;Protocol&gt;/ &lt;Time&gt;</c> 或 <c>&lt;Time&gt;@ typo=</c>。
        ///
        /// <para>
        /// <b>本方法不写任何日志</b>，与 <see cref="WarnAboutUnknownNames"/> 分开是为了让这套逻辑
        /// 能在游戏外被测试 —— <c>KELog</c> 走 BepInEx 的 logger，未初始化时输出无处可去，
        /// 那样就只能靠"没抛异常"来间接判断，证明不了真正发现了什么。
        /// </para>
        /// </summary>
        /// <param name="path">配置文件完整路径。</param>
        /// <param name="rootType">配置类型。</param>
        /// <returns>对不上的名字清单（可能为空）。</returns>
        public static List<string> CollectUnknownNames(string path, Type rootType)
        {
            var unknown = new List<string>();

            XDocument doc;
            using (var sr = new StreamReader(path))
            {
                doc = XDocument.Load(sr, LoadOptions.None);
            }

            XElement root = doc.Root;
            if (root != null) Walk(root, rootType, "", unknown);

            return unknown;
        }

        /// <summary>
        /// 递归比对：当前元素的名字已由**父层**判定过，这里只处理它的属性与子元素。
        /// <paramref name="path"/> 是**已走过的路径**（如 <c>Protocols/Protocol/</c>），
        /// 让报出来的位置能直接定位到配置里的哪一层。
        /// </summary>
        private static void Walk(XElement node, Type type, string path, List<string> unknown)
        {
            // 类型有 [XmlAnyElement] / [XmlAnyAttribute] 时，本层不校验（作者可以自由写）
            bool anyElement = HasAnyElement(type);

            // 属性：对**本元素**检查（叶子也要查 —— 早期版本只在会下钻的元素上查，
            // 导致 <Time typo="1"> 这种漏报）
            CheckAttributes(node, type, path, unknown);

            if (anyElement) return;

            var elementTypes = KnownElements(type);
            var wrappers = WrapperTypes(type);
            string childPath = path + node.Name.LocalName + "/";
            foreach (XElement child in node.Elements())
            {
                string n = child.Name.LocalName;
                bool isWrapper = wrappers.TryGetValue(n, out Type itemType);
                bool isElement = elementTypes.TryGetValue(n, out Type childType);

                if (!isWrapper && !isElement)
                {
                    unknown.Add($"{path}<{node.Name.LocalName}> 下的未知元素 <{n}>"
                        + "（写的名字与声明对不上 = 该配置项永远不会生效）");
                    continue;
                }

                // 包装元素（<Protocols> 之于 <Protocol>）：它自身没有类型，只是容器；
                // 它的每个子元素都是一个"项"，各自按**项类型**递归（递归时会查各自的属性）。
                if (isWrapper)
                {
                    CheckAttributes(child, type, childPath, unknown);   // 包装元素自己的属性由宿主声明约束
                    if (itemType == null || IsLeaf(itemType)) continue;
                    string itemPath = childPath + n + "/";
                    foreach (XElement item in child.Elements())
                        Walk(item, itemType, itemPath, unknown);
                    continue;
                }

                // 普通子元素：类型非空 → 交给它的 Walk（属性由那次调用查，类型是它自己）；
                // 类型为空（标量 / 不可下钻）→ 它不会再被 Walk，属性必须在这里查，否则漏报。
                if (childType == null)
                {
                    CheckAttributes(child, type, childPath, unknown);
                    continue;
                }

                Walk(child, childType, childPath, unknown);
            }
        }

        /// <summary>检查一个元素上的属性名是否都被声明过。</summary>
        private static void CheckAttributes(XElement node, Type ownerType, string path, List<string> unknown)
        {
            if (HasAnyAttribute(ownerType)) return;

            var known = KnownAttributes(ownerType);
            foreach (XAttribute a in node.Attributes())
            {
                if (a.IsNamespaceDeclaration) continue;
                string an = a.Name.LocalName;
                if (an == "xmlns" || an.StartsWith("xmlns:", StringComparison.Ordinal)) continue;
                if (!known.Contains(an))
                    unknown.Add($"{path}<{node.Name.LocalName}> 的未知属性 '{an}'"
                        + "（属性名对不上 = 该设置永远不会生效）");
            }
        }

        // ── 元数据缓存（反射只做一次；配置类数量有限）──
        private static readonly Dictionary<Type, Dictionary<string, Type>> ElementCache = new();
        private static readonly Dictionary<Type, HashSet<string>> AttributeCache = new();
        private static readonly Dictionary<Type, Dictionary<string, Type>> WrapperCache = new();

        /// <summary>
        /// 该类型上"包装元素"的名字 → 项类型。
        ///
        /// <para>
        /// 包装元素（<c>[XmlArray]</c> 的名字）**自身不是一个类型**，它只起一层壳的作用；
        /// 校验它的子元素时要用**项类型**。所以它从 <see cref="KnownElements"/> 里排除、
        /// 单独放这张表 —— 否则会拿错类型去查子元素（实测把合法的 <c>&lt;Protocol&gt;</c> 报成未知）。
        /// </para>
        /// </summary>
        private static Dictionary<string, Type> WrapperTypes(Type type)
        {
            if (WrapperCache.TryGetValue(type, out var cached)) return cached;

            var map = new Dictionary<string, Type>(StringComparer.Ordinal);
            foreach (var (name, itemType) in Declared(type, elements: true, attributes: false, wrappersOnly: true))
                map[name] = itemType;

            WrapperCache[type] = map;
            return map;
        }

        /// <summary>
        /// 该类型能接受的子元素名 → 对应类型（<c>null</c> 表示"认识这个名字，但无需再往下走"）。
        ///
        /// <para>
        /// 登记规则（两条曾经踩错、已修正）：
        /// <list type="bullet">
        ///   <item>数组形式 <c>&lt;Protocols&gt;&lt;Protocol&gt;…&lt;/Protocol&gt;&lt;/Protocols&gt;</c>
        ///     （<see cref="XmlArrayAttribute"/> + <see cref="XmlArrayItemAttribute"/>）：
        ///     **包装名不进本表**（它是个壳、没有类型），项名带项类型进本表。
        ///     只登记包装名而不给项名，会让整棵子树逃过检查（实测漏报过）；
        ///     把包装名映射成项类型，则会拿错类型去查它的子元素（实测误报过）。</item>
        ///   <item>标量一律映射成 <c>null</c>（叶子）：否则会拿 <see cref="string"/> 的成员
        ///     （<c>Length</c> / <c>Chars</c>）去比对，把合法的 <c>&lt;string&gt;</c> 报成未知。</item>
        /// </list>
        /// </para>
        /// </summary>
        private static Dictionary<string, Type> KnownElements(Type type)
        {
            if (ElementCache.TryGetValue(type, out var cached)) return cached;

            var map = new Dictionary<string, Type>(StringComparer.Ordinal);
            foreach (var (name, t) in Declared(type, elements: true, attributes: false))
                map[name] = t;

            ElementCache[type] = map;
            return map;
        }

        /// <summary>该类型能接受的属性名。</summary>
        private static HashSet<string> KnownAttributes(Type type)
        {
            if (AttributeCache.TryGetValue(type, out var cached)) return cached;

            var set = new HashSet<string>(StringComparer.Ordinal);
            foreach (var (name, _) in Declared(type, elements: false, attributes: true))
                set.Add(name);

            AttributeCache[type] = set;
            return set;
        }

        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.Instance;

        /// <summary>
        /// 从 <see cref="XmlSerializer"/> 的元数据特性里取出"声明的名字"。
        ///
        /// <para>
        /// 覆盖四类：<see cref="XmlElementAttribute"/>（含数组隐式展开）、
        /// <see cref="XmlArrayAttribute"/> + <see cref="XmlArrayItemAttribute"/>（显式包装元素）、
        /// <see cref="XmlAttributeAttribute"/>。
        /// 没写名字特性时按**字段名**兜底（<see cref="XmlSerializer"/> 的默认行为）。
        /// </para>
        /// </summary>
        /// <param name="type">要检查的配置类型。</param>
        /// <param name="elements">true = 只收**元素名**（排除带 <see cref="XmlAttributeAttribute"/> 的成员）。</param>
        /// <param name="attributes">true = 只收**属性名**。</param>
        /// <param name="wrappersOnly">
        /// true = 只收 <c>[XmlArray]</c> 的**包装元素**（名字 → 项类型），供
        /// <see cref="WrapperTypes"/> 用；false = 收全部元素名（包装元素已被排除）。
        /// </param>
        private static List<(string Name, Type Type)> Declared(
            Type type, bool elements, bool attributes, bool wrappersOnly = false)
        {
            var result = new List<(string, Type)>();

            foreach (FieldInfo f in type.GetFields(Flags))
            {
                var attr = f.GetCustomAttribute<XmlAttributeAttribute>();
                if (elements && attr != null) continue;
                if (attributes && attr == null) continue;
                Declare(result, wrappersOnly, f.Name, f.FieldType,
                    f.GetCustomAttribute<XmlElementAttribute>(),
                    f.GetCustomAttribute<XmlArrayAttribute>(),
                    f.GetCustomAttribute<XmlArrayItemAttribute>(),
                    attr,
                    f.GetCustomAttribute<XmlTextAttribute>() != null,
                    f.GetCustomAttribute<XmlIgnoreAttribute>() != null,
                    f.GetCustomAttribute<XmlAnyAttributeAttribute>() != null,
                    f.GetCustomAttribute<XmlAnyElementAttribute>() != null);
            }

            foreach (PropertyInfo p in type.GetProperties(Flags))
            {
                var attr = p.GetCustomAttribute<XmlAttributeAttribute>();
                if (elements && attr != null) continue;
                if (attributes && attr == null) continue;
                Declare(result, wrappersOnly, p.Name, p.PropertyType,
                    p.GetCustomAttribute<XmlElementAttribute>(),
                    p.GetCustomAttribute<XmlArrayAttribute>(),
                    p.GetCustomAttribute<XmlArrayItemAttribute>(),
                    attr,
                    p.GetCustomAttribute<XmlTextAttribute>() != null,
                    p.GetCustomAttribute<XmlIgnoreAttribute>() != null,
                    p.GetCustomAttribute<XmlAnyAttributeAttribute>() != null,
                    p.GetCustomAttribute<XmlAnyElementAttribute>() != null);
            }

            return result;
        }

        /// <summary>
        /// 单个成员 → 它贡献的"名字"。
        ///
        /// <para>
        /// <b>不变量</b>：<see cref="KnownElements"/> 里**永远不含包装元素**（<c>[XmlArray]</c> 的名字）；
        /// 包装元素只出现在 <see cref="WrapperTypes"/> 里，值 = 项类型。
        /// 两类名字因此不会混淆。
        /// </para>
        /// </summary>
        /// <param name="result">收集目标。</param>
        /// <param name="wrappersOnly">true = 只产出包装元素的映射（见上面的不变量）。</param>
        /// <param name="memberName">成员名（没写名字特性时作为兜底）。</param>
        /// <param name="memberType">成员类型。</param>
        /// <param name="el"><see cref="XmlElementAttribute"/>（可空）。</param>
        /// <param name="arr"><see cref="XmlArrayAttribute"/>（可空）。</param>
        /// <param name="arrItem"><see cref="XmlArrayItemAttribute"/>（可空）。</param>
        /// <param name="attr"><see cref="XmlAttributeAttribute"/>（可空）。</param>
        /// <param name="isText">是否 <see cref="XmlTextAttribute"/>（文本内容不参与比对）。</param>
        /// <param name="ignored">是否 <see cref="XmlIgnoreAttribute"/>。</param>
        /// <param name="anyAttr">是否 <see cref="XmlAnyAttributeAttribute"/>（作者可自由写属性）。</param>
        /// <param name="anyEl">是否 <see cref="XmlAnyElementAttribute"/>（作者可自由写元素）。</param>
        private static void Declare(
            List<(string, Type)> result, bool wrappersOnly,
            string memberName, Type memberType,
            XmlElementAttribute el, XmlArrayAttribute arr, XmlArrayItemAttribute arrItem,
            XmlAttributeAttribute attr, bool isText, bool ignored, bool anyAttr, bool anyEl)
        {
            if (ignored) return;

            // 属性：由 Declared 预筛过，走到这里的一定是属性成员
            if (attr != null)
            {
                if (wrappersOnly) return;
                result.Add((attr.AttributeName is { Length: > 0 } n ? n : memberName, null));
                return;
            }

            if (isText || anyAttr || anyEl) return;

            // 显式包装：<Items><Item>…</Item></Items>
            if (arr != null)
            {
                Type itemType = ElementTypeOf(memberType);
                string itemName = arrItem?.ElementName is { Length: > 0 } ien ? ien : "Item";
                Type visited = itemType == null || IsLeaf(itemType) ? null : itemType;

                if (wrappersOnly)
                {
                    // 包装名 → 项类型：让 Walk 知道"这一层要下钻，用项类型校验子元素"
                    result.Add((arr.ElementName is { Length: > 0 } an ? an : memberName, visited));
                }
                else
                {
                    // 项名 → 项类型：支持把项直接平铺在父层（XmlSerializer 也接受）
                    result.Add((itemName, visited));
                }
                return;
            }

            if (wrappersOnly) return;

            // 元素形式（数组会被 XmlSerializer 隐式展开：同一名字可重复出现）
            string name = el?.ElementName is { Length: > 0 } en ? en : memberName;
            Type t = el?.Type ?? memberType;
            Type inner = ElementTypeOf(t);
            Type target = inner ?? t;
            result.Add((name, IsLeaf(target) ? null : target));
        }

        /// <summary>
        /// 数组 / 单参数泛型集合 → 元素类型；否则 null（标量或不可展开）。
        /// </summary>
        private static Type ElementTypeOf(Type t)
        {
            if (t == null || t == typeof(string)) return null;
            if (t.IsArray) return t.GetElementType();
            if (t.IsGenericType)
            {
                var args = t.GetGenericArguments();
                if (args.Length == 1) return args[0];
            }
            return null;
        }

        /// <summary>
        /// 是否是"叶子"类型 —— 没有值得继续比对的子元素结构。
        ///
        /// <para>
        /// ⚠️ 必须挡住标量：否则 <c>&lt;RequireOpenPortsBefore&gt;&lt;string&gt;ssh&lt;/string&gt;&lt;/…&gt;</c>
        /// 会被拿去和 <see cref="string"/> 的成员（<c>Length</c> / <c>Chars</c>）比对，
        /// 把合法的 <c>&lt;string&gt;</c> 报成未知元素（实测误报过）。
        /// </para>
        /// </summary>
        private static bool IsLeaf(Type t)
            => t == null
            || t == typeof(string) || t == typeof(decimal)
            || t.IsPrimitive || t.IsEnum
            || t == typeof(object) || t == typeof(Guid)
            || t == typeof(DateTime) || t == typeof(TimeSpan)
            || (Nullable.GetUnderlyingType(t) is Type u && IsLeaf(u));

        private static bool HasAnyElement(Type type)
            => type.GetCustomAttribute<XmlAnyElementAttribute>() != null
            || type.GetFields(Flags).Any(f => f.GetCustomAttribute<XmlAnyElementAttribute>() != null)
            || type.GetProperties(Flags).Any(p => p.GetCustomAttribute<XmlAnyElementAttribute>() != null);

        private static bool HasAnyAttribute(Type type)
            => type.GetCustomAttribute<XmlAnyAttributeAttribute>() != null
            || type.GetFields(Flags).Any(f => f.GetCustomAttribute<XmlAnyAttributeAttribute>() != null)
            || type.GetProperties(Flags).Any(p => p.GetCustomAttribute<XmlAnyAttributeAttribute>() != null);
    }
}
