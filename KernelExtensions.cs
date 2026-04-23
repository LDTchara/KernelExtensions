using BepInEx;
using BepInEx.Hacknet;
using Hacknet;
using KernelExtensions.Actions;
using KernelExtensions.Executables;
using KernelExtensions.Saving;
using Microsoft.Xna.Framework.Graphics;
using Pathfinder.Action;
using Pathfinder.Event;
using Pathfinder.Event.Saving;
using Pathfinder.Executable;
using Pathfinder.Replacements;      // 提供 SaveLoader 用于注册存档加载器
using System.Xml.Linq;

namespace KernelExtensions
{
    [BepInPlugin(ModGUID, ModName, ModVer)]
    public class KernelExtensions : HacknetPlugin
    {
        public const string ModGUID = "com.LDTchara.KernelExtensions";
        public const string ModName = "KernelExtensions";
        public const string ModVer = "0.4.4";

        public override bool Load()
        {
            // 1. 注册自定义试炼可执行程序
            ExecutableManager.RegisterExecutable<CustomTrialExe>("#CUSTOMTRIAL#");
            Console.WriteLine("[KernelExtensions] CustomTrial registered.");

            // 2. 注册各 Action
            ActionManager.RegisterAction<TerminalFocusAction>("TerminalFocus");
            Console.WriteLine("[KernelExtensions] TerminalFocus action registered.");
            ActionManager.RegisterAction<TerminalWriteAction>("TerminalWrite");
            Console.WriteLine("[KernelExtensions] TerminalWrite action registered.");
            ActionManager.RegisterAction<TerminalTypeAction>("TerminalType");
            Console.WriteLine("[KernelExtensions] TerminalType action registered.");
            ActionManager.RegisterAction<RestoreCustomTrialNodesAction>("RestoreCustomTrialNodes");
            Console.WriteLine("[KernelExtensions] RestoreCustomTrialNodes action registered.");

            // 3. 注册存档保存事件处理器（用于将删除节点写入存档）
            EventManager<SaveEvent>.AddHandler(OnSaveGame);
            Console.WriteLine("[KernelExtensions] Save event handler registered.");

            // 4. 注册自定义存档加载器（用于从存档中读取删除节点）
            SaveLoader.RegisterExecutor<CustomTrialSaveExecutor>("CustomTrialData");
            Console.WriteLine("[KernelExtensions] CustomTrialSaveExecutor registered.");

            Console.WriteLine("[KernelExtensions] All is well ** SUCCESS!!");
            return true;
        }

        /// <summary>
        /// 在游戏保存存档时触发，将当前正在运行的试炼中已删除的节点列表
        /// 写入存档的自定义节点 <CustomTrialData> 中。
        /// </summary>
        private void OnSaveGame(SaveEvent e)
        {
            // 获取当前正在运行的 CustomTrialExe 实例
            var currentTrial = CustomTrialExe.CurrentInstance;
            if (currentTrial == null)
                return;

            // 获取当前试炼的配置名（用于区分不同试炼）
            string configName = currentTrial.CurrentConfigName;
            if (string.IsNullOrEmpty(configName))
                return;

            // 获取已删除的节点索引列表
            var deletedNodes = currentTrial.GetDeletedNodeIndices();
            if (deletedNodes.Count == 0)
                return;

            // 将列表转换为逗号分隔的字符串
            string nodesStr = string.Join(",", deletedNodes);

            // 创建自定义 XML 节点
            XElement customNode = new("CustomTrialData",
                new XAttribute("ConfigName", configName),
                new XAttribute("Nodes", nodesStr));

            // 添加到存档根元素中
            e.Save.Add(customNode);
        }
    }
}