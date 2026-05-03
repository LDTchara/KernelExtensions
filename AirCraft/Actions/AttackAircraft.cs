using System.Drawing.Text;
using Hacknet;
using KernelExtensions.AirCraft.Daemon;
using Pathfinder.Util;

public class AttackAircraft : Pathfinder.Action.PathfinderAction
{
    // 存储受攻击飞行器的节点ID（用于定位对应FlightDaemon）
    [XMLStorage]
    public string NodeID;

    // 控制攻击开始前的延迟时间：-1表示使用默认135秒，0表示立即，正数表示指定延迟秒数
    [XMLStorage]
    public float CrushDelay = 135f;

    // 属性：获取节点ID
    public string Nodeid => NodeID;

    // 触发器：执行攻击操作
    public override void Trigger(object os_obj)
    {
        OS os = (OS)os_obj;

        // 通过节点ID查找对应的Computer实例
        Computer c = Programs.getComputer(os, Nodeid);

        // 获取与该Computer关联的FlightDaemon实例
        FlightDaemon d = FlightDaemon.CompToDamons[c];

        // 从Computer的daemon列表中重新定位FlightDaemon（确保引用一致）
        foreach (var dx in c.daemons)
        {
            if (dx == d)
            {
                d = (FlightDaemon)dx;
                break;
            }
        }

        // 校验CrushDelay的合法性：只允许-1、0或正数
        if (CrushDelay < -1)
        {
            throw new Exception($"Error! Set {CrushDelay} to -1 -> Default || 0 -> Instant || X(>0) Delay then start");
        }

        // ---------- 1. 根据CrushDelay设置FlightDaemon的延迟时间 ----------
        if (CrushDelay == -1)
            d.H = 135f;      // 使用默认135秒
        else if (CrushDelay == 0)
            d.H = 0f;        // 无延迟，立即攻击
        else if (CrushDelay > 0)
            d.H = CrushDelay; // 使用指定的延迟时间

        // 开始执行攻击流程
        StartAttack(os, d);
    }

    // 核心攻击方法：植入并立即删除关键DLL以触发固件崩溃
    private void StartAttack(OS os, FlightDaemon d)
    {
        // 获取目标Computer
        Computer c = FlightDaemon.FlightIdToComputer[Nodeid];

        // 确保目标Computer中存在"FlightSystems"文件夹
        Folder f = c.files.root.searchForFolder("FlightSystems");
        if (f == null)
        {
            f = new Folder("FlightSystems");
            c.files.root.folders.Add(f);
        }

        // 检查是否已存在"747FlightOps.dll"文件，避免重复写入
        bool dllExists = false;
        for (int i = 0; i < f.files.Count; i++)
        {
            if (f.files[i].name == "747FlightOps.dll")
            {
                dllExists = true;
                break;
            }
        }

        // 若不存在，则植入恶意的DLL文件
        if (!dllExists)
        {
            FileEntry item = new(PortExploits.ValidAircraftOperatingDLL, "747FlightOps.dll");
            f.files.Add(item);
        }

        // 3. 立即删除该文件，触发Critical Firmware Failure（6秒后生效）
        for (int i = f.files.Count - 1; i >= 0; i--)   // 倒序遍历避免索引混乱
        {
            if (f.files[i].name == "747FlightOps.dll")
            {
                f.files.RemoveAt(i);
                break;
            }
        }

        // 启动固件重载过程（实际效果是：先经过CrushDelay延迟，再因缺失关键DLL而崩溃）
        d.StartReloadFirmware();
    }
}