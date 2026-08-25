using Hacknet;
using KernelExtensions.Configs;
using KernelExtensions.Saving;
using KernelExtensions.Utilities;
using Pathfinder.Replacements;
using Pathfinder.Util;
using Pathfinder.Util.XML;

namespace KernelExtensions.Managers
{
    /// <summary>
    /// Clock 定时器管理器。
    ///
    /// 定位：Clock 是**剧情资产**——内部就是 Action 序列，只是循环形式，归根结底
    /// 服务于剧情——与 Mission/动作文件同类，分散组织：每个 Clock 一个独立 XML 文件，
    /// 由 ClockStart 按文件路径引用，ClockStop 按 ID 或路径停止。
    ///
    /// 系统级定时（挂 os.UpdateSubscriptions），不依赖 DelayHost 节点。
    /// Actions 启动时一次性预加载为 List&lt;SerializableAction&gt;，每次触发逐个
    /// Trigger（= 每次触发执行一遍无条件 instantly 集合），零重复解析零 IO。
    ///
    /// Clock 文件结构：
    ///   &lt;Clock ID="traceFlash" Interval="5.0" Times="3" Duration="60" OnComplete="Clocks/done.xml"&gt;
    ///       &lt;Actions&gt;
    ///           &lt;TerminalType Display="!WARNING!" RestoreDelay="0.2" /&gt;
    ///           &lt;FlashScreen Color="Red" Duration="2.0" /&gt;
    ///       &lt;/Actions&gt;
    ///   &lt;/Clock&gt;
    ///   ID — 必填（ClockStop/去重），省略回退为文件名不含扩展名
    ///   Interval — 触发间隔（秒），必须 &gt;0
    ///   Times — 循环次数上限（0/省略/负数 = 无限），耗尽自动停止
    ///   Duration — 运行总时长上限（秒），与 Times 谁先到谁停
    ///   OnComplete — 可选，耗尽后执行的 Action 文件（相对扩展根，对齐 CompleteAction）
    /// </summary>
    internal static class ClockManager
    {
        private static readonly Dictionary<OS, Dictionary<string, ClockInstance>> ActiveClocks = new();
        // 每个 OS 的 Update 委托缓存：必须保存引用，否则 -= 无法匹配 lambda 实例
        private static readonly Dictionary<OS, Action<float>> UpdateHandlers = new();

        /// <summary>读档暂存：由 ClockSaveExecutor 填充，OSLoaded 后重建（Clock 持久化）。</summary>
        public static List<ClockPersistentState> PendingRestore;

        /// <summary>启动一个 Clock（重复启动同一 ID = 刷新：替换为新定义，计时/计数重置）。</summary>
        public static void Start(OS os, string filepath, string extensionRoot)
        {
            if (string.IsNullOrWhiteSpace(filepath))
            {
                os.write("[ClockStart] Missing Filepath");
                return;
            }

            string fullPath = NormalizePath(Path.Combine(extensionRoot ?? "", filepath));
            if (!File.Exists(fullPath))
            {
                os.write($"Clock file not found: {filepath}");
                return;
            }

            var def = LoadDefinition(fullPath, extensionRoot);
            if (def == null)
            {
                os.write($"Clock parse failed: {filepath}");
                return;
            }

            if (!ActiveClocks.TryGetValue(os, out var clocks))
                ActiveClocks[os] = clocks = new Dictionary<string, ClockInstance>();

            clocks[def.Id] = new ClockInstance
            {
                Def = def,
                Timer = def.Interval,
                StartedAt = OS.currentElapsedTime
            };
            Subscribe(os);

            if (ConfigLoader.Debug)
                KELog.Info($"[Clock] started '{def.Id}' interval={def.Interval}s times={def.MaxTimes} duration={def.MaxDuration}s");
        }

        /// <summary>按 ID 停止（手动取消，不触发 OnComplete）。未知 ID 静默。</summary>
        public static void StopByID(OS os, string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return;
            if (!ActiveClocks.TryGetValue(os, out var clocks) || !clocks.Remove(id)) return;
            KELog.Info($"[Clock] stopped '{id}' by ID");
            if (clocks.Count == 0)
            {
                ActiveClocks.Remove(os);
                Unsubscribe(os);
            }
        }

        /// <summary>按路径停止（便利通道，与 ID 等价）。移除所有匹配路径的实例（正常唯一）。</summary>
        public static void StopByPath(OS os, string filepath, string extensionRoot)
        {
            if (string.IsNullOrWhiteSpace(filepath)) return;
            if (!ActiveClocks.TryGetValue(os, out var clocks)) return;

            string full = NormalizePath(Path.Combine(extensionRoot ?? "", filepath));
            var toRemove = new List<string>();
            foreach (var kv in clocks)
                if (string.Equals(kv.Value.Def.SourcePath, full, StringComparison.OrdinalIgnoreCase))
                    toRemove.Add(kv.Key);

            foreach (var id in toRemove) clocks.Remove(id);
            if (toRemove.Count > 0 && ConfigLoader.Debug)
                KELog.Info($"[Clock] stopped '{string.Join(",", toRemove)}' by path");
            if (clocks.Count == 0)
            {
                ActiveClocks.Remove(os);
                Unsubscribe(os);
            }
        }

