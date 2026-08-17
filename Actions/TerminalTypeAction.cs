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

                os.delayer.Post(ActionDelayer.NextTick(), () =>
                {
                    // 使用原版逐字打印方法
                    TextWriterTimed.WriteTextToTerminal(
                        finalText, os, CharDelay, 1f, 20f, 0f, 0);
                });
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
    }
}
