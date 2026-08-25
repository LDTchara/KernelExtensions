using System.Runtime.CompilerServices;
using Hacknet;
using KernelExtensions.Patches;
using KernelExtensions.Utilities;
using Microsoft.Xna.Framework;
using Pathfinder.Action;
using Pathfinder.Util;
using Pathfinder.Util.XML;
using XColor = Microsoft.Xna.Framework.Color; // 嵌套类内 Color 会与外层 XMLStorage 字段重名，用别名

using KernelExtensions.Managers;
namespace KernelExtensions.Actions
{
    /// <summary>
    /// UI 闪烁 —— 修改 os.highlightColor / os.moduleColorSolid 为指定色并渐隐回默认。
    /// 对齐原版 warningFlash() 机制（timer + Lerp 渐隐），但颜色与时长可自定义。
    ///
    /// 用法：
    /// <FlashScreen Color="Red" Duration="2.0" />
    /// <FlashScreen Color="#FF2020" Duration="1.5" />
    /// <FlashScreen Color="LDTchara:0.1" Duration="2.0" />   <!-- 动态色（彩虹循环）-->
    ///
    /// 属性：
    ///   Color    — 必要。Hex (#RRGGBB/#AARRGGBB)、数值 RGB (R,G,B[,A])、命名色、
    ///              CustomColor 动态色（LDTchara/Rainbow/Gradient/预设）
    ///   Duration — 渐变时长（秒），默认 2.0。触发瞬间最亮，线性渐隐回默认色。
    ///   PlaySound — 可选，默认 false。true 时闪烁同时播放警告音效
    ///              （os.beepSound = SFX/beep，原版警告提示音）。
    ///
    /// 行为：
    ///   - 重复触发 = 刷新（取消上一次闪烁，重新开始），不叠加
    ///   - 恢复目标实时读 defaultHighlightColor/moduleColorSolidDefault，
    ///     闪烁期间切主题会淡回"当前主题的默认色"（原版行为）
    ///   - 无 Manager/常驻状态：每次触发创建一次性实例，订阅 os.UpdateSubscriptions
    ///     驱动渐变，结束后退订自清理
    /// </summary>
    public class FlashScreenAction : DelayablePathfinderAction
    {
        [XMLStorage] public string Color;
        [XMLStorage] public float Duration = 2.0f;
        [XMLStorage] public bool PlaySound;

        /// <summary>当前活跃闪烁实例（弱引用表：仅用于重复触发刷新，不阻止 GC、OS 卸载自动清理）</summary>
        private static readonly ConditionalWeakTable<OS, FlashFade> ActiveFades = new();

        public override void Trigger(OS os)
        {
            try
            {
                if (Duration <= 0f)
                {
                    // 非正时长：立即恢复默认，不订阅
                    os.highlightColor = os.defaultHighlightColor;
                    os.moduleColorSolid = os.moduleColorSolidDefault;
                    return;
                }

                // 重复触发 = 刷新：取消上一次
                if (ActiveFades.TryGetValue(os, out var old))
                {
                    old.Cancel();
                    ActiveFades.Remove(os);
                }

                // 可选：闪烁同时播放警告音效（原版 os.beepSound = SFX/beep）
                if (PlaySound && os.beepSound != null)
                    os.beepSound.Play();

                var (staticColor, dynConfig) = ParseColor(os, Color);
                var fade = new FlashFade(os, staticColor, dynConfig, Duration);
                ActiveFades.Add(os, fade);
                os.UpdateSubscriptions += fade.Update;
            }
            catch (Exception ex)
            {
                KELog.Error("[FlashScreen] Trigger failed: " + ex.Message);
            }
        }

        private static (XColor staticColor, CustomColorManager.DynColorConfig dynConfig) ParseColor(OS os, string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return (os.defaultHighlightColor, null);

            // 动态色：LDTchara/Rainbow/Gradient/预设
            var dyn = CustomColorManager.ParseColorString(raw);
            if (dyn != null)
                return (XColor.White, dyn);

            // 静态色：#RRGGBB / #AARRGGBB / R,G,B[,A]
            if (raw.StartsWith("#") || raw.Contains(','))
                return (ColorHelper.ParseHexColor(raw), null);

            // 兜底：命名色（Red/Monochrome 等）
            try
            {
                if (new Microsoft.Xna.Framework.Design.ColorConverter().ConvertFromString(raw) is Color named)
                    return (named, null);
            }
            catch { }
            return (os.defaultHighlightColor, null);
        }

        /// <summary>一次性闪烁渐变实例：订阅驱动，结束时退订自清理。</summary>
        private class FlashFade
        {
            private readonly OS os;
            private readonly float duration;
            private readonly XColor staticColor;
            private readonly CustomColorManager.DynColorConfig dynConfig;
            private float timer;
            private bool cancelled;

            public FlashFade(OS os, XColor staticColor, CustomColorManager.DynColorConfig dynConfig, float duration)
            {
                this.os = os;
                this.staticColor = staticColor;
                this.dynConfig = dynConfig;
                this.duration = Math.Max(0.01f, duration);
                this.timer = duration;
                Apply(1f); // 触发瞬间最亮
            }

            public void Update(float dt)
            {
                if (cancelled) return;
                timer -= dt;
                if (timer <= 0f)
                {
                    Restore();
                    Unsubscribe();
                    return;
                }
                Apply(timer / duration);
            }

            /// <summary>取消（刷新语义）：恢复默认并退订。</summary>
            public void Cancel()
            {
                if (cancelled) return;
                cancelled = true;
                Restore();
                Unsubscribe();
            }

            private void Apply(float t)
            {
                XColor current = dynConfig != null
                    ? CustomColorManager.CalcColor(dynConfig, OS.currentElapsedTime)
                    : staticColor;
                os.highlightColor = XColor.Lerp(os.defaultHighlightColor, current, t);
                os.moduleColorSolid = XColor.Lerp(os.moduleColorSolidDefault, current, t);
            }

            private void Restore()
            {
                os.highlightColor = os.defaultHighlightColor;
                os.moduleColorSolid = os.moduleColorSolidDefault;
            }

            private void Unsubscribe()
            {
                os.UpdateSubscriptions -= Update;
            }
        }
    }
}