        // ========== 持久化 ==========

        /// <summary>生成当前 OS 运行中 Clock 的持久化状态（仅运行中；耗尽/手动停止已移除，天然不存）。</summary>
        public static List<ClockPersistentState> GetPersistentState(OS os)
        {
            var list = new List<ClockPersistentState>();
            if (!ActiveClocks.TryGetValue(os, out var clocks)) return list;
            foreach (var inst in clocks.Values)
            {
                list.Add(new ClockPersistentState
                {
                    Id = inst.Def.Id,
                    SourcePath = inst.Def.SourcePath,
                    ExtensionRoot = inst.Def.ExtensionRoot,
                    TimesElapsed = inst.TimesElapsed,
                    Elapsed = (float)(OS.currentElapsedTime - inst.StartedAt),
                    Timer = inst.Timer
                });
            }
            return list;
        }

        /// <summary>
        /// 按持久化状态重建一个 Clock（OSLoaded 后调用，Clock 持久化）。
        /// SourcePath 重载 Clock 文件恢复定义（Actions 预加载），恢复计时/次数连续。
        /// 文件缺失或解析失败 → Warn 跳过不复活（剧情资产被删即不恢复）。
        /// </summary>
        public static void Restore(OS os, ClockPersistentState state)
        {
            if (state == null) return;

            var def = LoadDefinition(state.SourcePath, state.ExtensionRoot);
            if (def == null)
            {
                KELog.Warn($"[Clock] restore skipped '{state.Id}': reload failed (file missing or parse error)");
                return;
            }

            if (!ActiveClocks.TryGetValue(os, out var clocks))
                ActiveClocks[os] = clocks = new Dictionary<string, ClockInstance>();

            clocks[def.Id] = new ClockInstance
            {
                Def = def,
                // Timer 随档保存（免推导边界问题）；异常值回退为完整间隔
                Timer = state.Timer > 0f ? state.Timer : def.Interval,
                TimesElapsed = state.TimesElapsed > 0 ? state.TimesElapsed : 0,
                // StartedAt 反推：Duration 判定（now - StartedAt >= MaxDuration）连续
                StartedAt = OS.currentElapsedTime - (state.Elapsed > 0f ? state.Elapsed : 0f)
            };
            Subscribe(os);

            if (ConfigLoader.Debug)
                KELog.Info($"[Clock] restored '{def.Id}' (times={state.TimesElapsed}, elapsed={state.Elapsed:F1}s)");
        }

        // ========== 每帧驱动 ==========

        private static void OnUpdate(OS os, float dt)
        {
            if (!ActiveClocks.TryGetValue(os, out var clocks) || clocks.Count == 0) return;

            // 触发期间可能修改字典（OnComplete 里再 ClockStart/Stop），先快照 key
            var ids = new List<string>(clocks.Keys);
            foreach (var id in ids)
            {
                if (!clocks.TryGetValue(id, out var inst)) continue;

                inst.Timer -= dt;
                if (inst.Timer > 0f) continue;

                // 触发：预加载列表逐个 Trigger（无条件 instantly 集合，可重复）
                foreach (var a in inst.Def.Actions) a.Trigger(os);
                inst.TimesElapsed++;

                bool timesDone = inst.Def.MaxTimes > 0 && inst.TimesElapsed >= inst.Def.MaxTimes;
                bool durDone = inst.Def.MaxDuration > 0f
                               && (OS.currentElapsedTime - inst.StartedAt) >= inst.Def.MaxDuration;
                if (timesDone || durDone)
                {
                    clocks.Remove(id);
                    // OnComplete 只在“耗尽自动停止”触发；移除即天然幂等。
                    // 用 ActionHelper 一次性执行（对齐 CompleteAction，支持
                    // <Actions>/<ConditionalActions> 双根，如测试扩展 Clocks/done.xml）
                    if (!ConfigValue.IsNone(inst.Def.OnCompletePath))
                        ActionHelper.ExecuteActionFile(os, inst.Def.OnCompletePath, inst.Def.ExtensionRoot);
                    if (ConfigLoader.Debug)
                        KELog.Info($"[Clock] '{inst.Def.Id}' completed (times={inst.TimesElapsed}, elapsed={(OS.currentElapsedTime - inst.StartedAt):F1}s)");
                }
                else
                {
                    inst.Timer = inst.Def.Interval; // Actions 耗时不计入 Interval，节拍稳定
                }
            }

            if (clocks.Count == 0)
            {
                ActiveClocks.Remove(os);
                Unsubscribe(os);
            }
        }

