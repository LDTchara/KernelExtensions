using System.Collections;
using System.Reflection;
using Hacknet;
using Pathfinder.Util;
using KernelExtensions.Utilities;

namespace KernelExtensions.Actions.Misc
{
    /// <summary>
    /// 列出**所有已注册的端口协议**（模组注册的 + 原版内置的），供扩展作者查名字与端口号。
    ///
    /// <para>
    /// 为什么需要它：端口协议名是"跨模组契约" —— 别家扩展注册了什么协议、显示名是什么、
    /// 默认端口号多少，作者写配置时（如端口破解器的协议名、端口前置条件）必须去猜字符串。
    /// 而 Pathfinder 的端口注册表**没有公开的枚举接口**，只有按名/按号查询：
    /// <c>IsPortRegistered</c> / <c>GetPortRecordFromProtocol</c> / <c>GetPortRecordFromNumber</c>。
    /// 本 Action 用反射补上枚举这一环。
    /// </para>
    ///
    /// <para>
    /// 两个注册表（都已实机核对过字段与可见性）：
    /// <list type="bullet">
    ///   <item><c>Pathfinder.Port.PortManager.CustomPorts</c>（private static
    ///     <c>AssemblyAssociatedList&lt;PortRecord&gt;</c>，模组注册的）——
    ///     字段私有，但其 <c>AllItems</c> 属性是 public，反射取到后即可枚举；</item>
    ///   <item><c>Pathfinder.Port.ComputerExtensions.OGPorts</c>（internal static
    ///     <c>Dictionary&lt;string, PortRecord&gt;</c>，原版内置的）—— 直接读字典。</item>
    /// </list>
    /// </para>
    ///
    /// <para>
    /// 用法：<c>&lt;ListPorts /&gt;</c>，可选 <c>Filter</c> 做**协议名或显示名的子串**筛选。
    /// 输出到玩家终端；设 <c>ToLog="true"</c> 可同时写 KELog（便于在日志里留档）。
    /// </para>
    /// </summary>
    public class ListPortsAction : KEAction
    {
        /// <summary>可选筛选：协议名或显示名**包含**该子串（大小写不敏感）。</summary>
        [XMLStorage] public string Filter;

        /// <summary>同时写入 KELog.Info（默认 false：只写玩家终端，不刷日志）。</summary>
        [XMLStorage] public bool ToLog;

        /// <summary>终端一次最多列出多少条（0 = 不限）。默认 40，避免几百条刷屏。</summary>
        [XMLStorage] public int Max = 40;

        /// <summary>内部取到的端口条目（反射读出的字段值）。</summary>
        private struct PortInfo
        {
            public string Protocol;
            public string DisplayName;
            public int DefaultPort;
            public bool IsCustom;
        }

        public override void Trigger(OS os)
        {
            try
            {
                List<PortInfo> ports = CollectPorts();

                if (ports.Count == 0)
                {
                    // 一个都没有通常是反射失效（例如 Pathfinder 版本变了字段名），而不是真没有端口
                    os.write("ListPorts: no ports found. If Pathfinder is loaded, this is a reflection "
                           + "failure - please report it.");
                    KELog.Error("[ListPorts] enumerated 0 ports; reflection into Pathfinder's port "
                              + "registry may have broken.");
                    return;
                }

                ports.Sort(ComparePorts);

                List<PortInfo> shown = ApplyFilter(ports);
                int customCount = 0;
                foreach (PortInfo p in ports) if (p.IsCustom) customCount++;

                os.write($"Registered ports: {ports.Count} ({customCount} from mods, "
                       + $"{ports.Count - customCount} built-in)");

                int limit = Max > 0 ? Math.Min(Max, shown.Count) : shown.Count;
                for (int i = 0; i < limit; i++)
                    os.write(FormatRow(shown[i]));

                if (shown.Count > limit)
                    os.write($"... and {shown.Count - limit} more (raise Max= to see them all).");
                else if (shown.Count == 0)
                    os.write($"No port matches filter '{Filter}'.");

                if (ToLog)
                    KELog.Info($"[ListPorts] {ports.Count} registered port(s): "
                             + string.Join(", ", ports.ConvertAll(p => p.Protocol).ToArray()));
            }
            catch (Exception ex)
            {
                // 反射失败不该让剧情动作整条中断
                os.write("ListPorts: failed - " + ex.Message);
                KELog.Error($"[ListPorts] failed: {ex}");
            }
        }

