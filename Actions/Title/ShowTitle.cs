using Hacknet;
using KernelExtensions.Managers;
using KernelExtensions.Utilities;
using Microsoft.Xna.Framework;
using Pathfinder.Action;
using Pathfinder.Util;

namespace KernelExtensions.Actions.Title
{
    /// <summary>
    /// 显示标题横幅（dev1 标题系统移植版）。
    /// XML 用法：
    ///   &lt;ShowTitle title="标题" body="正文（支持 \n）" time="5" type="info|warning"
    ///              color="#FF9900" icon="Images/Info.png" iconbg="Images/InfoBG.png" /&gt;
    /// type 决定默认强调色（info=蓝 / warning=黄）；color 可覆盖（CustomColor：Hex/名称/CC 预设/动态，
    /// NONE/空=用 type 默认色）；icon/iconbg 可配（相对扩展根，默认 Images/Info.png）。
    /// </summary>
    public class ShowTitle : DelayablePathfinderAction
    {
        private static readonly Color INFO_BLUE = new(100, 180, 255);
        private static readonly Color WARNING_YELLOW = new(255, 210, 0);

        [XMLStorage] public string title = "";
        [XMLStorage] public string body = "";
        [XMLStorage] public float time = 5f;
        [XMLStorage] public string type = "info";   // info|warning（默认色预设）
        [XMLStorage] public string color = "";      // CustomColor 覆盖；NONE/空=type 默认色
        [XMLStorage] public string icon = "Images/Info.png";
        [XMLStorage] public string iconbg = "Images/InfoBG.png";

        public override void Trigger(OS os)
        {
            // type 兜底：未知值按 info + Warn
            bool isWarning = type.Equals("warning", StringComparison.OrdinalIgnoreCase);
            if (!isWarning && !type.Equals("info", StringComparison.OrdinalIgnoreCase))
                KELog.Warn($"[ShowTitle] unknown type '{type}', using info");

            // 默认色按 type；color 覆盖（NONE/空=默认，走 CustomColorManager 动态色入口支持 CC）
            Color defaultColor = isWarning ? WARNING_YELLOW : INFO_BLUE;
            Color accent = CustomColorManager.GetDynamicColor(color, defaultColor);

            // 图标路径（NONE/空=默认）
            string iconPath = ConfigValue.IsNone(icon) ? "Images/Info.png" : icon;
            string iconBgPath = ConfigValue.IsNone(iconbg) ? "Images/InfoBG.png" : iconbg;

            TitleBannerHooks.IconPath = iconPath;
            TitleBannerHooks.IconBgPath = iconBgPath;
            TitleBannerHooks.Show(title, body.Replace(" \\n ", "\n"), time, accent);
        }
    }
}