        private static void Subscribe(OS os)
        {
            if (UpdateHandlers.ContainsKey(os)) return;
            Action<float> handler = dt => OnUpdate(os, dt);
            UpdateHandlers[os] = handler;
            os.UpdateSubscriptions += handler;
        }

        private static void Unsubscribe(OS os)
        {
            if (UpdateHandlers.TryGetValue(os, out var handler))
            {
                os.UpdateSubscriptions -= handler;
                UpdateHandlers.Remove(os);
            }
        }

        // ========== 预加载解析 ==========

        /// <summary>解析 Clock 文件为不可变定义（启动时一次性；失败返回 null）。</summary>
        private static ClockDefinition LoadDefinition(string fullPath, string extensionRoot)
        {
            try
            {
                var executor = new EventExecutor(fullPath, true);
                ElementInfo clockInfo = null;
                // 通配 + ParseInterior：捕获顶层 <Clock>，元素结束时 info.Children/Attributes 已完整
                executor.RegisterExecutor("*", (exec, info) => clockInfo = info, ParseOption.ParseInterior);
                if (!executor.TryParse(out _) || clockInfo == null) return null;

                var attrs = clockInfo.Attributes;
                string id = attrs.GetString("ID");
                if (string.IsNullOrWhiteSpace(id))
                    id = Path.GetFileNameWithoutExtension(fullPath);
                float interval = attrs.GetFloat("Interval", 1f);
                int times = attrs.GetInt("Times", 0);
                float duration = attrs.GetFloat("Duration", 0f);
                string onCompletePath = attrs.GetString("OnComplete");

                if (interval <= 0f)
                {
                    KELog.Warn($"[Clock] '{id}' invalid Interval={interval} (must be > 0), start refused");
                    return null;
                }
                if (times < 0) times = 0;      // 负数归一为无限
                if (duration < 0f) duration = 0f; // 负数归一为不限

                var actions = new List<SerializableAction>();
                var actionsEl = clockInfo.Children.GetElement("Actions");
                if (actionsEl != null)
                {
                    foreach (var child in actionsEl.Children)
                    {
                        try
                        {
                            actions.Add(ActionsLoader.ReadAction(child));
                        }
                        catch (Exception ex)
                        {
                            KELog.Warn($"[Clock] '{id}' action '{child.Name}' load failed: {ex.Message}");
                        }
                    }
                }

                // OnComplete：相对扩展根的动作文件路径（对齐 CompleteAction，
                // 支持 <Actions>/<ConditionalActions> 双根，由 ActionHelper 一次性执行；
                // NONE/空 = 不执行（不查文件，避免 NONE 误报）
                if (!ConfigValue.IsNone(onCompletePath))
                {
                    string ocFull = NormalizePath(Path.Combine(extensionRoot ?? "", onCompletePath));
                    if (!File.Exists(ocFull))
                    {
                        KELog.Warn($"[Clock] '{id}' OnComplete file not found: {onCompletePath}");
                        onCompletePath = null;
                    }
                }

                return new ClockDefinition
                {
                    Id = id,
                    SourcePath = fullPath,
                    Interval = interval,
                    MaxTimes = times,
                    MaxDuration = duration,
                    Actions = actions,
                    OnCompletePath = onCompletePath,
                    ExtensionRoot = extensionRoot
                };
            }
            catch (Exception ex)
            {
                KELog.Error("[Clock] LoadDefinition failed: " + ex.Message);
                return null;
            }
        }

        private static string NormalizePath(string path)
            => string.IsNullOrWhiteSpace(path) ? path : path.Replace('\\', '/');

        /// <summary>Clock 文件解析结果（不可变定义，不含运行时状态）。</summary>
        private class ClockDefinition
        {
            public string Id;
            public string SourcePath;                 // 规范化完整路径（供路径停止匹配）
            public float Interval;                    // 触发间隔（秒），>0
            public int MaxTimes;                      // 0 = 无限
            public float MaxDuration;                 // 0 = 不限
            public List<SerializableAction> Actions;  // 预加载 <Actions>
            public string OnCompletePath;             // OnComplete 动作文件（相对扩展根，可空）
            public string ExtensionRoot;              // 扩展根（OnComplete 执行用）
        }

        /// <summary>运行时实例。</summary>
        private class ClockInstance
        {
            public ClockDefinition Def;
            public float Timer;       // 距下次触发剩余秒数
            public int TimesElapsed;  // 已触发次数
            public double StartedAt;  // OS.currentElapsedTime（double）启动时刻，Duration 起算
        }
    }
}
