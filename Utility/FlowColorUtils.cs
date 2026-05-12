using System;
using Microsoft.Xna.Framework;

namespace KernelExtensions.Utility
{
    /// <summary>
    /// 提供基于时间流动的彩虹颜色计算。
    /// </summary>
    public static class FlowColorUtils
    {
        /// <summary>
        /// 根据当前位置（0~1）和基础时间偏移，返回流动的彩虹色。
        /// 每个字符的颜色会随时间循环变化。
        /// </summary>
        /// <param name="position">字符在文本中的相对位置（0~1）</param>
        /// <param name="baseTime">基础时间（秒），用于产生流动效果</param>
        public static Color GetFlowingRainbowColor(float position, float baseTime)
        {
            // 每个字符拥有独立的相位偏移，同时整体色调随时间循环
            float hue = (baseTime * 100f + position * 360f) % 360f;
            return HslToRgbSimple(hue, 1.0, 0.5);    // 纯色、标准亮度
        }

        /// <summary>
        /// HSL → RGB，同 KernelExtensions 内部实现，独立一份避免依赖。
        /// </summary>
        public static Color HslToRgbSimple(double h, double s, double l)
        {
            double c = (1 - Math.Abs(2 * l - 1)) * s;
            double x = c * (1 - Math.Abs((h / 60) % 2 - 1));
            double m = l - c / 2;
            double r1, g1, b1;

            if (h < 60) { r1 = c; g1 = x; b1 = 0; }
            else if (h < 120) { r1 = x; g1 = c; b1 = 0; }
            else if (h < 180) { r1 = 0; g1 = c; b1 = x; }
            else if (h < 240) { r1 = 0; g1 = x; b1 = c; }
            else if (h < 300) { r1 = x; g1 = 0; b1 = c; }
            else { r1 = c; g1 = 0; b1 = x; }

            byte r = (byte)Math.Max(0, Math.Min(255, (r1 + m) * 255));
            byte g = (byte)Math.Max(0, Math.Min(255, (g1 + m) * 255));
            byte b = (byte)Math.Max(0, Math.Min(255, (b1 + m) * 255));
            return new Color(r, g, b);
        }
    }
}