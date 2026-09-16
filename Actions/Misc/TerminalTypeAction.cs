using System;
using Hacknet;
using Hacknet.Effects;
using KernelExtensions.Configs;
using KernelExtensions.Utilities;
using Pathfinder.Action;
using Pathfinder.Util;
using Pathfinder.Util.XML;

namespace KernelExtensions.Actions.Misc
{
    /// <summary>
    /// 向终端逐字打印文本（支持原版 #宏# 替换）。
    /// **不自动换行**：从当前光标处逐字追加，语义与 HackerScript 的 write 相近——
    /// 可用多条 TerminalType 在同一行内分段输出（各自不同速度），或用 \n 自行控制换行。
    /// 支持 Delay 和 DelayHost 属性。
    ///
    /// 用法：
    ///   &lt;TerminalType Text="消息内容" CharDelay="0.04" /&gt;
    ///   &lt;TerminalType text="旧写法兼容" /&gt;
    ///
    /// 属性（正式名 = 字段名，大小写敏感）：
    ///   Text      — 必填。打印的文本（支持 #宏# 替换）
    ///   CharDelay — 每个字符输出间隔（秒），默认 0.04（与原版 TextWriterTimed 一致）
    ///   Delay / DelayHost — 由 DelayablePathfinderAction 提供
    ///
    /// 兼容性：历史版本曾用小写 "text" 属性，仍可用（仅当 Text 未提供时回退读取）。
    ///
    /// 实现：原版 TextWriterTimed.WriteTextToTerminal 是**增量渲染**函数
    /// （按 elapsedTimeSoFar 计算应渲染到第几个字符并返回进度），不能一次性调用。
    /// 因此用一次性实例订阅 os.UpdateSubscriptions 逐帧推进（同 FlashScreenAction 的做法）
    /// 模式），渲染完成后退订自清理。
    /// </summary>
    public class TerminalTypeAction : KEAction
    {
        [XMLStorage] public string Text;
        [XMLStorage] public float CharDelay = 0.04f;

        public override void Trigger(OS os)
        {
            try
            {
                if (os.terminal == null)
                {
                    KELog.Warn("[TerminalType] os.terminal is null, cannot print");
                    return;
                }
                if (string.IsNullOrEmpty(Text))
                {
                    KELog.Warn("[TerminalType] Text is empty (use attribute Text= or legacy text=)");
                    return;
                }

                string finalText = ComputerLoader.filter(Text);
                if (ConfigLoader.Debug)
                    KELog.Debug($"[TerminalType] trigger: len={finalText.Length} CharDelay={CharDelay}");

                new TimedPrinter(os, finalText, CharDelay).Start();
            }
            catch (Exception ex)
            {
                KELog.Error("[TerminalType] Trigger failed: " + ex.Message);
            }
        }

        public override void LoadFromXml(ElementInfo info)
        {
            base.LoadFromXml(info); // 读取 [XMLStorage]：Text / CharDelay / Delay / DelayHost
            // 兼容历史小写 "text"（仅当正式名 Text 未提供时回退）
            if (string.IsNullOrEmpty(Text) && info.Attributes.TryGetValue("text", out string legacyText))
                Text = legacyText;
        }

        /// <summary>一次性逐字打印实例：订阅驱动，渲染完成退订自清理（FlashScreen 同款模式）。</summary>
        private class TimedPrinter
        {
            private readonly OS os;
            private readonly string text;
            private readonly float timePerChar;
            private float elapsed;
            private int rendered;
            private bool done;

            public TimedPrinter(OS os, string text, float timePerChar)
            {
                this.os = os;
                this.text = text;
                this.timePerChar = timePerChar;
            }

            public void Start()
            {
                // 不自动换行：从当前光标处开始逐字追加，语义与 HackerScript 的 write 一致。
                // 需要换行时由作者在文本中写 \n，或用 TerminalWrite 另起一行。
                // （2026-09-16 变更：此前会先 os.write(" ") 强制另起一行，导致无法行内追加）
                os.UpdateSubscriptions += Update;
            }

            private void Update(float dt)
            {
                if (done) return;
                elapsed += dt;
                // 增量渲染：传入累计时间 + 上次进度，返回新进度
                rendered = TextWriterTimed.WriteTextToTerminal(
                    text, os, timePerChar, 1f, 20f, elapsed, rendered);
                if (rendered >= text.Length)
                {
                    done = true;
                    os.UpdateSubscriptions -= Update;
                }
            }
        }
    }
}
