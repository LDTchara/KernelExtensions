using Hacknet;
using Hacknet.Gui;
using KernelExtensions.Utility;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace KernelExtensions.Modules
{
    /// <summary>
    /// KE 版飞机高度计（本地化方案 G）。
    /// 复制原版 AircraftAltitudeIndicator.RenderAltitudeIndicator 的渲染逻辑，但 9 处文字
    /// 改为 KELoc（FLIGHT_ALTITUDE_* key，随 KE-Locales.xml 可控）；图标从原版资源路径
    /// 自加载（原版字段 private 无法复用）；GetFlashRateFromTimer 直接用原版 public 方法。
    /// 调用点：OverlayPatch（覆盖层）+ FlightDaemon.Draw（daemon 界面）。
    /// </summary>
    public static class KEAltitudeIndicator
    {
        private static Texture2D WarningIcon;
        private static Texture2D PlaneIcon;

        public static void Init(ContentManager content)
        {
            WarningIcon = content.Load<Texture2D>("Sprites/Icons/CautionIcon");
            PlaneIcon = content.Load<Texture2D>("DLC/Sprites/Airplane");
        }

        public static void RenderAltitudeIndicator(Rectangle dest, SpriteBatch sb, int currentAltitude, bool IsInCriticalDescenet, bool IconFlashIsVisible, int maxAltitude = 50000, int upperReccomended = 40000, int lowerReccomended = 30000, int warningArea = 14000, int criticalFailureArea = 3000)
        {
            if (WarningIcon == null)
            {
                Init(OS.currentInstance.content);
            }
            bool flag = currentAltitude <= 0;
            if (flag)
            {
                currentAltitude = maxAltitude;
            }
            int num = Math.Min(dest.Width, 100);
            Rectangle rectangle = new(dest.X + dest.Width - num, dest.Y, num, dest.Height);
            int num2 = 200;
            Rectangle dest2 = new(dest.X + dest.Width - num2, rectangle.Y, num2, 21);
            Color color = IsInCriticalDescenet ? Utils.AddativeRed : OS.currentInstance.highlightColor;
            Rectangle rectangle2 = rectangle;
            rectangle2.Width = num / 2;
            rectangle2.X = dest.X + dest.Width - rectangle2.Width;
            sb.Draw(Utils.gradientLeftRight, rectangle2, color * 0.2f);
            int heightForAltitude = GetHeightForAltitude(currentAltitude, maxAltitude, rectangle2);
            rectangle.Y += heightForAltitude;
            rectangle.Height -= heightForAltitude;
            sb.Draw(Utils.gradientLeftRight, rectangle, color);
            DrawIndicatorForAltitude(dest2, maxAltitude, KELoc.Loc("FLIGHT_ALTITUDE_MAXIMUM", "Maximum Altitude"), maxAltitude, rectangle2, sb, color, true, true);
            DrawIndicatorForAltitude(dest2, upperReccomended, KELoc.Loc("FLIGHT_ALTITUDE_MAXIMUM_CRUISING", "Maximum Cruising Altitude"), maxAltitude, rectangle2, sb, color, false, false);
            DrawIndicatorForAltitude(dest2, lowerReccomended, KELoc.Loc("FLIGHT_ALTITUDE_MINIMUM_CRUISING", "Minimum Cruising Altitude"), maxAltitude, rectangle2, sb, color, false, false);
            DrawIndicatorForAltitude(dest2, warningArea, KELoc.Loc("FLIGHT_ALTITUDE_UNSAFE_MARGIN", "Unsafe Altitude Margin"), maxAltitude, rectangle2, sb, color, false, false);
            dest2.Height *= 2;
            DrawIndicatorForAltitude(dest2, criticalFailureArea, KELoc.Loc("FLIGHT_ALTITUDE_CRITICAL_FAILURE_REGION", "Critical Failure Region") + "\n- " + KELoc.Loc("FLIGHT_ALTITUDE_POINT_OF_NO_RETURN", "POINT OF NO RETURN") + " -", maxAltitude, rectangle2, sb, Utils.makeColorAddative(color), true, false);
            dest2 = new(dest2.X - 20, dest2.Y, dest2.Width + 20, dest2.Height + 10);
            DrawIndicatorForAltitude(dest2, currentAltitude, flag ? (KELoc.Loc("FLIGHT_ALTITUDE_CRITICAL_ERROR", "CRITICAL ERROR") + "\n" + KELoc.Loc("FLIGHT_ALTITUDE_SIGNAL_LOST", "SIGNAL LOST")) : (KELoc.Loc("FLIGHT_ALTITUDE_CURRENT", "Current Altitude") + "\n" + string.Format("{0}ft", currentAltitude)), maxAltitude, rectangle2, sb, color, true, false);
            int num3 = dest2.Height - 4;
            Rectangle rectangle3 = new(dest2.X - num3 - 4, dest2.Y + GetHeightForAltitude(currentAltitude, maxAltitude, rectangle2), num3, num3);
            Rectangle destinationRectangle = new(dest2.X - num3 - 4, rectangle3.Y, num3 + 4, dest2.Height);
            if (currentAltitude < lowerReccomended)
            {
                Rectangle dest3 = new(destinationRectangle.X, destinationRectangle.Y, destinationRectangle.Width + dest2.Width, destinationRectangle.Height);
                PatternDrawer.draw(dest3, 0.2f, Color.Transparent, Color.Red * 0.2f, sb);
            }
            sb.Draw(Utils.white, destinationRectangle, Color.Black * 0.4f);
            destinationRectangle.Height = 1;
            sb.Draw(Utils.white, destinationRectangle, color);
            rectangle3.Y += 2;
            rectangle3.X += 2;
            rectangle3 = Utils.InsetRectangle(rectangle3, 4);
            if (IsInCriticalDescenet)
            {
                sb.Draw(WarningIcon, rectangle3, Color.Red * (IconFlashIsVisible ? 1f : 0.3f));
            }
            else
            {
                sb.Draw(PlaneIcon, rectangle3, color);
            }
        }

        private static int GetHeightForAltitude(int altitude, int maxAltitude, Rectangle glowBar)
        {
            float num = (float)altitude / (float)maxAltitude;
            return (int)((float)glowBar.Height * (1f - num));
        }

        private static void DrawIndicatorForAltitude(Rectangle dest, int altitude, string ElementTitle, int totalAltitude, Rectangle totalBar, SpriteBatch sb, Color c, bool LineAtTop = false, bool useGradientBacking = false)
        {
            dest.Y = totalBar.Y + GetHeightForAltitude(altitude, totalAltitude, totalBar);
            if (LineAtTop)
            {
                dest.Y++;
                dest.Height--;
            }
            sb.Draw(useGradientBacking ? Utils.gradientLeftRight : Utils.white, dest, null, Color.Black * (useGradientBacking ? 1f : 0.5f), 0f, Vector2.Zero, SpriteEffects.FlipHorizontally, 0.4f);
            TextItem.doFontLabelToSize(dest, ElementTitle, GuiData.font, c, true, true);
            if (LineAtTop)
            {
                dest.Y--;
                dest.Height = 1;
            }
            else
            {
                dest.Y += dest.Height - 2;
                dest.Height = 1;
            }
            sb.Draw(Utils.white, dest, c);
        }
    }
}
