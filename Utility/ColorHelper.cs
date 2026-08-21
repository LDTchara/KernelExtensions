using Microsoft.Xna.Framework;

namespace KernelExtensions.Utility
{
    /// <summary>
    /// 共享颜色工具类。
    ///
    /// 集中的 HSV/RGB 转换和十六进制颜色解析，
    /// 由 CustomColorPatch、PhaseSwiftManager、CustomTrialExe 等共同使用。
    ///
    /// 函数：
    ///   HSVToColor(hue, saturation, value)  — HSV → Color，hue 范围 0~1
    ///   ParseHexColor(hex)                   — #RRGGBB 或 #AARRGGBB → Color
    /// </summary>
    public static class ColorHelper
    {
        /// <summary>HSV → RGB，hue 范围 0~1</summary>
        public static Color HSVToColor(float hue, float saturation, float value)
        {
            int hi = (int)(hue * 6) % 6;
            float f = hue * 6 - hi;
            float p = value * (1f - saturation);
            float q = value * (1f - f * saturation);
            float t = value * (1f - (1f - f) * saturation);
            return hi switch
            {
                0 => new Color(value, t, p),
                1 => new Color(q, value, p),
                2 => new Color(p, value, t),
                3 => new Color(p, q, value),
                4 => new Color(t, p, value),
                _ => new Color(value, p, q),
            };
        }

        /// <summary>解析颜色值：支持 #RRGGBB、#AARRGGBB、R,G,B、R,G,B,A</summary>
        public static Color ParseHexColor(string input)
        {
            if (string.IsNullOrEmpty(input)) return Color.White;

            // 先尝试 R,G,B[,A] 格式
            string trimmed = input.Trim();
            if (trimmed.Contains(','))
            {
                string[] parts = trimmed.Split(',');
                if (parts.Length >= 3 &&
                    byte.TryParse(parts[0].Trim(), out byte cr) &&
                    byte.TryParse(parts[1].Trim(), out byte cg) &&
                    byte.TryParse(parts[2].Trim(), out byte cb))
                {
                    byte ca = 255;
                    if (parts.Length >= 4) byte.TryParse(parts[3].Trim(), out ca);
                    return new Color(cr, cg, cb, ca);
                }
            }

            // #RRGGBB / #AARRGGBB 格式
            string hex = trimmed.TrimStart('#');
            if (hex.Length < 6) return Color.White;
            try
            {
                byte r = Convert.ToByte(hex.Substring(0, 2), 16);
                byte g = Convert.ToByte(hex.Substring(2, 2), 16);
                byte b = Convert.ToByte(hex.Substring(4, 2), 16);
                byte a = 255;
                if (hex.Length >= 8) a = Convert.ToByte(hex.Substring(6, 2), 16);
                return new Color(r, g, b, a);
            }
            catch { return Color.White; }
        }
    }
}
