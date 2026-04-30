using Hacknet;
using Pathfinder.Action;
using Pathfinder.Replacements;
using Pathfinder.Util.XML;
using System;
using System.IO;
using System.Xml;

namespace KernelExtensions.Utility
{
    public static class ActionHelper
    {
        /// <summary>
        /// 执行一个动作文件，与 CustomTrialExe 中的 ExecuteActionFile 逻辑一致。
        /// </summary>
        /// <param name="os">当前 OS 实例</param>
        /// <param name="actionFilePath">相对于扩展根目录的完整路径</param>
        /// <param name="extensionRoot">扩展根目录</param>
        public static void ExecuteActionFile(OS os, string actionFilePath, string extensionRoot)
        {
            if (string.IsNullOrEmpty(actionFilePath)) return;

            string fullPath = Path.Combine(extensionRoot, actionFilePath).Replace('\\', '/');
            if (!File.Exists(fullPath))
            {
                os.write($"Action file not found: {actionFilePath}");
                return;
            }

            try
            {
                var executor = new EventExecutor(fullPath, true);

                // 注册 ConditionalActions 处理器
                executor.RegisterExecutor("ConditionalActions", (exec, info) =>
                {
                    var sets = ActionsLoader.LoadActionSets(info);
                    os.ConditionalActions.Actions.AddRange(sets.Actions);
                    if (!os.ConditionalActions.IsUpdating)
                        os.ConditionalActions.Update(0f, os);
                }, ParseOption.ParseInterior);

                // 注册 Actions 处理器（标准动作列表）
                executor.RegisterExecutor("Actions", (exec, info) =>
                {
                    foreach (var child in info.Children)
                    {
                        var action = ActionsLoader.ReadAction(child);
                        action?.Trigger(os);
                    }
                }, ParseOption.ParseInterior);

                // 解析文件
                if (!executor.TryParse(out var ex))
                {
                    // 回退到简单解析
                    using FileStream fs = new(fullPath, FileMode.Open);
                    using XmlReader reader = XmlReader.Create(fs);
                    reader.MoveToContent();
                    while (reader.Read())
                    {
                        if (reader.NodeType == XmlNodeType.Element)
                        {
                            SerializableAction action = SerializableAction.Deserialize(reader);
                            action?.Trigger(os);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"[KernelExtensions] ActionHelper: Error executing action file '{fullPath}': {e.Message}");
            }
        }
    }
}