        /// <summary>按显示名排序，再按协议名；保证 `Filter` 之外的输出顺序稳定可读。</summary>
        private static int ComparePorts(PortInfo a, PortInfo b)
        {
            int byName = string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase);
            if (byName != 0) return byName;
            return string.Compare(a.Protocol, b.Protocol, StringComparison.OrdinalIgnoreCase);
        }

        private List<PortInfo> ApplyFilter(List<PortInfo> all)
        {
            if (string.IsNullOrEmpty(Filter)) return all;

            var kept = new List<PortInfo>();
            foreach (PortInfo p in all)
            {
                bool hit = (p.Protocol != null
                            && p.Protocol.IndexOf(Filter, StringComparison.OrdinalIgnoreCase) >= 0)
                        || (p.DisplayName != null
                            && p.DisplayName.IndexOf(Filter, StringComparison.OrdinalIgnoreCase) >= 0);
                if (hit) kept.Add(p);
            }
            return kept;
        }

        /// <summary>
        /// 一行一条：<c>[22] ssh - SSH</c>。
        /// 没有默认端口号的协议（注册时传 -1）显式标注，提醒作者 AddPort 时必须自己给端口号。
        /// </summary>
        private static string FormatRow(PortInfo p)
        {
            string port = p.DefaultPort > 0 ? p.DefaultPort.ToString() : "no default port";
            string kind = p.IsCustom ? "mod" : "vanilla";
            return $"[{port}] {p.Protocol} - {p.DisplayName} ({kind})";
        }

        /// <summary>
        /// 把两个注册表合成一个列表。
        ///
        /// <para>
        /// ⚠️ **两个注册表各自独立 try**：原版那份（<c>OGPorts</c>）来自
        /// <c>ComputerExtensions</c> 的静态构造，而那里会调 <c>PortExploits.populate()</c>。
        /// 只要静态构造失败，访问该类型就会抛 <c>TypeInitializationException</c>，
        /// 且**失败会被 CLR 记住**（后续访问持续抛）。实测在非游戏宿主里它确实会抛。
        /// 若把两者放在同一个 try 里，原版那份一抛就**连模组端口都列不出来** ——
        /// 而模组端口恰恰是作者最需要查的部分。所以这里逐个隔离。
        /// </para>
        /// </summary>
        private static List<PortInfo> CollectPorts()
        {
            var list = new List<PortInfo>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (PortInfo p in SafeRead(ReadCustomPorts, "mod-registered"))
                if (!string.IsNullOrEmpty(p.Protocol) && seen.Add(p.Protocol))
                    list.Add(p);

            foreach (PortInfo p in SafeRead(ReadVanillaPorts, "built-in"))
                if (!string.IsNullOrEmpty(p.Protocol) && seen.Add(p.Protocol))
                    list.Add(p);

            return list;
        }

        /// <summary>
        /// 执行一个注册表读取动作，失败时记 Debug 并返回空列表（不影响另一个注册表）。
        ///
        /// <para>
        /// 用 <see cref="KELog.Debug"/> 而不是 Warn/Error：原版注册表读取失败是
        /// **可预期的环境差异**（静态构造依赖原版引擎），不该在正常游戏里刷警告；
        /// 真正"一个端口都没列出来"的情况由 <see cref="Trigger"/> 统一报 Error。
        /// </para>
        /// </summary>
        private static List<PortInfo> SafeRead(Func<List<PortInfo>> read, string what)
        {
            try
            {
                return read();
            }
            catch (Exception ex)
            {
                KELog.Debug($"[ListPorts] could not read the {what} port registry: {ex.Message}");
                return new List<PortInfo>();
            }
        }

