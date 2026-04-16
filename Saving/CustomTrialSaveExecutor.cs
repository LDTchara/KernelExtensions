using Hacknet;
using Pathfinder.Meta.Load;
using Pathfinder.Replacements;      // 提供 SaveLoader.SaveExecutor 基类
using Pathfinder.Util.XML;
using KernelExtensions.Storage;

namespace KernelExtensions.Saving
{
    /// <summary>
    /// 自定义存档加载器，用于从存档 XML 中读取 <CustomTrialData> 节点，
    /// 恢复试炼过程中被删除的节点索引列表，存入全局存储。
    /// </summary>
    [SaveExecutor("CustomTrialData")]   // 对应存档中的根元素名
    public class CustomTrialSaveExecutor : SaveLoader.SaveExecutor
    {
        /// <summary>
        /// 在加载存档时由 Pathfinder 自动调用。
        /// </summary>
        /// <param name="exec">事件执行器（本方法内未使用）</param>
        /// <param name="info">当前 XML 节点的信息，包含属性和子元素</param>
        public override void Execute(EventExecutor exec, ElementInfo info)
        {
            // 读取 ConfigName 和 Nodes 属性
            string configName = info.Attributes.ContainsKey("ConfigName") ? info.Attributes["ConfigName"] : null;
            string nodesStr = info.Attributes.ContainsKey("Nodes") ? info.Attributes["Nodes"] : null;

            // 如果缺少必要属性，直接返回
            if (string.IsNullOrEmpty(configName) || string.IsNullOrEmpty(nodesStr))
                return;

            // 解析逗号分隔的节点索引字符串
            var nodes = new System.Collections.Generic.List<int>();
            foreach (var part in nodesStr.Split(','))
            {
                if (int.TryParse(part, out int idx))
                    nodes.Add(idx);
            }

            // 将解析得到的列表存入全局存储（供后续恢复 Action 使用）
            if (nodes.Count > 0)
                CustomTrialNodeStorage.SetDeletedNodes(configName, nodes);
        }
    }
}