using Hacknet;
using Pathfinder.Action;
using Pathfinder.Util;
using Pathfinder.Util.XML;

namespace KernelExtensions.Actions.Misc
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
    public class TerminalWriteAction : KEAction
    {
        /// <summary>写入的文本（属性名大小写不敏感，推荐 PascalCase；不可为空，否则静默跳过）。</summary>
        [XMLStorage] public string Text;

        public override void Trigger(OS os)
        {
            if (os.terminal == null || string.IsNullOrEmpty(Text)) return;
            string finalText = ComputerLoader.filter(Text);
            os.terminal.writeLine(finalText);
        }
    }
}