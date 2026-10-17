using BepInEx.Logging;
using Hacknet;
using KernelExtensions.Configs;

namespace KernelExtensions.Utilities
{
    /// <summary>
    /// 统一日志。级别约定（2026-08-20 定）：
    ///   KELog.Debug("...");  // KE 源码开发者排错：内部细节/时序/数据（Clock 状态、触发过程等）
    ///                         // 默认关闭（Debug 配置开启才显示）
    ///   KELog.Info("...");   // 扩展作者排错：正常状态流转/关键步骤结果
    ///                         // （剧本执行到哪、状态对不对，如 overlay 激活、节点恢复）
    ///   KELog.Warn("...");   // 可恢复/降级/注意：功能继续但值得关注（跳过、用默认值、配置提醒）
    ///   KELog.Error("...");  // 不该发生/功能失败：代码路径错误、配置非法、异常导致功能未生效
    ///
    /// 分层: Debug(默认关) &lt; Info &lt; Warn &lt; Error（后三者始终显示）
    /// Info/Debug 划分（2026-08-20）:
    ///   Info  = 扩展作者配置/触发动作的“结果/状态”（动作级，一次触发一条，如 Clock 启停/完成）
    ///   Debug = KE 内部机制/数据流细节（数据级/循环级，如 len、预加载计数、内部比较）
    ///
    /// <para>
    /// <b>终端镜像（<see cref="WarnT"/> / <see cref="ErrorT"/>）</b>：
    /// 光落盘不够 —— 扩展作者通常不会去翻 BepInEx 日志，而配置/动作点写错时
    /// 游戏里"什么都不发生"，无从排查。这两个变体在写日志的**同时**把同一条消息
    /// 送进游戏内终端。
    /// </para>
    ///
    /// <para>
    /// ⚠️ 只用于**作者可作业**的诊断（作者能修 + 不显示就等于静默失效）。
    /// 运行期降级、渲染失败、内部补丁异常等**不要**用，否则会刷屏且作者也修不了。
    /// 全量判定清单见根目录《9.72-覆盖清单.md》。
    /// </para>
    ///
    /// <para>
    /// <b>防刷屏</b>：同一 OS 会话内终端最多输出 <see cref="MaxTerminalLines"/> 行，
    /// 超出后只打一条省略提示（日志不受限）。配置加载这类可能一次报十几条的场合，
    /// 用 <see cref="BeginBatch"/> / <see cref="EndBatch"/> 聚合成一段。
    /// </para>
    /// </summary>
    public static class KELog
    {
        private static ManualLogSource _log;

        /// <summary>
        /// 【整活】报错是因为报错功能开着，关掉就不会报错了。
        /// <para>
        /// 置 false 后所有 <see cref="Error"/> 调用静默（其余级别不受影响）。
        /// 仅代码内开关，不接入 KE-Config。
        /// </para>
        /// </summary>
        public static bool EnableErrorLogging = true;

        // ===== 终端镜像（9.72）=====

        /// <summary>单次 OS 会话内允许写入终端的最大行数（防刷屏）。</summary>
        private const int MaxTerminalLines = 30;

        /// <summary>聚合批次里最多逐条列出的条数，其余折叠成一行。</summary>
        private const int BatchHeadLines = 5;

        private static int _terminalLines;
        private static bool _suppressed;
        private static OS _boundOs;

        private static readonly List<string> _batch = new();
        private static bool _batchActive;
        private static int _batchDepth;

        public static void Init()
        {
            _log = Logger.CreateLogSource("KernelExtensions");
        }

        public static void Info(string msg)
        {
            _log?.LogInfo(msg);
        }

        public static void Warn(string msg)
        {
            _log?.LogWarning(msg);
        }

        public static void Error(string msg)
        {
            if (!EnableErrorLogging) return;
            _log?.LogError(msg);
        }

        public static void Debug(string msg)
        {
            if (ConfigLoader.Debug)
                _log?.LogDebug(msg);
        }

        /// <summary>Warn + 同一条也进游戏内终端（作者可作业的诊断）。</summary>
        public static void WarnT(string msg)
        {
            Warn(msg);
            Mirror(msg, isError: false);
        }

        /// <summary>Error + 同一条也进游戏内终端（作者可作业的诊断）。</summary>
        public static void ErrorT(string msg)
        {
            Error(msg);
            if (EnableErrorLogging) Mirror(msg, isError: true);
        }

        /// <summary>
        /// 开始聚合：期间的 <see cref="WarnT"/> / <see cref="ErrorT"/> 先收集，
        /// 等 <see cref="EndBatch"/> 时一次性输出。用于配置加载（可能一次报十几条）。
        /// </summary>
        public static void BeginBatch()
        {
            if (_batchDepth++ == 0) _batch.Clear();   // 嵌套时只在最外层清空
            _batchActive = true;
        }

        /// <summary>
        /// 结束聚合并输出。<paramref name="header"/> 形如
        /// <c>PortHackV2/Example.xml</c>（说清是哪份配置）。
        /// 明细最多列 <see cref="BatchHeadLines"/> 条，其余折叠。
        /// </summary>
        public static void EndBatch(string header)
        {
            if (_batchDepth > 0) _batchDepth--;
            if (_batchDepth > 0) return;               // 仍有外层批次 → 不输出

            _batchActive = false;
            if (_batch.Count == 0) return;

            WriteTerminal($"[KE] {header}: {_batch.Count} issue(s)");
            int shown = 0;
            foreach (string m in _batch)
            {
                if (shown >= BatchHeadLines) break;
                WriteTerminal("  - " + OneLine(m));
                shown++;
            }
            if (_batch.Count > shown)
                WriteTerminal($"  … +{_batch.Count - shown} more (see BepInEx log)");

            _batch.Clear();
        }

        /// <summary>丢弃当前批次（异常路径用，避免残留到下一次性）。</summary>
        public static void DiscardBatch()
        {
            _batchActive = false;
            _batchDepth = 0;
            _batch.Clear();
        }

        private static void Mirror(string msg, bool isError)
        {
            if (_batchActive)
            {
                // 聚合里也要保证日志仍写（Warn/Error 已先写过），这里只收集终端文本
                _batch.Add(msg);
                return;
            }
            WriteTerminal("[KE] " + OneLine(msg));
        }

        private static void WriteTerminal(string text)
        {
            if (_suppressed) return;

            OS os = OS.currentInstance;
            if (os == null) return;

            // 换了一次 OS（新会话/读档）→ 重新计数
            if (!ReferenceEquals(os, _boundOs))
            {
                _boundOs = os;
                _terminalLines = 0;
                _suppressed = false;
            }

            if (_terminalLines >= MaxTerminalLines)
            {
                _suppressed = true;
                try
                {
                    os.write($"[KE] further KE diagnostics suppressed after {MaxTerminalLines} lines "
                        + "— see the BepInEx log for the rest.");
                }
                catch { }
                return;
            }

            try
            {
                os.write(text);
                _terminalLines++;
            }
            catch { }
        }

        /// <summary>压成单行：日志正文可能带换行，终端一行更整齐。</summary>
        private static string OneLine(string msg)
        {
            if (string.IsNullOrEmpty(msg)) return "";
            string s = msg.Replace("\r", " ").Replace("\n", " ");
            return s.Length <= 200 ? s : s.Substring(0, 200) + "…";
        }

        /// <summary>换会话/卸载时重置计数与批次（由入口调用）。</summary>
        public static void ResetTerminalMirror()
        {
            _boundOs = null;
            _terminalLines = 0;
            _suppressed = false;
            DiscardBatch();
        }
    }
}
