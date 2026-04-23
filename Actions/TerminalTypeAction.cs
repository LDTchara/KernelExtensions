using Hacknet;
using Hacknet.Effects;
using Pathfinder.Action;
using Pathfinder.Util;
using Pathfinder.Util.XML;

namespace KernelExtensions.Actions
{
    /// <summary>
    /// 向终端逐字打印文本（支持原版 #宏# 替换）。
    /// 支持 Delay 和 DelayHost 属性。
    /// 用法：<TerminalType text="消息内容" CharDelay="0.04" Delay="1.5" />
    /// CharDelay：每个字符输出间隔（秒），默认 0.04（与原版 TextWriterTimed 一致）。
    /// </summary>
    public class TerminalTypeAction : DelayablePathfinderAction
    {
        public string Text;
        public float CharDelay = 0.04f;

        public override void Trigger(OS os)
        {
            if (os.terminal == null || string.IsNullOrEmpty(Text)) return;

            string finalText = ComputerLoader.filter(Text);
            os.delayer.Post(ActionDelayer.NextTick(), () =>
            {
                // 使用原版逐字打印方法
                TextWriterTimed.WriteTextToTerminal(
                    finalText, os, CharDelay, 1f, 20f, 0f, 0);
            });
        }

        public override void LoadFromXml(ElementInfo info)
        {
            Text = info.Attributes.GetString("text", null);
            if (info.Attributes.TryGetValue("CharDelay", out string delayStr))
                float.TryParse(delayStr, out CharDelay);
            if (info.Attributes.TryGetValue("Delay", out string delayStr2))
                Delay = delayStr2;
            if (info.Attributes.TryGetValue("DelayHost", out string delayHost))
                DelayHost = delayHost;
        }
    }
}