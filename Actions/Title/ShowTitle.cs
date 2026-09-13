using Hacknet;
using KernelExtensions.Utilities;
using Microsoft.Xna.Framework;
using Pathfinder.Action;
using Pathfinder.Util;

namespace KernelExtensions.Actions.Title
{
    /// <summary>
    /// 显示标题横幅（重制自原版 IncomingConnectionOverlay「本机被外部连接」覆盖层）。
    ///
    /// XML 用法（正文写在元素内容里，与 StartScreenBleedEffectWCC 一致，可直接多行）：
    ///   &lt;ShowTitle title="警告" preset="warning" time="5" icon="Images/Warn.png"&gt;
    ///   第一行正文
    ///   第二行正文
    ///   &lt;/ShowTitle&gt;
    ///
    /// 说明：
    ///   · 正文 = **元素内容**（真正的换行符；首尾空行与公共缩进会自动去除，可自由排版）
    ///   · preset：info（默认，强调色取 os.defaultHighlightColor 主题高亮基色）
    ///              warning（强调色取 os.warningColor 主题警告色）
    ///   · color：CustomColor 覆盖（Hex/名称/CC 预设/动态），NONE/空 = 用 preset 的主题色
    ///   · icon：图标路径（相对扩展根，默认 Images/Info.png；NONE/空 = 默认）
    ///   · ⚠️ title **仅支持 ASCII**：标题用游戏标题字体（Kremlin），官方未提供任何语言的
    ///     本地化版本，非 ASCII 字符（中文/日文/俄文…）会显示为 `?`。正文不受此限制。
    /// </summary>
    public class ShowTitle : DelayablePathfinderAction
    {
        [XMLStorage] public string title = "";
        /// <summary>正文（元素内容，支持多行）。</summary>
        [XMLStorage(IsContent = true)] public string body = "";
        [XMLStorage] public float time = 5f;
        /// <summary>info | warning（强调色预设，分别取主题 defaultHighlightColor / warningColor）。</summary>
        [XMLStorage] public string preset = "info";
        /// <summary>CustomColor 覆盖；NONE/空 = 用 preset 的主题色。</summary>
        [XMLStorage] public string color = "";
        [XMLStorage] public string icon = "Images/Info.png";

        public override void Trigger(OS os)
        {
            // preset 兜底：未知值按 info + Warn
            bool isWarning = preset.Equals("warning", StringComparison.OrdinalIgnoreCase);
            if (!isWarning && !preset.Equals("info", StringComparison.OrdinalIgnoreCase))
                KELog.Warn($"[ShowTitle] unknown preset '{preset}', using info");

            // 强调色跟随主题：info = defaultHighlightColor（基色，不被 warningFlash 插值污染）
            //                  warning = warningColor
            Color defaultColor = isWarning ? os.warningColor : os.defaultHighlightColor;

            // 标题仅 ASCII：titlefont(Kremlin) 无本地化版，含非 ASCII 会被清洗为 '?'——提前提示作者
            if (!IsAscii(title))
                KELog.Warn("[ShowTitle] title contains non-ASCII characters; the title font only supports ASCII, they will render as '?'. Put such text in the body instead.");

            // 图标路径（NONE/空=默认）
            string iconPath = ConfigValue.IsNone(icon) ? "Images/Info.png" : icon;

            TitleBannerHooks.Show(title, NormalizeBody(body), time, color, defaultColor, iconPath);
        }

        /// <summary>规范化元素内容：统一换行、去首尾空行、去各行公共缩进（允许作者自由排版）。</summary>
        private static string NormalizeBody(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";

            var lines = new List<string>(
                raw.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'));

            // 去首尾空行
            while (lines.Count > 0 && lines[0].Trim().Length == 0) lines.RemoveAt(0);
            while (lines.Count > 0 && lines[lines.Count - 1].Trim().Length == 0) lines.RemoveAt(lines.Count - 1);
            if (lines.Count == 0) return "";

            // 去公共缩进
            int indent = int.MaxValue;
            foreach (var l in lines)
            {
                if (l.Trim().Length == 0) continue;
                int cur = l.Length - l.TrimStart().Length;
                if (cur < indent) indent = cur;
            }
            if (indent > 0 && indent != int.MaxValue)
            {
                for (int i = 0; i < lines.Count; i++)
                {
                    var l = lines[i];
                    lines[i] = l.Length >= indent ? l.Substring(indent) : l.TrimStart();
                }
            }

            return string.Join("\n", lines);
        }

        /// <summary>是否全部为 ASCII（标题字体限制检测）。</summary>
        private static bool IsAscii(string s)
        {
            if (string.IsNullOrEmpty(s)) return true;
            foreach (char c in s)
            {
                if (c > 127) return false;
            }
            return true;
        }
    }
}
