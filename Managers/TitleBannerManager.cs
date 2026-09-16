using Hacknet;
using Hacknet.Extensions;
using Hacknet.Gui;
using KernelExtensions.Utilities;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;

namespace KernelExtensions.Managers
{
    /// <summary>
    /// 标题横幅的**绘制组件**（ShowTitle 的可视部分）——原 CustomInfoTitle/CustomWarningTitle 结构相同
    /// （仅默认色/时长差异），合并为单类。由 <c>Patches/TitleBannerPatches</c> 驱动：
    /// OS.LoadContent 时创建单例，OS.Update 推进计时，OS.Draw 时调用 <see cref="Draw"/>。
    ///
    /// 强调色支持 CustomColor（CC 预设/动态，经 <c>CustomColorManager.GetDynamicColor</c>，每帧刷新不定格）；
    /// 图标支持内置（内嵌资源）/ 扩展路径 / 不显示三态，染色由 ShowTitle 的 IconTint 决定。
    /// </summary>
    internal class TitleBanner
    {
        /// <summary>icon="default" 的内部标记（与扩展路径区分；ShowTitle 与 Patch 共用）。</summary>
        internal const string DefaultIconMarker = "\u0001default";

        /// <summary>强调色（条纹/标题/图标 tint）。每帧经 CustomColorManager 按 AccentColorKey 刷新（CC 动态色持续变化）。</summary>
        public Color AccentColor = new(100, 180, 255);

        /// <summary>CC 颜色关键字/动态色；NONE/空=用 DefaultAccentColor（preset 主题色）。</summary>
        public string AccentColorKey = "";

        /// <summary>无 color 覆盖时的默认强调色（由 ShowTitle 的 preset 决定）。</summary>
        public Color DefaultAccentColor = new(100, 180, 255);

        public float Duration = 5f;
        public bool IsActive { get; private set; }

        public string TitleText { get; set; } = "!!!ATTENTION!!!";
        public string BodyText { get; set; } = "";

        private float timeElapsed;
        private Texture2D infoIcon;
        private bool tintIcon = true;          // 当前图标是否染色（由 SetIcon 决定）
        private SoundEffect sound1;
        private SoundEffect sound2;

        /// <summary>当前图标标识（"iconArg|tint"，用于 Show 时比较，变化才重载）。</summary>
        public string CurrentIconPath { get; private set; } = "";

        /// <summary>加载音效。OS 就绪后调用一次（图标由 SetIcon 按需加载）。</summary>
        public void LoadContent(OS osInstance)
        {
            sound1 = osInstance.content.Load<SoundEffect>("SFX/DoomShock");
            sound2 = osInstance.content.Load<SoundEffect>("SFX/BrightFlash");
        }

        /// <summary>
        /// 按 ShowTitle 的 Icon / IconTint 设置图标。
        /// iconArg：null = 不显示；DefaultIconMarker = 内置默认图标；其他 = 扩展相对路径（失败回退内置 + Warn）。
        /// tintOverride：null = 自动（内置图标染色 / 自定义图标原色）；true / false = 强制。
        /// </summary>
        public void SetIcon(string iconArg, bool? tintOverride)
        {
            string key = (iconArg ?? "\u0000none") + "|" + (tintOverride.HasValue ? tintOverride.Value.ToString() : "auto");
            if (CurrentIconPath == key) return;   // 未变化不重载

            try { infoIcon?.Dispose(); } catch { }
            infoIcon = null;
            CurrentIconPath = key;

            if (iconArg == null)
            {
                tintIcon = false;                 // 不显示图标
                return;
            }

            bool usedEmbedded = false;
            if (iconArg == DefaultIconMarker)
            {
                infoIcon = LoadEmbeddedIcon();
                usedEmbedded = true;
            }
            else if (!TryLoadFromExtension(iconArg))
            {
                KELog.Warn($"[TitleBanner] icon load failed ({iconArg}); falling back to built-in default icon");
                infoIcon = LoadEmbeddedIcon();
                usedEmbedded = true;
            }

            // 自动规则：内置图标染色（跟随强调色）、自定义图标原色；显式 IconTint 优先
            tintIcon = tintOverride ?? usedEmbedded;
        }

        /// <summary>从扩展目录加载图标；失败返回 false（不抛）。</summary>
        private bool TryLoadFromExtension(string iconPath)
        {
            try
            {
                string folder = ExtensionLoader.ActiveExtensionInfo.FolderPath;
                var gd = Game1.getSingleton().GraphicsDevice;
                using (var s = File.OpenRead(Path.Combine(folder, iconPath)))
                    infoIcon = Texture2D.FromStream(gd, s);
                return infoIcon != null;
            }
            catch (Exception ex)
            {
                KELog.Warn($"[TitleBanner] icon load failed ({iconPath}): {ex.Message}");
                return false;
            }
        }

        /// <summary>从程序集内嵌资源加载内置默认图标（不依赖扩展目录）。</summary>
        private static Texture2D LoadEmbeddedIcon()
        {
            try
            {
                var gd = Game1.getSingleton().GraphicsDevice;
                using (var s = typeof(TitleBanner).Assembly
                    .GetManifestResourceStream("KernelExtensions.Img.Info.png"))
                {
                    if (s == null) { KELog.Warn("[TitleBanner] embedded default icon not found"); return null; }
                    return Texture2D.FromStream(gd, s);
                }
            }
            catch (Exception ex)
            {
                KELog.Warn($"[TitleBanner] embedded icon load failed: {ex.Message}");
                return null;
            }
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
            if (infoIcon != null)
            {
                int inset = 3;
                var iconInner = new Rectangle(iconRect.X + inset, iconRect.Y + inset, iconRect.Width - inset * 2, iconRect.Height - inset * 2);
                // 染色仅当 tintIcon：内置图标默认染色（跟随强调色），自定义图标原色；可由 IconTint 覆盖
                Color iconColor = tintIcon ? Color.Lerp(AccentColor, Color.White, 0.3f) : Color.White;
                sb.Draw(infoIcon, iconInner, iconColor * alpha);
            }
        }
    }
}
