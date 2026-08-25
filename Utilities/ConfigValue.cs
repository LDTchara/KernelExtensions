using System;

namespace KernelExtensions.Utilities
{
    /// <summary>
    /// 配置值语义工具（NONE 约定）。
    /// 对齐原版生态：字符串配置项写 `NONE`（大小写不敏感，兼容原版 `none`）或留空
    /// = 显式禁用该功能；不写属性 = 用默认值（参见 AGENTS.md「配置 NONE 约定」）。
    /// 工具层（ActionHelper/SoundHelper/MusicPathResolver/ColorHelper）与判断层
    /// （FlightDaemon/ClockManager/ScreenBleedWCCManager 等）统一走此判断。
    /// </summary>
    public static class ConfigValue
    {
        /// <summary>是否视为"禁用/无"：null、空白、或 NONE（大小写不敏感）。</summary>
        public static bool IsNone(string s)
            => string.IsNullOrWhiteSpace(s) || s.Trim().Equals("NONE", StringComparison.OrdinalIgnoreCase);
    }
}
