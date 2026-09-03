using Hacknet;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Pathfinder.Util;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Color = Microsoft.Xna.Framework.Color;

namespace KernelExtensions.ThemeColorChanger;

public class ThemeColorChangerAction : Pathfinder.Action.DelayablePathfinderAction
{
    //[XMLStorage] string Color;
    [XMLStorage] public string Locate;//topbar:red,bar1:#000000.......
    
    private Dictionary<string, string> LocateConfig = new()
    {
        { "defaultHighlightColor", null },
        { "defaultTopBarColor", null },
        { "moduleColorSolidDefault", null },
        { "moduleColorStrong", null },
        { "moduleColorBacking", null },
        { "exeModuleTopBar", null },
        { "exeModuleTitleText", null },
        { "warningColor", null },
        { "subtleTextColor", null },
        { "darkBackgroundColor", null },
        { "indentBackgroundColor", null },
        { "outlineColor", null },
        { "lockedColor", null },
        { "brightLockedColor", null },
        { "brightUnlockedColor", null },
        { "unlockedColor", null },
        { "lightGray", null },
        { "shellColor", null },
        { "shellButtonColor", null },
        { "terminalTextColor", null },
        { "topBarTextColor", null },
        { "superLightWhite", null },
        { "connectedNodeHighlight", null },
        { "netmapToolTipColor", null },
        { "netmapToolTipBackground", null },
        { "topBarIconsColor", null },
        { "thisComputerNode", null },
        { "scanlinesColor", null },
        { "AFX_KeyboardMiddle", null },
        { "AFX_KeyboardOuter", null },
        { "AFX_WordLogo", null },
        { "AFX_Other", null }
    };
    public static Color RgbToColor(string rgb)
    {
        if (string.IsNullOrWhiteSpace(rgb))
            throw new ArgumentException("RGB字符串不能为空");

        // 修正点：使用 char[] 数组
        var parts = rgb.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3 && parts.Length != 4)
            throw new ArgumentException($"RGB格式错误，需要3或4个数字: {rgb}");

        byte r = byte.Parse(parts[0].Trim());
        byte g = byte.Parse(parts[1].Trim());
        byte b = byte.Parse(parts[2].Trim());

        if (parts.Length == 3)
            return new Color(r, g, b);
        else
        {
            byte a = byte.Parse(parts[3].Trim());
            return new Color(r, g, b, a);
        }
    }
    private bool TryParseColor(string input, out Color color)
    {
        color = Microsoft.Xna.Framework.Color.White;

        // 1. 十六进制
        if (IsHexColor(input))
        {
            try { color = HexToColor(input); return true; }
            catch { return false; }
        }

        // 2. XNA 预定义名称
        if (ColorNameMapper.TryGetColor(input, out color))
            return true;

        // 3. RGB 数值 (如 "12,122,1")
        if (input.Contains(','))
        {
            try { color = RgbToColor(input); return true; }
            catch { return false; }
        }

        return false;
    }
    private List<string> StringToList(string s, string splitSignText)
    {
        try
        {
            // 取分隔符的第一个字符（假设传入的是单个字符）
            char separator = splitSignText[0];
            return s.Split(separator).ToList();
        }
        catch (Exception e)
        {
            Console.WriteLine(e.ToString());
            return null;
        }
    }



    public static Microsoft.Xna.Framework.Color HexToColor(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
            throw new ArgumentException("颜色字符串不能为空");

        // 去除前缀 # 或 0x
        hex = hex.TrimStart('#', '0', 'x', 'X');

        // 根据长度判断格式：6位(RGB) 或 8位(ARGB)
        if (hex.Length == 6)
        {
            byte r = byte.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
            byte g = byte.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
            byte b = byte.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
            return new Color(r, g, b); // XNA Color 构造函数 (byte,byte,byte)
        }
        else if (hex.Length == 8)
        {
            byte a = byte.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
            byte r = byte.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
            byte g = byte.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
            byte b = byte.Parse(hex.Substring(6, 2), System.Globalization.NumberStyles.HexNumber);
            return new Color(r, g, b, a); // XNA Color 构造函数 (byte,byte,byte,byte)
        }
        else
        {
            throw new ArgumentException($"无效的十六进制颜色格式: {hex}");
        }
    }
    public static bool IsHexColor(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return false;
        // 去掉可能的前缀
        string cleaned = input.TrimStart('#', '0', 'x', 'X');
        // 长度必须为 6 或 8
        if (cleaned.Length != 6 && cleaned.Length != 8) return false;
        // 检查是否都是十六进制字符
        return System.Text.RegularExpressions.Regex.IsMatch(cleaned, @"^[0-9A-Fa-f]+$");
    }
    public override void Trigger(OS os)
    {
        Console.WriteLine("[ChangeThemeColor] triggered, locate=" + Locate);
        if (string.IsNullOrWhiteSpace(Locate))
            return;

        // 分割 Locate 字符串（正确使用 char[] 重载）
        var pairs = Locate.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

        // 解析所有键值对，存储为 Dictionary<string, Color>
        var parsedColors = new Dictionary<string, Color>();
        foreach (var pair in pairs)
        {
            // 正确使用 Split(char[], int) 重载
            var kv = pair.Split(new char[] { ':' }, 2);
            if (kv.Length != 2) continue;

            string key = kv[0].Trim();
            string value = kv[1].Trim();

            if (!LocateConfig.ContainsKey(key))
                continue; // 忽略未知键

            if (TryParseColor(value, out Color color))
            {
                parsedColors[key] = color;
            }
            else
            {
                Console.WriteLine($"无法解析颜色: {key} = {value}");
            }
        }

        // 获取或创建 CustomTheme
        var theme = ThemeManager.LastLoadedCustomTheme;
        if (theme == null || ThemeManager.currentTheme != OSTheme.Custom)
        {
            theme = new CustomTheme();
            // 复制当前 OS 的主题字段值
            foreach (var f in typeof(CustomTheme).GetFields())
            {
                var src = typeof(OS).GetField(f.Name);
                if (src != null)
                    f.SetValue(theme, src.GetValue(os));
            }
        }

        // 应用解析到的颜色到 theme
        var themeType = theme.GetType();
        foreach (var kv in parsedColors)
        {
            var field = themeType.GetField(kv.Key);
            if (field != null && field.FieldType == typeof(Color))
            {
                field.SetValue(theme, kv.Value);
            }
        }

        // 只把颜色灌入当前 OS：不切换主题，保留现有布局与背景
        theme.LoadIntoOS(os);
        os.RefreshTheme(); // 同步派生色 topBarColor / highlightColor / moduleColorSolid
    }
}

public static class ColorNameMapper
{
    private static readonly Dictionary<string, Color> _colorMap;

    static ColorNameMapper()
    {
        _colorMap = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase);
        var colorType = typeof(Color);
        // 获取所有静态属性（如 Red, Blue, White ...）
        foreach (var prop in colorType.GetProperties(BindingFlags.Public | BindingFlags.Static))
        {
            if (prop.PropertyType == typeof(Color) && prop.CanRead)
            {
                var color = (Color)prop.GetValue(null);
                _colorMap[prop.Name] = color;
            }
        }
        // 注意：有些颜色是字段（如 TransparentBlack），也一并添加
        foreach (var field in colorType.GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (field.FieldType == typeof(Color))
            {
                var color = (Color)field.GetValue(null);
                _colorMap[field.Name] = color;
            }
        }
    }

    public static bool TryGetColor(string name, out Color color)
    {
        return _colorMap.TryGetValue(name, out color);
    }
}