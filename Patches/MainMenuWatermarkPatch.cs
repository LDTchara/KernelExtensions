using Hacknet;
using HarmonyLib;
using KernelExtensions.Compat;
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
        // 主菜单绘制时插件已全部加载完成，因此 ZDTK 检测只做一次。
        // 未安装时永久占用原版位置，不再逐帧重试（也不会出现“后来才加载”的情况）。
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

        private static float GetZdtkOffset()
        {
            if (_zdtkResolved)
                return _offset;

            _zdtkResolved = true;
            if (!ModCompats.TryGetZdtkWatermark(out string text))
            {
                _offset = 0f;   // 未安装 ZDTK：占用它原本的位置
                return _offset;
            }

            // ZDTK 已加载：按其实际版本文字宽度右移，避免两段文字重叠。
            _offset = GuiData.smallfont.MeasureString(text).X + 8f;
            return _offset;
        }
    }
}