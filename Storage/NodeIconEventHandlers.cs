using System.Linq;
using System.Xml.Linq;
using Hacknet;
using Pathfinder.Event.Loading;
using Pathfinder.Event.Saving;
using KernelExtensions.Storage;
using KernelExtensions.Actions;

namespace KernelExtensions.Storage
{
    public static class NodeIconEventHandlers
    {
        /// <summary>
        /// 每个节点保存时，将节点图标数据写入该节点的 XML 元素。
        /// 注册方式：EventManager&lt;SaveComputerEvent&gt;.AddHandler(NodeIconEventHandlers.OnSaveComputer);
        /// </summary>
        public static void OnSaveComputer(SaveComputerEvent e)
        {
            string id = e.Comp.idName;
            if (string.IsNullOrEmpty(id)) return;

            string org = NodeIconStorage.GetOrgIcon(id);
            string cur = NodeIconStorage.GetCurrIcon(id);
            if (org == null && cur == null) return;

            e.Element.Add(new XElement("NodeIcon",
                new XAttribute("org", org ?? ""),
                new XAttribute("curr", cur ?? "")));
        }

        /// <summary>
        /// 每个节点加载时，从该节点的 XML 中恢复图标数据。
        /// 注册方式：EventManager&lt;SaveComputerLoadedEvent&gt;.AddHandler(NodeIconEventHandlers.OnLoadComputer);
        /// </summary>
        public static void OnLoadComputer(SaveComputerLoadedEvent e)
        {
            var iconNode = e.Info.Children.FirstOrDefault(c => c.Name == "NodeIcon");
            if (iconNode == null) return;

            string org = iconNode.Attributes.TryGetValue("org", out var o) ? o : null;
            string cur = iconNode.Attributes.TryGetValue("curr", out var c) ? c : null;
            if (!string.IsNullOrEmpty(e.Comp.idName))
            {
                NodeIconStorage.LoadFromSave(e.Comp.idName, org, cur);
            }
        }

        /// <summary>
        /// 保存事件（全局，旧方案保留兼容性）。
        /// </summary>
        public static void OnSave(SaveEvent e)
        {
        }

        /// <summary>
        /// 加载事件：游戏加载完成后，恢复所有节点的 CurrIcon。
        /// </summary>
        public static void OnOSLoaded(OSLoadedEvent e)
        {
            OS os = e.Os;
            if (os?.netMap?.nodes == null) return;

            // 预加载 KE-Images.ini 中的纹理
            NodeIconPreloader.Load();

            var currIcons = NodeIconStorage.GetAllCurrIcons();
            foreach (var comp in os.netMap.nodes)
            {
                if (comp != null && currIcons.TryGetValue(comp.idName, out var iconKey)
                    && !string.IsNullOrEmpty(iconKey))
                {
                    SetNodeIconAction.ApplyIconFromStorage(comp, os);
                }
            }
        }
    }
}
