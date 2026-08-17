using System;
using System.Collections.Generic;
using System.IO;
using Hacknet;
using KernelExtensions.Config;
using KernelExtensions.Utility;
using Pathfinder.Replacements;
using Pathfinder.Util;
using Pathfinder.Util.XML;

namespace KernelExtensions.Modules
{
    /// <summary>
    /// 9.34 Clock 定时器管理器。
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
    ///   OnComplete — 可选，耗尽后执行的 Action 文件（相对扩展根，对齐 9.36）
    /// </summary>
    internal static class ClockManager
    {
        private static readonly Dictionary<OS, Dictionary<string, ClockInstance>> ActiveClocks = new();
        // 每个 OS 的 Update 委托缓存：必须保存引用，否则 -= 无法匹配 lambda 实例
        private static readonly Dictionary<OS, Action<float>> UpdateHandlers = new();

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

            if (KEConfigLoader.Debug)
                KELog.Debug($"[Clock] started '{def.Id}' interval={def.Interval}s times={def.MaxTimes} duration={def.MaxDuration}s");
        }

        /// <summary>按 ID 停止（手动取消，不触发 OnComplete）。未知 ID 静默。</summary>
        public static void StopByID(OS os, string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return;
            if (!ActiveClocks.TryGetValue(os, out var clocks) || !clocks.Remove(id)) return;
            if (KEConfigLoader.Debug) KELog.Debug($"[Clock] stopped '{id}' by ID");
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
            if (toRemove.Count > 0 && KEConfigLoader.Debug)
                KELog.Debug($"[Clock] stopped '{string.Join(",", toRemove)}' by path");
            if (clocks.Count == 0)
            {
                ActiveClocks.Remove(os);
                Unsubscribe(os);
            }
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
                    // OnComplete 只在"耗尽自动停止"触发；移除即天然幂等
                    foreach (var a in inst.Def.OnComplete) a.Trigger(os);
                    if (KEConfigLoader.Debug)
                        KELog.Debug($"[Clock] '{inst.Def.Id}' completed (times={inst.TimesElapsed}, elapsed={(OS.currentElapsedTime - inst.StartedAt):F1}s)");
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

                List<SerializableAction> onComplete = null;
                if (!string.IsNullOrWhiteSpace(onCompletePath))
                {
                    string ocFull = NormalizePath(Path.Combine(extensionRoot ?? "", onCompletePath));
                    onComplete = LoadActionsFile(ocFull);
                    if (onComplete == null)
                        KELog.Warn($"[Clock] '{id}' OnComplete file not found/parse failed: {onCompletePath}");
                }

                return new ClockDefinition
                {
                    Id = id,
                    SourcePath = fullPath,
                    Interval = interval,
                    MaxTimes = times,
                    MaxDuration = duration,
                    Actions = actions,
                    OnComplete = onComplete ?? new List<SerializableAction>()
                };
            }
            catch (Exception ex)
            {
                KELog.Error("[Clock] LoadDefinition failed: " + ex.Message);
                return null;
            }
        }

        /// <summary>预加载一个动作文件（&lt;Actions&gt; 根，OnComplete 用）：只收集不触发。</summary>
        private static List<SerializableAction> LoadActionsFile(string fullPath)
        {
            if (!File.Exists(fullPath)) return null;
            try
            {
                var executor = new EventExecutor(fullPath, true);
                var list = new List<SerializableAction>();
                executor.RegisterExecutor("Actions", (exec, info) =>
                {
                    foreach (var child in info.Children)
                    {
                        try
                        {
                            list.Add(ActionsLoader.ReadAction(child));
                        }
                        catch (Exception ex)
                        {
                            KELog.Warn("[Clock] OnComplete action load failed: " + ex.Message);
                        }
                    }
                }, ParseOption.ParseInterior);
                if (!executor.TryParse(out _)) return null;
                return list;
            }
            catch (Exception ex)
            {
                KELog.Error("[Clock] LoadActionsFile failed: " + ex.Message);
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
            public List<SerializableAction> OnComplete; // 预加载 OnComplete 文件（可空）
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
