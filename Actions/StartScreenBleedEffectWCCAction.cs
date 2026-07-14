using System;
using System.Globalization;
using Hacknet;
using KernelExtensions.Modules;
using KernelExtensions.Patches;
using KernelExtensions.Utility;
using Microsoft.Xna.Framework;
using Pathfinder.Action;
using Pathfinder.Util;
using Pathfinder.Util.XML;

namespace KernelExtensions.Actions
{
    /// <summary>
    /// 全屏 ScreenBleed 效果（支持 CustomColor）。
    /// 替代原版 StartScreenBleedEffect，背景色和文字背景色支持自定义颜色与预设。
    /// 兼容原版 CancelScreenBleedEffect Action 停止。
    /// 用法：
    /// <![CDATA[
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
    /// ]]>
    /// 颜色值支持 CustomColor 全部语法：Hex (#RRGGBB)、预设名 (Monochrome)、
    /// 动态色 (Rainbow, Rainbow:0.5:0.3)、CustomColor 预设 (RedWarn2) 等。
    /// 使用 9.36 灰度功能时，颜色饱和度越低灰度效果越强。
    /// </summary>
    public class StartScreenBleedEffectWCCAction : DelayablePathfinderAction
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
                Color bgColor = ResolveColor(os, BackgroundColor, new Color(120, 0, 0));
                Color textBgColor = ResolveColor(os, TextBackgroundColor, new Color(105, 0, 0, 200));

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
                KELog.Error("[WCC] Trigger failed: " + ex.Message);
            }
        }

        private static string[] SplitLines(string text)
        {
            var parts = text.Split(Utils.robustNewlineDelim, StringSplitOptions.None);
            var list = new System.Collections.Generic.List<string>(parts);
            while (list.Count < 3) list.Add("");
            return list.ToArray();
        }

        private string ContentLines;

        private static Color ResolveColor(OS os, string raw, Color fallback)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return fallback;

            var dynConfig = CustomColorPatch.ParseColorString(raw);
            if (dynConfig != null)
                return CustomColorPatch.CalcColor(dynConfig, OS.currentElapsedTime);

            if (raw.StartsWith("#"))
            {
                try
                {
                    string hex = raw.Substring(1);
                    if (hex.Length == 6)
                    {
                        int rgb = int.Parse(hex, NumberStyles.HexNumber);
                        return new Color((rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255);
                    }
                    if (hex.Length == 8)
                    {
                        int a = int.Parse(hex.Substring(0, 2), NumberStyles.HexNumber);
                        int rgb = int.Parse(hex.Substring(2), NumberStyles.HexNumber);
                        return new Color((rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255) * (a / 255f);
                    }
                }
                catch { }
            }
            return fallback;
        }
    }
}
