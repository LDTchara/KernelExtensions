using BepInEx.Logging;

namespace KernelExtensions.Utility
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
    /// 用法：KELog.Info("...");
    /// </summary>
    public static class KELog
    {
        private static ManualLogSource _log;

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
            _log?.LogError(msg);
        }

        public static void Debug(string msg)
        {
            if (KEConfigLoader.Debug)
                _log?.LogDebug(msg);
        }
    }
}
