using Pathfinder.Meta.Load;
using Pathfinder.Replacements;
using Pathfinder.Util.XML;

namespace KernelExtensions.Storage
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

            // 确定 OrgIcon：有显式 icon 属性 → 直接用；否则按 security 级别映射
            string orgIcon;
            if (info.Attributes.TryGetValue("icon", out var srcIcon) && !string.IsNullOrEmpty(srcIcon))
                orgIcon = srcIcon;
            else
            {
                // 默认 0 与 C# int 默认值一致，也对应 computers[0] = Sec0Computer
                int secLevel = 0;
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