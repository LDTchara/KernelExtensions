using Hacknet;
using Hacknet.Extensions;
using Hacknet.Gui;
using HarmonyLib;
using KernelExtensions.Utilities;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;

using KernelExtensions.Managers;
namespace KernelExtensions.Actions.Title
{
    /// <summary>
    /// 标题横幅（dev1 标题系统合并版）——原 CustomInfoTitle/CustomWarningTitle 结构相同
    /// （仅默认色/时长差异），合并为单类。强调色支持 CustomColor（Hex/名称/CC 预设/动态，
    /// 经 CustomColorManager.GetDynamicColor），图标路径可配（默认 Images/Info.png）。
    /// </summary>
    internal class TitleBanner
    {
        /// <summary>强调色（条纹/标题/图标 tint），默认信息蓝。每帧经 CustomColorManager 按 AccentColorKey 刷新（CC 动态色持续变化）。</summary>
        public Color AccentColor = new(100, 180, 255);

        /// <summary>CC 颜色关键字/Hex/名称；NONE/空=用 DefaultAccentColor（type 预设色）。</summary>
        public string AccentColorKey = "";

        /// <summary>无 color 覆盖时的默认强调色（由 ShowTitle 的 type 决定）。</summary>
        public Color DefaultAccentColor = new(100, 180, 255);

        public float Duration = 5f;
        public bool IsActive { get; private set; }

        public string TitleText { get; set; } = "!!!ATTENTION!!!";
        public string BodyText { get; set; } = "";

        private float timeElapsed;
        private Texture2D infoIcon;
        private Texture2D infoBg;
        private SoundEffect sound1;
        private SoundEffect sound2;

        /// <summary>加载纹理（图标路径相对扩展根，可配）和音效。OS 就绪后调用。</summary>
        public void LoadContent(OS osInstance, string iconPath, string iconBgPath)
        {
            string folder = ExtensionLoader.ActiveExtensionInfo.FolderPath;
            var gd = Game1.getSingleton().GraphicsDevice;

            // 图标缺失不崩（Warn 提示，横幅仍显示但无图标）
            try
            {
                using (var s = File.OpenRead(Path.Combine(folder, iconPath))) infoIcon = Texture2D.FromStream(gd, s);
                using (var s = File.OpenRead(Path.Combine(folder, iconBgPath))) infoBg = Texture2D.FromStream(gd, s);
            }
            catch (Exception ex)
            {
                KELog.Warn($"[TitleBanner] icon load failed ({iconPath}/{iconBgPath}): {ex.Message}");
            }

            sound1 = osInstance.content.Load<SoundEffect>("SFX/DoomShock");
            sound2 = osInstance.content.Load<SoundEffect>("SFX/BrightFlash");
        }

        public void Activate()
        {
            IsActive = true;
            timeElapsed = 0f;
            sound1?.Play();
            sound2?.Play();
        }

        public void Update(float dt)
        {
            if (!IsActive) return;
            timeElapsed += dt;
            if (timeElapsed > Duration) IsActive = false;
        }

        public void Draw(Rectangle dest, SpriteBatch sb)
        {
            if (!IsActive) return;

            // CC 动态色每帧刷新——AccentColorKey 为动态关键字（彩虹/渐变/预设）时
            // 按 OS.currentElapsedTime 持续变化；Hex/名称/NONE 则每帧结果不变（2026-08-25 修复定格）
            AccentColor = CustomColorManager.GetDynamicColor(AccentColorKey, DefaultAccentColor);

            float t = timeElapsed;
            float fadeInDuration = 0.2f;
            float fadeOutDuration = 0.5f;
            float alpha = 1f;
            int barHeight = 230;

            if (t < fadeInDuration)
            {
                alpha = t / fadeInDuration;
                barHeight = (int)(130f * (t / fadeInDuration));
            }
            else if (t > Duration - fadeOutDuration)
            {
                float fp = (Duration - t) / fadeOutDuration;
                alpha = fp;
                barHeight = (int)(130f * fp);
            }

            var barRect = new Rectangle(dest.X, dest.Y + dest.Height / 2 - barHeight / 2, dest.Width, barHeight);
            sb.Draw(Utils.white, barRect, Color.Black * 0.9f * alpha);

            int stripeH = 15;
            var topStripe = new Rectangle(barRect.X, barRect.Y, barRect.Width, stripeH);
            var botStripe = new Rectangle(barRect.X, barRect.Bottom - stripeH, barRect.Width, stripeH);
            PatternDrawer.draw(topStripe, 1f, Color.Transparent, AccentColor * alpha, sb, PatternDrawer.warningStripe);
            PatternDrawer.draw(botStripe, 1f, Color.Transparent, AccentColor * alpha, sb, PatternDrawer.warningStripe);

            int margin = 14;
            int maxW = barRect.Width - margin * 2;
            string wrappedBody = Utils.SuperSmartTwimForWidth(BodyText, maxW, GuiData.font);
            string[] bodyLines = wrappedBody.Split('\n');
            int lh = GuiData.font.LineSpacing;
            int bodyH = bodyLines.Length * lh;

            int titleH = GuiData.titlefont.LineSpacing;
            float blockCenterY = barRect.Y + (barHeight - titleH - 8 - bodyH) / 2f;

            Vector2 ts = GuiData.titlefont.MeasureString(TitleText);
            float titleX = barRect.X + (barRect.Width - ts.X) / 2f;
            TextItem.doFontLabel(new Vector2(titleX, blockCenterY), TitleText, GuiData.titlefont, AccentColor * alpha);

            float bodyY = blockCenterY + titleH + 8;
            for (int i = 0; i < bodyLines.Length; i++)
            {
                Vector2 ls = GuiData.font.MeasureString(bodyLines[i]);
                float lx = barRect.X + (barRect.Width - ls.X) / 2f;
                if (lx < barRect.X + margin) lx = barRect.X + margin;
                TextItem.doFontLabel(new Vector2(lx, bodyY + i * lh), bodyLines[i], GuiData.font, Color.LightGray * alpha);
            }

            const int FIXED_ICON_SIZE = 100;
            int iconSz = FIXED_ICON_SIZE;
            int iconGap = 10;
            int iconX2 = barRect.X + margin - iconSz - iconGap;
            if (iconX2 < barRect.X + 4) iconX2 = barRect.X + 4;
            int iconY2 = (int)(blockCenterY + (titleH + 8 + bodyH - iconSz) / 2f);

            var iconRect = new Rectangle(iconX2, iconY2, iconSz, iconSz);
            if (infoBg != null) sb.Draw(infoBg, iconRect, Color.White * alpha);
            if (infoIcon != null)
            {
                int inset = 3;
                var iconInner = new Rectangle(iconRect.X + inset, iconRect.Y + inset, iconRect.Width - inset * 2, iconRect.Height - inset * 2);
                sb.Draw(infoIcon, iconInner, Color.Lerp(AccentColor, Color.White, 0.3f) * alpha);
            }
        }
    }

