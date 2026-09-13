using Microsoft.Xna.Framework.Graphics;
using System.Collections.Concurrent;
using System.Text;

namespace KernelExtensions.Utilities;

/// <summary>
/// 文本渲染辅助：按字体实际字符集清洗。
///
/// 背景：Hacknet 官方按语言加载字体——<c>LocaleActivator.ActivateLocale</c> →
/// <c>LocaleFontLoader.LoadFontConfigForLocale</c> 读取 <c>Locales/{locale}/Fonts/{locale}_FontXX</c>，
/// 并赋给 <c>GuiData.smallfont / tinyfont / font(=bigFont) / detailfont</c>，这些字体含**该语言**的字形
/// （zh-cn / ja-jp / ko-kr / ru-ru / de-de / fr-be / es-ar / tr-tr …）。
/// 唯独 <c>GuiData.titlefont</c> 是 <c>Game1.cs</c> 里硬编码的 "Kremlin"，官方未提供任何本地化版本。
///
/// 而 FNA 的 <c>SpriteFont.MeasureString</c> 与 <c>SpriteBatch.DrawString</c> 在遇到字符集外的字符、
/// 且字体未设 <c>DefaultCharacter</c> 时会抛 <c>ArgumentException</c>
/// （SpriteFont.cs:228-238、SpriteBatch.cs:748-762）。
///
/// 因此绘制前需要把"该字体确实画不出"的字符替换掉。判断依据用 <c>font.Characters</c>：
/// <list type="bullet">
/// <item>官方本地化字体 → 含本语言字形 → 原样保留，正常显示</item>
/// <item>Kremlin 等未本地化的字体 → 只含 ASCII → 非 ASCII 字符替换为 '?'</item>
/// </list>
/// 该判断对**任意语言**成立，不依赖硬编码白名单，也不依赖具体字体实例
/// （字体被模组替换时依然按新字体的字符集工作）。
///
/// 注意：必须在 <c>MeasureString</c> / <c>DrawString</c> **之前**调用，否则测量阶段就会抛异常。
/// </summary>
public static class TextHelper
{
    // 字体 → 字符集；ConcurrentDictionary 保证绘制等多线程场景下的安全
    private static readonly ConcurrentDictionary<SpriteFont, HashSet<char>> CharSetCache = new();

    /// <summary>
    /// 把该字体无法渲染的字符替换为 '?'（换行符始终保留）。
    /// </summary>
    /// <param name="font">将用于测量的字体（以其字符集为准）</param>
    /// <param name="input">原文本</param>
    public static string CleanStringForFont(SpriteFont font, string input)
    {
        if (string.IsNullOrEmpty(input) || font == null) return input;

        var charSet = CharSetCache.GetOrAdd(font, f => new HashSet<char>(f.Characters));

        var sb = new StringBuilder(input.Length);
        foreach (char c in input)
        {
            if (c == '\n' || c == '\r' || charSet.Contains(c)) sb.Append(c);
            else sb.Append('?');
        }
        return sb.ToString();
    }
}
