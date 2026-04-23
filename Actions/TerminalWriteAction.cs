using Hacknet;
using Pathfinder.Action;
using Pathfinder.Util;
using Pathfinder.Util.XML;

namespace KernelExtensions.Actions
{
    /// <summary>
    /// 向终端写入一行文本。
    /// 支持延迟（Delay）和延迟主机（DelayHost）属性。
    /// 
    /// 用法示例：
    ///   <TerminalWrite text="消息内容" />
    ///   带延迟：
    ///   <TerminalWrite text="延迟消息" Delay="1.5" />
    /// </summary>
    public class TerminalWriteAction : DelayablePathfinderAction
    {
        public string Text;

        public override void Trigger(OS os)
        {
            if (os.terminal == null || string.IsNullOrEmpty(Text)) return;
            string finalText = ComputerLoader.filter(Text);
            os.terminal.writeLine(finalText);
        }

        public override void LoadFromXml(ElementInfo info)
        {
            Text = info.Attributes.GetString("text", null);
            if (info.Attributes.TryGetValue("Delay", out string delayStr))
                Delay = delayStr;
            if (info.Attributes.TryGetValue("DelayHost", out string delayHost))
                DelayHost = delayHost;
        }
    }
}