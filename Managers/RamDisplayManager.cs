using KernelExtensions.Utilities;

namespace KernelExtensions.Managers
{
    /// <summary>
    /// RAM 显示的可调状态。
    /// <para>
    /// 只影响原版 <c>RamModule</c> 的文字 <c>"USED RAM: x / y mb"</c>：
    /// <c>x</c> 与 <c>y</c> 统一乘 <see cref="Multiplier"/>，单位串换成 <see cref="Unit"/>。
    /// **RAM 条的长度与其它绘制一律不动**（仍按真实占用比例）。
    /// </para>
    /// <para>
    /// 没有 KE-Config.xml 段：默认值写死在这里，由 <c>RamDisplayAction</c> 运行时覆盖，
    /// 并随存档持久化（<c>RamDisplaySaveExecutor</c>）。
    /// </para>
    /// </summary>
    public static class RamDisplayManager
    {
        public const float DefaultMultiplier = 1f;
        public const string DefaultUnit = "mb";

        /// <summary>x/y 的统一倍率（默认 1 = 原样显示）。</summary>
        public static float Multiplier = DefaultMultiplier;

        /// <summary>单位串（默认 <c>mb</c>）。</summary>
        public static string Unit = DefaultUnit;

        /// <summary>
        /// 读档暂存：由 <c>RamDisplaySaveExecutor</c> 填充，<c>OSLoaded</c> 时消费。
        /// 用暂存而不是直改，是为了跟 <c>Reset()</c> 拉开顺序——
        /// executor 在 OSLoaded 之前跑，若它直接写 Manager，OSLoaded 的 Reset 会把刚恢复的值抹掉。
        /// </summary>
        public static bool PendingRestore;
        public static float PendingMultiplier = DefaultMultiplier;
        public static string PendingUnit = DefaultUnit;

        /// <summary>恢复默认（新档 / 无存档数据时）。</summary>
        public static void Reset()
        {
            Multiplier = DefaultMultiplier;
            Unit = DefaultUnit;
        }

        /// <summary>
        /// 在 <c>OSLoaded</c> 时调用：先回到默认，有读档数据则套用。
        /// （新档 / 存档里没有 &lt;RamDisplayData&gt; 时就会干净地停在默认值，
        ///   不会残留上一个存档的设置。）
        /// </summary>
        public static void ApplyOnLoaded()
        {
            Reset();
            if (!PendingRestore) return;

            Multiplier = PendingMultiplier;
            Unit = NormalizeUnit(PendingUnit);
            PendingRestore = false;   // 消费即清空，防残留串档
        }

        /// <summary>按 NONE 约定解析单位串：<c>NONE</c> / 空 / 全空白 → 默认 <c>mb</c>。</summary>
        public static string NormalizeUnit(string unit)
        {
            return ConfigValue.IsNone(unit) ? DefaultUnit : unit;
        }

        /// <summary>
        /// 把一个 RAM 数值按倍率缩放为显示文本。保留小数，最多两位、去掉尾随零
        /// （1 → "512"，0.1 → "102.4"，1/3 → "340.99"）。
        /// </summary>
        public static string Scale(long value)
        {
            double scaled = value * (double)Multiplier;
            return scaled.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
