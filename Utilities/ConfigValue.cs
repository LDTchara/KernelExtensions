using System.Globalization;

namespace KernelExtensions.Utilities
{
    /// <summary>
    /// 配置值语义工具。
    ///
    /// · NONE 约定：对齐原版生态，字符串配置项写 `NONE`（大小写不敏感，兼容原版 `none`）
    ///   或留空 = 显式禁用该功能；不写属性 = 用默认值（参见 AGENTS.md「配置 NONE 约定」）。
    ///   工具层（ActionHelper/SoundHelper/MusicPathResolver/ColorHelper）与判断层
    ///   （FlightDaemon/ClockManager/ScreenBleedWCCManager 等）统一走此判断。
    /// · 数值范围解析：见 <see cref="ParseInRange"/>（对齐「负数/无效值约定」）。
    /// </summary>
    public static class ConfigValue
    {
        /// <summary>是否视为"禁用/无"：null、空白、或 NONE（大小写不敏感）。</summary>
        public static bool IsNone(string s)
            => string.IsNullOrWhiteSpace(s) || s.Trim().Equals("NONE", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// 解析数值配置项并做范围校验，**不抛异常**。
        ///
        /// 留空 / 未写 → 返回 <paramref name="fallback"/>，<paramref name="ok"/> = true（非错误）。
        /// 写了但无效（非数字、NaN、Infinity、超出 [min, max]）→ 返回 <paramref name="fallback"/>，
        /// <paramref name="ok"/> = false，由调用方决定要不要 Warn。
        ///
        /// 用 InvariantCulture：配置文件是跨语言的，不能用当前系统的区域设置解析小数。
        /// 负数在 min ≥ 0 时自然落入“无效”，与本项目的「负数 = 默认」约定一致。
        /// </summary>
        public static float ParseInRange(string raw, float fallback, float min, float max, out bool ok)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                ok = true;
                return fallback;
            }

            if (!float.TryParse(raw.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float v)
                || float.IsNaN(v) || float.IsInfinity(v)
                || v < min || v > max)
            {
                ok = false;
                return fallback;
            }

            ok = true;
            return v;
        }
    }
}