        /// <summary>模组注册的端口：Pathfinder.Port.PortManager.CustomPorts → AllItems。</summary>
        private static List<PortInfo> ReadCustomPorts()
        {
            var result = new List<PortInfo>();
            Type pmType = FindType("Pathfinder.Port.PortManager");
            if (pmType == null) return result;

            FieldInfo field = pmType.GetField("CustomPorts",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null) return result;

            object registry = field.GetValue(null);
            if (registry == null) return result;

            PropertyInfo allItems = registry.GetType().GetProperty("AllItems",
                BindingFlags.Instance | BindingFlags.Public);
            if (allItems == null) return result;

            if (allItems.GetValue(registry, null) is IEnumerable items)
                foreach (object record in items)
                    result.Add(ReadRecord(record, isCustom: true));

            return result;
        }

        /// <summary>原版内置端口：Pathfinder.Port.ComputerExtensions.OGPorts（字典）。</summary>
        private static List<PortInfo> ReadVanillaPorts()
        {
            var result = new List<PortInfo>();
            Type ceType = FindType("Pathfinder.Port.ComputerExtensions");
            if (ceType == null) return result;

            FieldInfo field = ceType.GetField("OGPorts",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null) return result;

            if (!(field.GetValue(null) is IDictionary dict)) return result;

            foreach (DictionaryEntry entry in dict)
                result.Add(ReadRecord(entry.Value, isCustom: false));

            return result;
        }

        /// <summary>从 PortRecord 上按属性名读三个值；属性缺失时留空而不是抛异常。</summary>
        private static PortInfo ReadRecord(object record, bool isCustom)
        {
            var info = new PortInfo { IsCustom = isCustom };
            if (record == null) return info;

            Type t = record.GetType();
            info.Protocol = ReadString(t, record, "Protocol");
            // 显示名在不同版本里叫过 DefaultDisplayName / DisplayName，两个都试
            info.DisplayName = ReadString(t, record, "DefaultDisplayName")
                            ?? ReadString(t, record, "DisplayName");
            info.DefaultPort = ReadInt(t, record, "DefaultPortNumber");
            return info;
        }

        private static string ReadString(Type t, object o, string prop)
        {
            PropertyInfo p = t.GetProperty(prop, BindingFlags.Instance | BindingFlags.Public);
            if (p != null && p.PropertyType == typeof(string))
                return p.GetValue(o, null) as string;
            FieldInfo f = t.GetField(prop, BindingFlags.Instance | BindingFlags.Public);
            return f != null && f.FieldType == typeof(string) ? f.GetValue(o) as string : null;
        }

        private static int ReadInt(Type t, object o, string prop)
        {
            PropertyInfo p = t.GetProperty(prop, BindingFlags.Instance | BindingFlags.Public);
            if (p != null && (p.PropertyType == typeof(int) || p.PropertyType == typeof(short)))
                return Convert.ToInt32(p.GetValue(o, null));
            FieldInfo f = t.GetField(prop, BindingFlags.Instance | BindingFlags.Public);
            return f != null && (f.FieldType == typeof(int) || f.FieldType == typeof(short))
                ? Convert.ToInt32(f.GetValue(o))
                : -1;
        }

        /// <summary>
        /// 按类型名找类型：先查已加载的程序集，找不到再主动加载 PathfinderAPI
        /// （Pathfinder 必然是已加载的宿主，这里主要是兜住"尚未被引用过"的边界）。
        /// </summary>
        private static Type FindType(string fullName)
        {
            Type t = Type.GetType(fullName);
            if (t != null) return t;

            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    if (asm.GetName().Name == "PathfinderAPI") return asm.GetType(fullName);
                }
                catch { }
            }
            return null;
        }
    }
}
