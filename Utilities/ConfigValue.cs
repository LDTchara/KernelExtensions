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
        ///
        /// <para>
        /// <b>关于 <paramref name="max"/>：只在下面两类情况下才给真实上限</b>，
        /// 其余一律传 <c>float.MaxValue</c>（= 不限）。
        /// <list type="number">
        ///   <item><b>语义有界</b> —— 值域本身就是闭区间，超出去没有意义。
        ///     例：百分比 <c>&lt;Rng&gt;</c> 是 0~100、比例 <c>&lt;SpinUpRatio&gt;</c> 是 0~1
        ///     （它要乘总时长，大于 1 会让"起转"比整段还长）。</item>
        ///   <item><b>后续要转 <c>int</c></b> —— 超大 <c>float</c> 转 <c>int</c> 会溢出成
        ///     <c>int.MinValue</c>，必须在这里挡住。</item>
        /// </list>
        /// </para>
        ///
        /// <para>
        /// ⚠️ <b>不要为了"值看着太大"随手加一个上限。</b> 本项目曾给
        /// <c>&lt;SelfOverloadRate&gt;</c> 定过 1 的上限，结果作者写 10 被**静默换成默认 1**，
        /// 体感是"配置写了但行为不符"，排查成本很高。数值平衡是**扩展作者的事**；
        /// 解析层的职责只有"是不是一个能用的数"。
        /// </para>
        ///
        /// <para>
        /// 调用方显式传 <c>float.MaxValue</c> 而不是让本参数带默认值：C# 不允许
        /// <c>out</c> 参数跟在可选参数之后，而且"没有上限"应当是**看得见**的决定，
        /// 不该藏在一个默认值里。
        /// </para>
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
