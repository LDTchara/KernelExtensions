using Hacknet;
using KernelExtensions.Daemons;
using Pathfinder.Action;
using Pathfinder.Util;
using Pathfinder.Util.XML;

namespace KernelExtensions.Actions.Aircraft
{
    public class AttackAircraftAction : KEAction
    {
        [XMLStorage] public string NodeID;

        public float CrashDelay = 135f;

        public string Nodeid => NodeID;

        public override void Trigger(OS os)
        {
            Computer c = Programs.getComputer(os, Nodeid);
            if (c == null)
            {
                os.write($"ERROR: Computer '{Nodeid}' not found.");
                return;
            }

            // 1. 尝试从字典获取
            if (!FlightDaemon.CompToDaemons.TryGetValue(c, out FlightDaemon d))
            {
                // 2. 备用方案：从计算机的守护进程列表中查找
                d = c.daemons.OfType<FlightDaemon>().FirstOrDefault();
                if (d != null)
                    FlightDaemon.CompToDaemons[c] = d;   // 补全字典，下次直接找到
                else
                {
                    os.write($"ERROR: No FlightDaemon on computer '{Nodeid}'.");
                    return;
                }
            }

            // 确保引用与计算机守护进程列表中的实例一致（防止后续操作失效）
            foreach (var dx in c.daemons)
            {
                if (dx == d)
                {
                    d = (FlightDaemon)dx;
                    break;
                }
            }

            // 处理 CrashDelay —— 9.55 约定：负数 = 使用默认（Daemon 自身配置）；
            // NaN / Infinity 视为无效值，同样回退默认（此前会落入 throw，与「负数 = 默认」的约定相反）
            if (float.IsNaN(CrashDelay) || float.IsInfinity(CrashDelay) || CrashDelay < 0f)
                d.H = d.FallDuration; // 使用 Daemon 自身配置的默认坠落时长
            else if (CrashDelay == 0f)
            {
                d.CurrentAltitude = 0;
                d.CrashAircraft();
                return;
            }
            else
                d.H = CrashDelay; // 正数：覆盖 Daemon 的 FallDuration
            StartAttack(os, d);
        }

        private void StartAttack(OS os, FlightDaemon d)
        {
            // 目标计算机 = daemon 的宿主（不再查 FlightIdToComputer 字典，规避注册时机/裸索引器隐患）
            Computer c = d.comp;

            // 确保 FlightSystems 文件夹存在
            Folder f = c.files.root.searchForFolder("FlightSystems");
            if (f == null)
            {
                f = new Folder("FlightSystems");
                c.files.root.folders.Add(f);
            }

            // 检查是否已有该 DLL，避免重复写入
            bool dllExists = f.files.Any(file => file.name == "747FlightOps.dll");
            if (!dllExists)
            {
                FileEntry item = new(PortExploits.ValidAircraftOperatingDLL, "747FlightOps.dll");
                f.files.Add(item);
            }

            // 立即删除该文件，触发固件重载，6 秒后进入故障
            for (int i = f.files.Count - 1; i >= 0; i--)
            {
                if (f.files[i].name == "747FlightOps.dll")
                {
                    f.files.RemoveAt(i);
                    break;
                }
            }

            d.StartReloadFirmware();
        }
        // 手动读取 XML 属性，支持 FallDuration 和 CrashDelay 两个名称
        public override void LoadFromXml(ElementInfo info)
        {
            base.LoadFromXml(info);

            string delayStr = null;
            if (info.Attributes.TryGetValue("FallDuration", out delayStr) ||
                info.Attributes.TryGetValue("CrashDelay", out delayStr))
            {
                if (float.TryParse(delayStr, out float parsed))
                    CrashDelay = parsed;
            }
        }
    }
}