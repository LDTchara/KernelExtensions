using Hacknet;
using KernelExtensions.Storage;
using Pathfinder.Action;
using Pathfinder.Util;

namespace KernelExtensions.Actions.Link
{
    /// <summary>
    /// 运行时临时**删除**一条节点链接（只改 links，不写 org 基线）。
    ///
    /// XML 用法：
    ///   &lt;LinkControlRemove SourceComp="playerComp" TargetComp="jmail" /&gt;
    ///
    /// SourceComp：被操作电脑的 idName（必填，大小写敏感）；
    /// TargetComp：目标电脑的 idName（必填）。
    /// 临时改动可用 LinkControlReset 恢复；存档时 links 由原版处理。
    /// </summary>
    public class LinkControlRemoveAction : KEAction
    {
        [XMLStorage] public string SourceComp;
        [XMLStorage] public string TargetComp;

        public override void Trigger(OS os) => OrgLinksStorage.Modify(os, SourceComp, TargetComp, add: false);
    }
}
