namespace KernelExtensions.Saving
{
    /// <summary>
    /// Clock 持久化状态的数据传输对象（9.46，对齐 PhaseSwiftPersistentState 命名）。
    /// 由 ClockSaveExecutor 解析存档 XML 填充，存入 ClockManager.PendingRestore，
    /// 供 OSLoaded 后重建运行中的 Clock。
    ///
    /// 只存“运行中”的 Clock（耗尽/手动停止的实例已在 ActiveClocks 移除，天然不存）。
    /// 定义参数（Interval/Times/Duration/OnComplete/Actions）不随档，由 SourcePath
    /// 重载 Clock 文件恢复（剧情资产，文件本身即定义）。
    /// </summary>
    public class ClockPersistentState
    {
        /// <summary>Clock 标识（ClockStop/去重用，来自 Clock 文件 ID）。</summary>
        public string Id;

        /// <summary>Clock 文件完整路径（规范化，恢复时重载定义）。</summary>
        public string SourcePath;

        /// <summary>扩展根（OnComplete 相对路径解析用）。</summary>
        public string ExtensionRoot;

        /// <summary>已触发次数（Times 计数连续恢复）。</summary>
        public int TimesElapsed;

        /// <summary>已运行总时长（秒）= 存档时 now - StartedAt，Duration 判定连续。</summary>
        public float Elapsed;

        /// <summary>距下次触发剩余秒数（简单起见随档保存，免推导边界问题）。</summary>
        public float Timer;
    }
}
