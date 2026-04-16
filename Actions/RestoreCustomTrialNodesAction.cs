using Hacknet;
using Hacknet.Effects;
using KernelExtensions.Storage;
using Pathfinder.Action;
using Pathfinder.Util;
using Pathfinder.Util.XML;
using System;
using System.Collections.Generic;
using System.Xml;

namespace KernelExtensions.Actions
{
    /// <summary>
    /// 自定义 Action：恢复指定试炼配置中被删除的节点。
    /// 用法：<RestoreCustomTrialNodes ConfigName="MyTrial" />
    /// </summary>
    public class RestoreCustomTrialNodesAction : PathfinderAction
    {
        [XMLStorage]
        public string ConfigName;   // 试炼配置名（必须与删除时使用的配置名一致）

        public override void Trigger(object os_obj)
        {
            OS os = (OS)os_obj;
            if (string.IsNullOrEmpty(ConfigName))
            {
                Console.WriteLine("[KernelExtensions] RestoreCustomTrialNodes: ConfigName attribute is missing.");
                return;
            }

            var nodesToRestore = CustomTrialNodeStorage.GetDeletedNodes(ConfigName);
            if (nodesToRestore.Count == 0)
            {
                Console.WriteLine($"[KernelExtensions] RestoreCustomTrialNodes: No deleted nodes found for config '{ConfigName}'.");
                return;
            }

            // 计算每个节点的恢复间隔（总时长 3 秒）
            float totalTime = 3f;
            float interval = totalTime / nodesToRestore.Count;
            float currentDelay = 0f;

            foreach (int nodeIdx in nodesToRestore)
            {
                // 确保节点存在且不在可见列表中
                if (nodeIdx >= 0 && nodeIdx < os.netMap.nodes.Count)
                {
                    var node = os.netMap.nodes[nodeIdx];
                    // 延迟添加
                    os.delayer.Post(ActionDelayer.Wait(currentDelay), () =>
                    {
                        if (!os.netMap.visibleNodes.Contains(nodeIdx))
                            os.netMap.visibleNodes.Add(nodeIdx);
                        // 高亮闪烁
                        node.highlightFlashTime = 1f;
                        // 添加特效
                        SFX.addCircle(node.getScreenSpacePosition(), Utils.AddativeWhite * 0.4f, 70f);
                    });
                    // 第二次特效（在原版中延迟稍后）
                    os.delayer.Post(ActionDelayer.Wait(currentDelay + interval * 0.5f), () =>
                    {
                        SFX.addCircle(node.getScreenSpacePosition(), Utils.AddativeWhite * 0.3f, 30f);
                    });
                }
                currentDelay += interval;
            }

            // 恢复完成后清除该配置的删除记录
            CustomTrialNodeStorage.ClearDeletedNodes(ConfigName);
            Console.WriteLine($"[KernelExtensions] RestoreCustomTrialNodes: Restored {nodesToRestore.Count} nodes for config '{ConfigName}'.");
        }

        public override void LoadFromXml(ElementInfo info)
        {
            base.LoadFromXml(info);
            // XMLStorage 会自动填充 ConfigName，无需额外代码
        }
    }
}