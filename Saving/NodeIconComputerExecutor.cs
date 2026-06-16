using Hacknet;
using Pathfinder.Meta.Load;
using Pathfinder.Replacements;
using Pathfinder.Util.XML;
using KernelExtensions.Storage;

namespace KernelExtensions.Saving
{
    /// <summary>
    /// 在游戏加载/创建节点时自动初始化 OrgIcon 和 CurrIcon。
    /// 使用 [ComputerExecutor] 属性自动注册，无需手动调用 RegisterExecutor。
    /// </summary>
    [ComputerExecutor("Computer")]
    public class NodeIconComputerExecutor : ContentLoader.ComputerExecutor
    {
        public override void Execute(EventExecutor exec, ElementInfo info)
        {
            if (Comp == null) return;

            string orgIcon;
            if (info.Attributes.TryGetValue("icon", out var srcIcon) && !string.IsNullOrEmpty(srcIcon))
                orgIcon = srcIcon;
            else
            {
                int secLevel = 2;
                if (info.Attributes.TryGetValue("security", out var secStr)
                    && int.TryParse(secStr, out var parsed))
                    secLevel = parsed;
                orgIcon = NodeIconStorage.GetSecurityIconName(secLevel);
            }

            NodeIconStorage.InitOrgIcon(Comp.idName, orgIcon);

            if (string.IsNullOrEmpty(NodeIconStorage.GetCurrIcon(Comp.idName)))
                NodeIconStorage.SetCurrIcon(Comp.idName, orgIcon);
        }
    }
}