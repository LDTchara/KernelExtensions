using Hacknet;
using HarmonyLib;
using KernelExtensions.Managers;
using KernelExtensions.Utilities;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace KernelExtensions.Patches
{
    /// <summary>
    /// Harmony 钩子：OS.LoadContent 初始化横幅单例、OS.Update 推进计时、OS.Draw 绘制横幅。
    /// 绘制逻辑在 <see cref="TitleBanner"/>（Managers/TitleBannerManager.cs）。
    /// </summary>
    [HarmonyPatch]
    internal static class TitleBannerPatches
    {
        internal static TitleBanner Instance;

        private static bool _drawFailedWarned;

        /// <summary>弹出横幅。colorKey=CC 颜色关键字/动态色（NONE/空=用 defaultColor）；强调色每帧刷新，动态色不定格。
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