    /// <summary>Harmony 钩子：OS.LoadContent 初始化单例、OS.Update/Draw 驱动横幅。</summary>
    [HarmonyPatch]
    internal static class TitleBannerHooks
    {
        internal static TitleBanner Instance;
        internal static string IconPath = "Images/Info.png";
        internal static string IconBgPath = "Images/InfoBG.png";
        private static bool _drawFailedWarned;

        /// <summary>弹出横幅。colorKey=CC 颜色关键字/Hex/名称（NONE/空=用 defaultColor）；强调色每帧刷新，动态色不定格。</summary>
        internal static void Show(string title, string body, float duration, string colorKey, Color defaultColor)
        {
            if (Instance == null) return;
            Instance.TitleText = Utils.CleanStringToRenderable(title);
            Instance.BodyText = Utils.CleanStringToRenderable(body);
            Instance.Duration = duration;
            Instance.AccentColorKey = colorKey ?? "";
            Instance.DefaultAccentColor = defaultColor;
            Instance.Activate();
        }

        [HarmonyPatch(typeof(OS), nameof(OS.LoadContent))]
        [HarmonyPostfix]
        internal static void OnOSLoadContent(OS __instance)
        {
            if (Instance != null) return;
            var banner = new TitleBanner();
            banner.LoadContent(__instance, IconPath, IconBgPath);
            Instance = banner;
            KELog.Debug("[TitleBanner] initialized");
        }

        [HarmonyPatch(typeof(OS), nameof(OS.Update))]
        [HarmonyPostfix]
        internal static void OnOSUpdate(OS __instance, GameTime gameTime, bool otherScreenHasFocus, bool coveredByOtherScreen)
        {
            if (Instance == null) return;
            Instance.Update((float)gameTime.ElapsedGameTime.TotalSeconds);
        }

        [HarmonyPatch(typeof(OS), nameof(OS.Draw))]
        [HarmonyPostfix]
        internal static void OnOSDraw(OS __instance, GameTime gameTime)
        {
            if (Instance == null || !Instance.IsActive) return;
            bool began = false;
            bool drawFailed = false;
            try
            {
                // OS.Draw() 内部已结束 SpriteBatch（两次 Begin/End 配对），Postfix 需自己 Begin/End
                GuiData.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
                began = true;
                var fullscreen = new Rectangle(0, 0,
                    __instance.ScreenManager.GraphicsDevice.Viewport.Width,
                    __instance.ScreenManager.GraphicsDevice.Viewport.Height);
                Instance.Draw(fullscreen, GuiData.spriteBatch);
            }
            catch (Exception ex)
            {
                drawFailed = true;
                // 节流：持续失败只警告一次，直到某帧成功才复位
                if (!_drawFailedWarned)
                {
                    _drawFailedWarned = true;
                    KELog.Warn($"[TitleBanner] draw failed: {ex.Message}");
                }
            }
            finally
            {
                // 即使 Draw 内部异常也必须 End——否则 SpriteBatch 残留 Begin 状态，
                // 下一帧 OS.Draw 的 GuiData.startDraw() 会抛 "Begin before End"（2026-08-25 修复）
                if (began)
                {
                    try { GuiData.spriteBatch.End(); } catch { }
                }
                if (!drawFailed) _drawFailedWarned = false;
            }
        }
    }
}
