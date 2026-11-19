using BepInEx.Hacknet;
using Hacknet;
using HarmonyLib;
using KernelExtensions.Configs;
using KernelExtensions.Utilities;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace KernelExtensions.Patches
{
    [HarmonyPatch(typeof(MainMenu), nameof(MainMenu.DrawBackgroundAndTitle))]
    public static class MainMenuWatermarkPatch
    {
        // 流动速度（越大越快）
        private const float FlowSpeed = 0.8f;
        private const string ZdtkGuid = "kr.o_r.prodzpod.zerodaytoolkit";
        // ZDTK 存在时只需解析/测量一次；不存在时继续按帧查找，等它加载后再让位
        private static bool _zdtkResolved;
        private static float _offset = -1f;
        // 足够大的模数（约 11.5 天），确保在游戏会话中永不循环，同时 float 精度足够
        private const long TicksMod = 10_000_000_000_000L;

        static void Prefix(MainMenu __instance)
        {
            if (!ConfigLoader.Watermark) return;
            if (GuiData.smallfont == null) return;

            float baseX = __instance.State == MainMenu.MainMenuState.Normal ? 634f : 496f;
            // ZDTK 存在时排到它右侧；不存在时占用它原来的位置
            float x = baseX + GetZdtkOffset();
            float y = 200f;   // 与 ZDTK 同一行

            string text = "+ KernelExtensions " + KernelExtensions.ModVer;

            // 修正后的时间：模大数 → 相对秒数，单调递增且 float 高精度
            float baseTime = (float)((DateTime.Now.Ticks % TicksMod) / (double)TimeSpan.TicksPerSecond) * FlowSpeed;

            SpriteBatch sb = GuiData.spriteBatch;
            SpriteFont font = GuiData.smallfont;
            float currentX = x;
            // 逐字上下晃动幅度（像素），与彩虹色同速率
            const float bobAmplitude = 5f;

            for (int i = 0; i < text.Length; i++)
            {
                float pos = text.Length <= 1 ? 0f : (float)i / (text.Length - 1);
                Color color = FlowColorHelper.GetFlowingRainbowColor(pos, baseTime);
                // 同时间源产生逐字相位偏移的垂直晃动
                float bob = (float)Math.Sin(baseTime * 4 + pos * Math.PI * 4.0) * bobAmplitude;

                string character = text[i].ToString();
                float charWidth = font.MeasureString(character).X;
                sb.DrawString(font, character, new Vector2(currentX, y + bob), color);
                currentX += charWidth;
            }
        }

        /// <summary>
        /// 读取 ZDTK 实际绘制的水印文字。软依赖只声明加载关系；即使 ZDTK 尚未加载，
        /// 这里也会在后续帧继续查找，未安装时返回 false，KE 水印直接使用原版位置。
        /// </summary>
        private static bool TryGetZdtkWatermark(out string text)
        {
            text = null;
            var plugins = HacknetChainloader.Instance?.Plugins;
            if (plugins == null || !plugins.TryGetValue(ZdtkGuid, out var info))
                return false;

            // BepInEx.Core 的 PluginInfo.Metadata.Version 来自 SemanticVersioning；
            // KE 不直接引用该程序集，这里用反射取字符串，避免新增运行时依赖。
            var metadata = info?.Metadata;
            string version = metadata?.GetType().GetProperty("Version")?.GetValue(metadata)?.ToString();
            if (string.IsNullOrEmpty(version))
                return false;

            text = "+ ZeroDayToolKit " + version;
            return true;
        }

        private static float GetZdtkOffset()
        {
            if (_zdtkResolved)
                return _offset;
            if (!TryGetZdtkWatermark(out string text))
                return 0f;

            _offset = GuiData.smallfont.MeasureString(text).X + 8f;
            _zdtkResolved = true;
            return _offset;
        }
    }
}