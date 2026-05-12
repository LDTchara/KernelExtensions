using System.Linq;
using Hacknet;
using KernelExtensions.Daemons;
using Pathfinder.Action;
using Pathfinder.Util;

namespace KernelExtensions.Actions
{
    // 表示上传飞机系统文件到指定计算机或文件夹的可延迟路径查找动作
    public class UploadAircraftSysFile : DelayablePathfinderAction
    {
        // 目标节点的ID（如计算机ID）
        [XMLStorage]
        public string NodeID;

        // 目标路径（当目标节点不是飞行守护进程时使用）
        [XMLStorage]
        public string Path;

        // 触发动作：根据 NodeID 找到计算机，如果该计算机已运行飞行守护进程则直接写入 FlightSystems 文件夹 ，
        // 否则按路径查找文件夹并写入
        public override void Trigger(OS os)
        {
            // 通过 NodeID 获取计算机对象
            Computer c = Programs.getComputer(os, NodeID);

            // 如果 NodeID 不为空且计算机对象有效
            if (!string.IsNullOrEmpty(NodeID) && !(c == null))
            {
                // 检查该计算机是否已在飞行守护进程映射中（即已安装守护进程）
                if (FlightDaemon.CompToDaemons.ContainsKey(c))
                {
                    // 获取计算机的 FlightSystems 文件夹
                    Folder ff = c.files.root.searchForFolder("FlightSystems");
                    // 向该文件夹添加有效的飞机操作系统 DLL 文件
                    ff.files.Add(new FileEntry(PortExploits.ValidAircraftOperatingDLL, "747FlightOps.dll"));
                }
                else
                {
                    // 若计算机未安装守护进程，则按 Path 查找目标文件夹
                    Folder folderAtPath = Programs.getFolderAtPath(Path, os, c.files.root, returnsNullOnNoFind: true);

                    // 同样向目标文件夹添加飞机操作系统 DLL 文件
                    FileEntry item = new(PortExploits.ValidAircraftOperatingDLL, "747FlightOps.dll");
                    folderAtPath.files.Add(item);
                }
            }
            // 如果 NodeID 为空或计算机对象无效，则不做任何操作
        }
    }
}