using Hacknet;
using KernelExtensions.Storage;
using Pathfinder.Action;
using Pathfinder.Util;

namespace KernelExtensions.Actions.Link
{
    /// <summary>
    /// 重置节点链接为 org 基线（丢弃运行时所有临时增删）。
    ///
    /// XML 用法：
    ///   &lt;LinkControlReset SourceComp="playerComp" /&gt;
    ///
    /// SourceComp：被操作电脑的 idName（必填，大小写敏感）。
    /// org 基线见 Storage/OrgLinksStorage（内容 XML 的 dlink 与 &lt;OrgLinks&gt; 在 OSLoaded 时快照）。
    /// </summary>
    public class LinkControlResetAction : DelayablePathfinderAction
    {
        [XMLStorage] public string SourceComp;

        public override void Trigger(OS os) => OrgLinksStorage.Reset(os, SourceComp);
    }
}
