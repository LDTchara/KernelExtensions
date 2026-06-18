using BepInEx.Logging;

namespace KernelExtensions.Utility
{
    /// <summary>
    /// 统一日志。用法：
    ///   KELog.Info("...");      // 始终显示
    ///   KELog.Warn("...");      // 始终显示（黄色）
    ///   KELog.Error("...");     // 始终显示（红色）
    ///   KELog.Debug("...");     // 仅在 Debug 配置开启时显示
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
