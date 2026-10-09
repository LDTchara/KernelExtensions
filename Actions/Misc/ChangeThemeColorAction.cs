using Hacknet;
using KernelExtensions.Managers;
using KernelExtensions.Utilities;
using Microsoft.Xna.Framework;
using Pathfinder.Util;

namespace KernelExtensions.Actions.Misc
{
    /// <summary>
    /// 临时修改当前 OS 的主题颜色字段——**只灌颜色，不切换主题**（布局与背景保持原样）。
    ///
    /// XML 用法：
    ///   &lt;ChangeThemeColor Locate="defaultHighlightColor:Red,warningColor:#FF0000" /&gt;
    ///
    /// · Locate 为逗号分隔的「字段名:颜色值」列表；字段名须是主题的颜色字段
    ///   （如 defaultHighlightColor / warningColor / moduleColorSolidDefault / terminalTextColor 等）。
    /// · 颜色值走统一解析（<see cref="ColorHelper.ParseStaticColor"/>）：
    ///   XNA 命名色（如 Red，大小写不敏感）、#RGB、#RRGGBB、#AARRGGBB、R,G,B[,A]。
    /// · 字段值写 NONE / 空 = 该字段保持不变。
    /// · 未知字段名、格式不符的键值对 → KELog.Warn 并跳过，不影响其余字段。
    ///
    /// 撤销：本 Action 只改 OS 上的当前值，不写存档、不改玩家已加载的主题对象；
    /// 玩家**切换主题**后 OS 字段被主题重新写入，本次修改即自然失效。
    /// </summary>
    public class ChangeThemeColorAction : KEAction
    {
        /// <summary>逗号分隔的「字段名:颜色值」列表，如 defaultHighlightColor:Red,warningColor:#FF0000。</summary>
        [XMLStorage] public string Locate = "";

        public override void Trigger(OS os)
        {
            if (string.IsNullOrWhiteSpace(Locate)) return;

            // 以当前 OS 的颜色字段打底新建一份主题副本，只覆盖本次指定的字段。
            // 刻意不复用 ThemeManager.LastLoadedCustomTheme：那是玩家已加载的主题对象，
            // 改它会污染后续切回该主题时的颜色（本 Action 的语义是"临时"）。
            var theme = new CustomTheme();
            foreach (var f in typeof(CustomTheme).GetFields())
            {
                var src = typeof(OS).GetField(f.Name);
                if (src != null) f.SetValue(theme, src.GetValue(os));
            }

            Type themeType = theme.GetType();
            int applied = 0;

            foreach (string pair in Locate.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] kv = pair.Split(new[] { ':' }, 2);
                if (kv.Length != 2)
                {
                    KELog.Warn($"[ChangeThemeColor] malformed pair '{pair}', expected 'field:colour'");
                    continue;
                }

                string key = kv[0].Trim();
                string value = kv[1].Trim();

                if (ConfigValue.IsNone(value)) continue;   // NONE / 空 = 不改该字段

                var field = themeType.GetField(key);
                if (field == null || field.FieldType != typeof(Color))
                {
                    KELog.Warn($"[ChangeThemeColor] unknown colour field '{key}'");
                    continue;
                }

                if (!LooksLikeColour(value))
                {
                    KELog.Warn($"[ChangeThemeColor] cannot parse colour '{value}' for '{key}', field left unchanged");
                    continue;
                }

                field.SetValue(theme, ColorHelper.ParseStaticColor(value, (Color)field.GetValue(theme)));
                applied++;
            }

            if (applied == 0) return;

            // 只把颜色灌入当前 OS：不切主题，保留现有布局与背景。
            theme.LoadIntoOS(os);
            os.RefreshTheme();   // 同步派生色 topBarColor / highlightColor / moduleColorSolid
        }

        /// <summary>粗判是否为本解析链认得的颜色写法（仅用于解析失败时给出 Warn）。</summary>
        private static bool LooksLikeColour(string value)
        {
            if (value.StartsWith("#", StringComparison.Ordinal)) return true;
            if (value.Contains(',')) return true;
            return ColorHelper.TryGetNamedColor(value, out _);
        }
    }
}
