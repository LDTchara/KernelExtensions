using Microsoft.Xna.Framework.Graphics;
using System.Text;

namespace KernelExtensions.Utilities;

/// <summary>
/// 文本渲染辅助。
///
/// 背景：原版 <c>Utils.CleanStringToRenderable</c> 使用 ASCII 白名单，会把中文等非 ASCII
/// 字符一律替换为 '?'（演讲阶段未调用故中文正常，报幕/横幅调用后中文丢失）。
///
/// 分流策略：
/// <list type="bullet">
/// <item>已加载 <b>HacknetFontReplace</b>（HFR，FontStashSharp 动态字体）时——HFR 通过 Harmony
/// Prefix 接管 <c>SpriteBatch.DrawString</c> / <c>MeasureString</c>，任意字符（含中文）都能按需
/// 生成字形渲染，<b>不做清洗</b>，原样交给绘制。</item>
/// <item>未加载 HFR 时——原版 SpriteFont 只能渲染其烘焙字符集内的字形，仍按<b>该字体实际字符集</b>
/// 清洗为 '?'，让作者直观看到"这个字打不出来"。</item>
/// </list>
/// </summary>
public static class TextHelper
{
    // 字体字符集缓存（报幕等每帧绘制路径需要，避免重复线性扫描 SpriteFont.Characters）
    private static readonly Dictionary<SpriteFont, HashSet<char>> CharSetCache = new();

    private static bool? fontReplaceLoaded;

    /// <summary>是否已加载 HacknetFontReplace（动态字体接管绘制，任意字符可渲染）。</summary>
    private static bool FontReplaceLoaded
    {
        get
        {
            if (fontReplaceLoaded == null)
            {
                try
                {
                    fontReplaceLoaded = AppDomain.CurrentDomain.GetAssemblies()
                        .Any(a => a.GetName().Name == "HacknetFontReplace");
                }
                catch { fontReplaceLoaded = false; }
            }
            return fontReplaceLoaded.Value;
        }
    }

    /// <summary>
    /// 绘制前清洗文本：装了 HFR 则原样返回（动态字体可渲染中文）；
    /// 否则把当前字体不支持的字符替换为 '?'（换行符始终保留）。
    /// </summary>
    /// <param name="font">将要用于绘制的字体（以其字符集为准）</param>
    /// <param name="input">原文本</param>
    public static string CleanForDisplay(SpriteFont font, string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        if (FontReplaceLoaded) return input;
        return CleanStringForFont(font, input);
    }

    /// <summary>按字体实际支持的字符集清洗：字体不含的字符替换为 '?'（换行符保留）。</summary>
    private static string CleanStringForFont(SpriteFont font, string input)
    {
        if (string.IsNullOrEmpty(input) || font == null) return input;

        if (!CharSetCache.TryGetValue(font, out var charSet))
        {
            charSet = new HashSet<char>(font.Characters);
            CharSetCache[font] = charSet;
        }

        var sb = new StringBuilder(input.Length);
        foreach (char c in input)
        {
            if (c == '\n' || c == '\r' || charSet.Contains(c)) sb.Append(c);
            else sb.Append('?');
        }
        return sb.ToString();
    }
}
