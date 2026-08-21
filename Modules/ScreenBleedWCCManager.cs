using System;
using System.Collections.Generic;
using Hacknet;
using Hacknet.Effects;
using Hacknet.Extensions;
using Hacknet.Gui;
using KernelExtensions.Config;
using KernelExtensions.Patches;
using KernelExtensions.Utility;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace KernelExtensions.Modules
{
    /// <summary>
    /// 管理 WCC（WithCustomColor）ScreenBleed 效果的计时、渲染和清理。
    /// 由 StartScreenBleedEffectWCCAction 触发，ScreenBleedWCCPatch 驱动。
    /// 完全替代原版 PostProcessor.dangerMode + StartScreenBleed 方案。
    /// </summary>
    internal static class ScreenBleedWCCManager
    {
        private static readonly Dictionary<OS, WCCState> ActiveBleeds = new Dictionary<OS, WCCState>();
        private static readonly Dictionary<ActiveEffectsUpdater, OS> UpdaterToOS = new Dictionary<ActiveEffectsUpdater, OS>();
        private static SpriteBatch _wccSb;

        /// <summary>启动 WCC ScreenBleed。</summary>
        public static void Start(OS os, float duration, Color bgColor, Color textBgColor,
            string title, string l1, string l2, string l3, string completeAction,
            string rawBg = null, string rawTextBg = null)
        {
            if (ActiveBleeds.TryGetValue(os, out var existing))
            {
                existing.Cleanup(os);
                ActiveBleeds.Remove(os);
            }

            ActiveBleeds[os] = new WCCState
            {
                Timer = duration,
                TotalDuration = duration,
                BackgroundColor = bgColor,
                TextBackgroundColor = textBgColor,
                RawBgColor = rawBg ?? "",
                RawTextBgColor = rawTextBg ?? "",
                Title = title,
                L1 = l1,
                L2 = l2,
                L3 = l3,
                CompleteAction = completeAction
            };
            UpdaterToOS[os.EffectsUpdater] = os;
        }

        /// <summary>停止 WCC ScreenBleed（由 CancelScreenBleedEffect 触发）。</summary>
        public static void Stop(OS os)
        {
            if (ActiveBleeds.TryGetValue(os, out var state))
            {
                state.Cleanup(os);
                ActiveBleeds.Remove(os);
            }
        }

        /// <summary>每帧更新计时和渲染注册。</summary>
        public static void Update(OS os, float dt)
        {
            if (!ActiveBleeds.TryGetValue(os, out var state))
                return;

            if (state.CleanedUp)
            {
                ActiveBleeds.Remove(os);
                return;
            }

            state.Timer -= dt;

            if (state.Timer <= 0f)
            {
                state.Cleanup(os);
                ActiveBleeds.Remove(os);
                if (!string.IsNullOrWhiteSpace(state.CompleteAction))
                {
                    try { RunnableConditionalActions.LoadIntoOS(state.CompleteAction, os); }
                    catch (Exception ex) { KELog.Warn("[WCC] CompleteAction: " + ex.Message); }
                }
                return;
            }

            if (_wccSb == null && GuiData.spriteBatch?.GraphicsDevice != null)
                _wccSb = new SpriteBatch(GuiData.spriteBatch.GraphicsDevice);

            os.postFXDrawActions += WccDraw;
        }

        /// <summary>供 ScreenBleedWCCPatch 调用的取消处理器。</summary>
        public static void OnCancelScreenBleed(ActiveEffectsUpdater updater)
        {
            if (UpdaterToOS.TryGetValue(updater, out var os))
            {
                Stop(os);
                UpdaterToOS.Remove(updater);
            }
        }

        // ========== 渲染 ==========

        private static void WccDraw()
        {
            var first = ActiveBleeds.GetEnumerator();
            if (!first.MoveNext()) return;
            var kv = first.Current;
            first.Dispose();

            var state = kv.Value;
            float progress = 1f - (state.Timer / state.TotalDuration);
            if (_wccSb == null) return;

            Rectangle fullscreen = Utils.GetFullscreen();
            int barHeight = 110;
            Rectangle barRect = new Rectangle(0, fullscreen.Height - barHeight - 20, 520, barHeight);
            int scanY = (int)(progress * fullscreen.Height);

            // 每帧重算颜色（动态色随时间变化，静态色走 fallback）
            Color bg = GetColor(state.RawBgColor, state.BackgroundColor);
            Color textBg = GetColor(state.RawTextBgColor, state.TextBackgroundColor);

            _wccSb.Begin();

            // 覆盖色：只画扫描线以上的区域（背景闪烁）
            if (scanY > 0)
                _wccSb.Draw(Utils.white, new Rectangle(0, 0, fullscreen.Width, scanY),
                    Color.Lerp(bg, Color.Black, Utils.randm(0.22f)) * 0.55f);

            // 扫描线
            _wccSb.Draw(Utils.white, new Rectangle(0, scanY, fullscreen.Width, 1),
                Color.Lerp(Color.White, bg, 0.5f) * (Utils.randm(0.7f) + 0.3f));

            // 文字区域背景条（闪烁）
            _wccSb.Draw(Utils.white, barRect,
                Color.Lerp(textBg, Color.Transparent, Utils.randm(0.2f)) * 0.75f);

            _wccSb.End();

            // 文字（doFontLabel 使用已在 Draw 循环中开始的 GuiData.spriteBatch）
            Vector2 pos = new Vector2(barRect.X + 6, barRect.Y + 4);
            TextItem.doFontLabel(pos, state.Title, GuiData.titlefont, Color.White, barRect.Width - 12, 35f, false);
            pos.Y += 32f;
            TextItem.doFontLabel(pos, state.L1, GuiData.font, Color.White, barRect.Width - 10, 20f, false);
            pos.Y += 16f;
            TextItem.doFontLabel(pos, state.L2, GuiData.font, Color.White, barRect.Width - 10, 20f, false);
            pos.Y += 16f;
            TextItem.doFontLabel(pos, state.L3, GuiData.font, Color.White, barRect.Width - 10, 20f, false);
        }

        private static Color GetColor(string raw, Color fallback)
        {
            if (string.IsNullOrWhiteSpace(raw)) return fallback;
            var dynConfig = CustomColorPatch.ParseColorString(raw);
            if (dynConfig != null)
                return CustomColorPatch.CalcColor(dynConfig, OS.currentElapsedTime);

            // 兜底：数值 RGB (255,0,0) / RGBA (255,0,0,128) 和命名色
            try { return new Microsoft.Xna.Framework.Design.ColorConverter().ConvertFromString(raw) as Color? ?? fallback; }
            catch { }
            return fallback;
        }

        private class WCCState
        {
            public float Timer;
            public float TotalDuration;
            public Color BackgroundColor;
            public Color TextBackgroundColor;
            public string RawBgColor;
            public string RawTextBgColor;
            public string Title;
            public string L1;
            public string L2;
            public string L3;
            public string CompleteAction;
            public bool CleanedUp;
            public void Cleanup(OS os) { if (CleanedUp) return; CleanedUp = true; }
        }
    }
}
