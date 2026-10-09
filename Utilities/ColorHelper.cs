using Microsoft.Xna.Framework;

using KernelExtensions.Managers;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace KernelExtensions.Utilities
{
    /// <summary>
    /// 共享颜色工具类。
    ///
    /// 集中的 HSV/RGB 转换与颜色字符串解析，
    /// 由 CustomColorPatch、PhaseSwiftManager、CustomTrialExe 等共同使用。
    ///
    /// 函数：
    ///   HSVToColor(hue, saturation, value)      — HSV → Color，hue 范围 0~1
    ///   ParseHexColor(hex)                      — 历史入口；内部转调 ParseStaticColor(input, White)
    ///   ParseStaticColor(input, fallback)       — #RGB / #RRGGBB / #AARRGGBB / R,G,B[,A] / XNA 命名色
    ///   TryGetNamedColor(name, out color)       — 按名字取 XNA 命名色（大小写不敏感）
    ///
    /// ⚠️ 为何不用 <c>Microsoft.Xna.Framework.Design.ColorConverter</c>：
    /// FNA 17.02 的它只支持 "R,G,B,A"（0~1 float），**无名字表、无 hex**；
    /// 且 .NET 的 TypeConverter 会走到 <c>ConvertFrom(null, null, text)</c>，
    /// culture 为 null 导致 <c>culture.TextInfo</c> 抛 NRE —— 即那些兜底是死代码，
    /// 命名色从未生效。所以命名色只能自己反射 XNA 的 Color 静态属性来建表。
    /// </summary>
    public static class ColorHelper
    {
        /// <summary>
        /// XNA 命名色表（反射 <see cref="Color"/> 的公开静态属性与字段），只建一次。
        /// 用 OrdinalIgnoreCase：大小写不敏感，避免“预设敏感/名字忽略”的分裂（见颜色解析统一方案）。
        /// </summary>
        private static readonly Dictionary<string, Color> _namedColors = BuildNamedColorTable();

        private static Dictionary<string, Color> BuildNamedColorTable()
        {
            var map = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase);
            Type t = typeof(Color);
            const BindingFlags flags = BindingFlags.Public | BindingFlags.Static;
            try
            {
                foreach (var p in t.GetProperties(flags))
                {
                    if (p.PropertyType != typeof(Color) || p.GetIndexParameters().Length != 0) continue;
                    try { map[p.Name] = (Color)p.GetValue(null); } catch { }
                }
                foreach (var f in t.GetFields(flags))
                {
                    if (f.FieldType != typeof(Color)) continue;
                    try { map[f.Name] = (Color)f.GetValue(null); } catch { }
                }
            }
            catch { }
            return map;
        }

        /// <summary>按名字取 XNA 命名色（大小写不敏感）。</summary>
        public static bool TryGetNamedColor(string name, out Color color)
            => _namedColors.TryGetValue(name == null ? string.Empty : name.Trim(), out color);

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

        /// <summary>解析颜色值：支持 #RGB、#RRGGBB、#AARRGGBB（Alpha 在首位）、R,G,B、R,G,B,A、XNA 命名色。全部失败返回 fallback。</summary>
        public static Color ParseStaticColor(string input, Color fallback)
        {
            if (ConfigValue.IsNone(input)) return fallback;
            string trimmed = input.Trim();
            if (trimmed.Length == 0) return fallback;

            // R,G,B[,A]：按字节（0~255）解析
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
                return fallback;
            }

            // #RGB / #RRGGBB / #AARRGGBB（Alpha 在首位，对齐原版 Utils.ColorFromHexString）
            if (trimmed.StartsWith("#", StringComparison.Ordinal))
            {
                string hex = trimmed.Substring(1);
                try
                {
                    if (hex.Length == 3)   // #RGB → 每位重复（#F0A → #FF00AA）
                    {
                        byte r3 = Convert.ToByte(new string(hex[0], 2), 16);
                        byte g3 = Convert.ToByte(new string(hex[1], 2), 16);
                        byte b3 = Convert.ToByte(new string(hex[2], 2), 16);
                        return new Color(r3, g3, b3, (byte)255);
                    }
                    if (hex.Length == 6)
                    {
                        byte r = Convert.ToByte(hex.Substring(0, 2), 16);
                        byte g = Convert.ToByte(hex.Substring(2, 2), 16);
                        byte b = Convert.ToByte(hex.Substring(4, 2), 16);
                        return new Color(r, g, b, (byte)255);
                    }
                    if (hex.Length == 8)   // #AARRGGBB
                    {
                        byte a = Convert.ToByte(hex.Substring(0, 2), 16);
                        byte r = Convert.ToByte(hex.Substring(2, 2), 16);
                        byte g = Convert.ToByte(hex.Substring(4, 2), 16);
                        byte b = Convert.ToByte(hex.Substring(6, 2), 16);
                        return new Color(r, g, b, a);
                    }
                }
                catch { }
                return fallback;
            }

            // XNA 命名色（Red / CornflowerBlue / …）
            if (TryGetNamedColor(trimmed, out Color named)) return named;

            return fallback;
        }

        /// <summary>
        /// 解析颜色值（历史入口，保持签名不变）。
        /// 内部转调 <see cref="ParseStaticColor"/>，因此现在也支持 <c>#RGB</c> 与 XNA 命名色。
        /// 全部失败时沿用旧行为返回 <c>Color.White</c>。
        /// </summary>
        public static Color ParseHexColor(string input)
            => ParseStaticColor(input, Color.White);
    }
}
