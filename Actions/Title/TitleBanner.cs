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
        /// 按 ShowTitle 的 icon / icontint 设置图标。
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
            if (iconArg == TitleBannerHooks.DefaultIconMarker)
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

            // 自动规则：内置图标染色（跟随强调色）、自定义图标原色；显式 icontint 优先
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
                // 染色仅当 tintIcon：内置图标默认染色（跟随强调色），自定义图标原色；可由 icontint 覆盖
                Color iconColor = tintIcon ? Color.Lerp(AccentColor, Color.White, 0.3f) : Color.White;
                sb.Draw(infoIcon, iconInner, iconColor * alpha);
            }
        }
    }

    /// <summary>Harmony 钩子：OS.LoadContent 初始化单例、OS.Update/Draw 驱动横幅。</summary>
    [HarmonyPatch]
    internal static class TitleBannerHooks
    {
        internal static TitleBanner Instance;

        /// <summary>icon="default" 的内部标记（与扩展路径区分）。</summary>
        internal const string DefaultIconMarker = "\u0001default";

        private static bool _drawFailedWarned;

        /// <summary>弹出横幅。colorKey=CC 颜色关键字/Hex/名称（NONE/空=用 defaultColor）；强调色每帧刷新，动态色不定格。
        /// iconArg=null 时不显示图标；tintOverride=null 时按自动规则（内置图标染色 / 自定义图标原色）。</summary>
        internal static void Show(string title, string body, float duration, string colorKey, Color defaultColor, string iconArg, bool? tintOverride)
        {
            if (Instance == null) return;
            Instance.SetIcon(iconArg, tintOverride);
            // 各按自身字体的字符集清洗：titlefont(Kremlin) 无本地化版 → 非 ASCII 降级为 '?'；
            // Body 用 GuiData.font（官方本地化字体，含本语言字形）→ 中文/日文/韩文等原样保留。
            Instance.TitleText = TextHelper.CleanStringForFont(GuiData.titlefont, title);
            Instance.BodyText = TextHelper.CleanStringForFont(GuiData.font, body);
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
            banner.LoadContent(__instance);
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
