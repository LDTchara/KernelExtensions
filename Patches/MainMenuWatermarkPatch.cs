using Hacknet;
using Hacknet.Gui;
using HarmonyLib;
using KernelExtensions.Utility;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace KernelExtensions.Patches
{
    [HarmonyPatch(typeof(MainMenu), nameof(MainMenu.DrawBackgroundAndTitle))]
    public static class MainMenuWatermarkPatch
    {
        // 流动速度（越大越快）
        private const float FlowSpeed = 0.8f;
        // 缓存 ZDTK 最大宽度 + 间距，避免每帧重复测量
        private static float _offset = -1f;
        // 足够大的模数（约 11.5 天），确保在游戏会话中永不循环，同时 float 精度足够
        private const long TicksMod = 10_000_000_000_000L;

        static void Prefix(MainMenu __instance)
        {
            if (GuiData.smallfont == null) return;

            // 计算偏移：只在第一次测量
            if (_offset < 0)
            {
                string zdtkMax = "+ ZeroDayToolKit 9.9.9";
                _offset = GuiData.smallfont.MeasureString(zdtkMax).X + 8f;
            }

            float baseX = __instance.State == MainMenu.MainMenuState.Normal ? 634f : 496f;
            float x = baseX + _offset;
            float y = 200f;   // 与 ZDTK 同一行

            string text = "+ KernelExtensions " + KernelExtensions.ModVer;

            // 修正后的时间：模大数 → 相对秒数，单调递增且 float 高精度
            float baseTime = (float)((DateTime.Now.Ticks % TicksMod) / (double)TimeSpan.TicksPerSecond) * FlowSpeed;

            SpriteBatch sb = GuiData.spriteBatch;
            SpriteFont font = GuiData.smallfont;
            float currentX = x;

            for (int i = 0; i < text.Length; i++)
            {
                float pos = text.Length <= 1 ? 0f : (float)i / (text.Length - 1);
                Color color = FlowColorUtils.GetFlowingRainbowColor(pos, baseTime);

                string character = text[i].ToString();
                float charWidth = font.MeasureString(character).X;
                sb.DrawString(font, character, new Vector2(currentX, y), color);
                currentX += charWidth;
            }
        }
    }
}