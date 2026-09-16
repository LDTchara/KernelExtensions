using Hacknet;
using Hacknet.Gui;
using Microsoft.Xna.Framework;
using Pathfinder.Action;
using Pathfinder.Util;
using Pathfinder.Util.XML;

namespace KernelExtensions.Actions
{
    /// <summary>
    /// 终端聚焦特效：全屏变暗（除终端外）+ 终端边框扩散发光。
    /// 支持 Delay 和 DelayHost 属性。
    /// 
    /// 用法示例：
    ///   <TerminalFocus Duration="5.0" BorderDuration="2.0" FadeInDuration="0.5" DarkenAlpha="0.8" ExpandAmount="200" />
    ///   - Duration: 遮罩保持的总时长（秒），默认 2.0。在此之后遮罩消失。
    ///   - BorderDuration: 边框扩散动画的时长（秒），默认等于 Duration。若短于 Duration，则动画结束后边框消失但遮罩保留。
    ///   - FadeInDuration: 遮罩从不透明渐变到完全变黑所需时长（秒），默认与 Duration 相同。
    ///   - DarkenAlpha: 遮罩的最大透明度（0~1），默认 0.8。
    ///   - ExpandAmount: 边框向外扩展的最大像素量，默认 200。
    /// </summary>
    public class TerminalFocusAction : KEAction
    {
        [XMLStorage] public float Duration = 2.0f;
        [XMLStorage] public float BorderDuration = -1f;   // -1 表示使用 Duration
        [XMLStorage] public float FadeInDuration = -1f;   // -1 表示使用 Duration
        [XMLStorage] public float DarkenAlpha = 0.8f;
        [XMLStorage] public float ExpandAmount = 200f;

        public override void Trigger(OS os)
        {
            if (os.terminal == null) return;

            float actualBorder = BorderDuration >= 0 ? BorderDuration : Duration;
            float actualFadeIn = FadeInDuration >= 0 ? FadeInDuration : Duration;
            var anim = new TerminalFocusAnimation(os, Duration, actualBorder, actualFadeIn, DarkenAlpha, ExpandAmount);
            anim.Start();
        }

        // 参数由 [XMLStorage] 统一解析（基类 KEAction → DelayablePathfinderAction 负责 Delay/DelayHost）；
        // 早前版本在此手写 LoadFromXml 且未调用 base，导致 Delay/DelayHost 实际不生效（2026-09-16 修复）。

        private class TerminalFocusAnimation
        {
            private OS os;
            private float totalDuration;
            private float borderDuration;
            private float fadeInDuration;
            private float darkenAlpha;
            private float expandAmount;
            private float timer = 0f;
            private bool active = false;

            public TerminalFocusAnimation(OS os, float totalDuration, float borderDuration, float fadeInDuration, float darkenAlpha, float expandAmount)
            {
                this.os = os;
                this.totalDuration = totalDuration;
                this.borderDuration = borderDuration;
                this.fadeInDuration = fadeInDuration;
                this.darkenAlpha = darkenAlpha;
                this.expandAmount = expandAmount;
            }

            public void Start()
            {
                timer = 0f;
                active = true;
                os.postFXDrawActions += Draw;
                os.UpdateSubscriptions += Update;
            }

            private void Update(float dt)
            {
                if (!active) return;
                timer += dt;
                if (timer >= totalDuration)
                {
                    active = false;
                    os.postFXDrawActions -= Draw;
                    os.UpdateSubscriptions -= Update;
                }
            }

            private void Draw()
            {
                if (!active || os.terminal == null) return;

                // 遮罩淡入进度（基于 fadeInDuration，但不超过 1）
                float darkenProgress = MathHelper.Clamp(timer / fadeInDuration, 0f, 1f);

                // 全屏遮罩（持续整个 totalDuration）
                Utils.FillEverywhereExcept(
                    Utils.InsetRectangle(os.terminal.bounds, 1),
                    Utils.GetFullscreen(),
                    GuiData.spriteBatch,
                    Color.Black * darkenAlpha * darkenProgress
                );

                // 边框扩散动画（仅在前 borderDuration 秒内绘制）
                if (timer < borderDuration)
                {
                    float borderProgress = MathHelper.Clamp(timer / borderDuration, 0f, 1f);
                    float num = 1f - borderProgress;
                    num = Utils.CubicInCurve(num);

                    Rectangle borderRect = Utils.InsetRectangle(os.terminal.bounds, (int)(-1f * expandAmount * num));
                    float alpha = 1f - num;
                    if (alpha >= 0.8f)
                        alpha = (1f - (alpha - 0.8f) * 5f) * 0.8f;
                    int thickness = (int)(60f * (0.06f + num));

                    RenderedRectangle.doRectangleOutline(
                        borderRect.X, borderRect.Y,
                        borderRect.Width, borderRect.Height,
                        thickness,
                        new Color?(os.highlightColor * alpha)
                    );
                }
            }
        }
    }
}