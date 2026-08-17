using System;
using Hacknet;
using Hacknet.Effects;
using KernelExtensions.Utility;
using Pathfinder.Action;
using Pathfinder.Util;
using Pathfinder.Util.XML;

namespace KernelExtensions.Actions
{
    /// <summary>
    /// 向终端逐字打印文本（支持原版 #宏# 替换）。
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
    /// 因此用一次性实例订阅 os.UpdateSubscriptions 逐帧推进（同 9.35 FlashScreen
    /// 模式），渲染完成后退订自清理。
    /// </summary>
    public class TerminalTypeAction : DelayablePathfinderAction
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
                if (KEConfigLoader.Debug)
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
                // 输出前先换行，从新行开始逐字；文本本身以 \n 开头时交给
                // TextWriterTimed 处理（避免双空行）。与原版 TextWriterTimed
                // 的换行处理一致（os.write(" ")）。
                if (text.Length > 0 && text[0] != '\n')
                    os.write(" ");
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
