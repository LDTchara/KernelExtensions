using Microsoft.Xna.Framework.Graphics;
using System.Text;

namespace KernelExtensions.Utilities;

/// <summary>
/// 文本渲染辅助。
/// </summary>
public static class TextHelper
{
    // 字体字符集缓存（报幕等每帧绘制路径需要，避免每次线性扫描 SpriteFont.Characters）
    private static readonly Dictionary<SpriteFont, HashSet<char>> CharSetCache = new();

    /// <summary>
    /// 按字体实际支持的字符集清洗文本：该字体不含的字符替换为 '?'。
    ///
    /// 替代原版 <c>Utils.CleanStringToRenderable</c>——后者使用 ASCII 白名单，
    /// 会把中文/日文等非 ASCII 字符全部变成 '?'（演讲阶段未清洗故中文正常，报幕与横幅清洗后中文丢失）。
    /// 换行符始终保留。FNA 的 SpriteFont 遇未知字符不会抛异常（跳过或用 DefaultCharacter），
    /// 此清洗仅用于避免渲染出无意义的占位字形。
    /// </summary>
    /// <param name="font">将要用于绘制的字体（以其字符集为准）</param>
    /// <param name="input">原文本</param>
    public static string CleanStringForFont(SpriteFont font, string input)
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
