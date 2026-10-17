using Hacknet;
using KernelExtensions.Managers;
using KernelExtensions.Patches;
using KernelExtensions.Utilities;
using Microsoft.Xna.Framework;
using Pathfinder.Action;
using Pathfinder.Util;
using Pathfinder.Util.XML;

namespace KernelExtensions.Actions.Misc
{
    /// <summary>
    /// 管理 WCC（WithCustomColor）ScreenBleed 效果的计时、渲染和清理。
    /// 替代原版 StartScreenBleedEffect，支持自定义颜色和文字颜色。
    /// 支持 CancelScreenBleedEffect Action 停止。
    /// 用法：
    /// <StartScreenBleedEffectWCC
    ///     AlertTitle="WARNING"
    ///     TotalDurationSeconds="5.0"
    ///     BackgroundColor="#FF2020"
    ///     TextBackgroundColor="#880000"
    ///     CompleteAction="Actions/end.xml">
    /// Line 1 text
    /// Line 2 text
    /// Line 3 text
    /// </StartScreenBleedEffectWCC>
    /// 颜色值支持 CustomColor 预设、Hex 与 XNA 命名色 (Monochrome)。
    /// 8 位 Hex 走统一语义 #AARRGGBB（Alpha 在首位，只控制不透明度、不缩放 RGB）。
    /// 早期开发版本曾在此处额外做 Alpha 乘性调制（new Color(r,g,b) * (a/255)），使 RGB 随 Alpha 变暗；
    /// 已并入统一解析链 —— 该写法从未随任何发行版发布，且与"各模块共用同一套颜色解析"相矛盾。
    /// 影响面仅 8 位 Hex（如 #80FF0000 由暗红变亮红）；3/6 位 Hex、命名色、数值式逐字节不变。
    /// 支持动态颜色 (RGB, RGB:0.5:0.3) 等。
    /// </summary>
    public class StartScreenBleedEffectWCCAction : KEAction
    {
        [XMLStorage] public string AlertTitle = "EMERGENCY";
        [XMLStorage] public string CompleteAction;
        [XMLStorage] public float TotalDurationSeconds = 200f;
        [XMLStorage] public string BackgroundColor;
        [XMLStorage] public string TextBackgroundColor;

        public override void LoadFromXml(ElementInfo info)
        {
            base.LoadFromXml(info);
            if (!string.IsNullOrWhiteSpace(info.Content))
                ContentLines = ComputerLoader.filter(info.Content);
        }

        public override void Trigger(OS os)
        {
            try
            {
                Color bgColor = ResolveColor(BackgroundColor, new Color(120, 0, 0));
                Color textBgColor = ResolveColor(TextBackgroundColor, new Color(105, 0, 0, 200));

                var lines = string.IsNullOrEmpty(ContentLines)
                    ? new[] { "", "", "" }
                    : SplitLines(ContentLines);

                ScreenBleedWCCManager.Start(os, TotalDurationSeconds,
                    bgColor, textBgColor,
                    AlertTitle, lines[0], lines[1], lines[2],
                    CompleteAction, BackgroundColor, TextBackgroundColor);
            }
            catch (Exception ex)
            {
                KELog.ErrorT("[WCC] Trigger failed: " + ex.Message);
            }
        }

        private static string[] SplitLines(string text)
        {
            var parts = text.Split(Utils.robustNewlineDelim, StringSplitOptions.None);
            var list = new List<string>(parts);
            while (list.Count < 3) list.Add("");
            return list.ToArray();
        }

        private string ContentLines;

        private static Color ResolveColor(string raw, Color fallback)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return fallback;

            var dynConfig = CustomColorManager.ParseColorString(raw);
            if (dynConfig != null)
                return CustomColorManager.CalcColor(dynConfig, OS.currentElapsedTime);

            // 静态色统一走 ColorHelper：#RGB / #RRGGBB / #AARRGGBB、数值 RGB/RGBA、XNA 命名色
            return ColorHelper.ParseStaticColor(raw, fallback);
        }
    }
}
