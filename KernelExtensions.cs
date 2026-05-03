using BepInEx;
using BepInEx.Hacknet;
using Hacknet;
using Hacknet.Extensions;
using HarmonyLib;
using KernelExtensions.Actions;
using KernelExtensions.AirCraft.Actions;
using KernelExtensions.AirCraft.Daemon;
using KernelExtensions.Config;
using KernelExtensions.Executables;
using KernelExtensions.Modules;
using KernelExtensions.Patches;
using KernelExtensions.Saving;
using KernelExtensions.Utility;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Pathfinder.Action;
using Pathfinder.Event;
using Pathfinder.Event.Loading;
using Pathfinder.Event.Saving;
using Pathfinder.Executable;
using Pathfinder.Replacements;      // 提供 SaveLoader 用于注册存档加载器
using System.Xml.Linq;
using System.Xml.Serialization;

namespace KernelExtensions
{
    [BepInPlugin(ModGUID, ModName, ModVer)]
    public class KernelExtensions : HacknetPlugin
    {
        public const string ModGUID = "com.LDTchara.KernelExtensions";
        public const string ModName = "KernelExtensions";
        public const string ModVer = "0.5.0";

        public static bool Debug = true;   // <--- 调试开关，测试时请改为 true

        public override bool Load()
        {
            // 1. 注册自定义试炼可执行程序
            ExecutableManager.RegisterExecutable<CustomTrialExe>("#CUSTOMTRIAL#");
            Console.WriteLine("[KernelExtensions] CustomTrial registered.");

            // 2. 注册各 Action
            ActionManager.RegisterAction<FailTrialAction>("FailTrial");
            Console.WriteLine("[KernelExtensions] FailTrial action registered.");
            ActionManager.RegisterAction<LaunchVMAttackAction>("LaunchVMAttack");
            Console.WriteLine("[KernelExtensions] LaunchVMAttack action registered.");
            ActionManager.RegisterAction<PlaySoundAction>("PlaySound");
            Console.WriteLine("[KernelExtensions] PlaySound action registered.");
            ActionManager.RegisterAction<TerminalFocusAction>("TerminalFocus");
            Console.WriteLine("[KernelExtensions] TerminalFocus action registered.");
            ActionManager.RegisterAction<TerminalWriteAction>("TerminalWrite");
            Console.WriteLine("[KernelExtensions] TerminalWrite action registered.");
            ActionManager.RegisterAction<TerminalTypeAction>("TerminalType");
            Console.WriteLine("[KernelExtensions] TerminalType action registered.");
            ActionManager.RegisterAction<RestoreCustomTrialNodesAction>("RestoreCustomTrialNodes");
            Console.WriteLine("[KernelExtensions] RestoreCustomTrialNodes action registered.");

            // 3. 注册各事件处理器
            EventManager<OSLoadedEvent>.AddHandler(OnOSLoaded_CheckVMInfection);
            Console.WriteLine("[KernelExtensions] OSLoaded event handler registered.");
            EventManager<SaveEvent>.AddHandler(OnSaveGame);
            Console.WriteLine("[KernelExtensions] Save event handler registered.");

            // 4. 注册自定义存档加载器（用于从存档中读取删除节点）
            SaveLoader.RegisterExecutor<CustomTrialSaveExecutor>("CustomTrialData");
            Console.WriteLine("[KernelExtensions] CustomTrialSaveExecutor registered.");

            // 4.5 飞机Daemon相关
            ActionManager.RegisterAction<HideAircraftOverlay>("HideAircraftOverlay");
            Console.WriteLine("[KernelExtensions] HideAircraftOverlay action registered.");
            ActionManager.RegisterAction<ShowAircraftOverlay>("ShowAircraftOverlay");
            Console.WriteLine("[KernelExtensions] ShowAircraftOverlay action registered.");
            Pathfinder.Daemon.DaemonManager.RegisterDaemon<FlightDaemon>();
            Console.WriteLine("[KernelExtensions] FlightDaemon registered.");
            ActionManager.RegisterAction<UploadAircraftSysFile>("UploadAircraftSysFile");
            Console.WriteLine("[KernelExtensions] UploadAircraftSysFile action registered.");
            ActionManager.RegisterAction<AttackAircraft>("AttackAircraft");
            Console.WriteLine("[KernelExtensions] AttackAircraft action registered.");

            // 5. 加载 Harmony 补丁
            var harmony = new Harmony("com.LDTchara.KernelExtensions.vmpatch");
            //harmony.PatchAll(typeof(CrashModuleVMAttackPatch).Assembly);
            // 也可以直接 PatchAll
            harmony.PatchAll();

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("[KernelExtensions] All is well ** SUCCESS!!");
            Console.ResetColor();
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

        private void OnOSLoaded_CheckVMInfection(OSLoadedEvent e)
        {
            OS os = e.Os;
            string flag = os.Flags.GetFlagStartingWith("Kernel_VMInfected_");

            // 以下是原有感染分支，也加入少量调试
            if (Debug) Log.LogDebug("Infection flag found: " + (flag ?? "null"));

            // 没有感染标记，直接返回
            if (flag == null)
            {
                return;
            }

            string configName = flag.Substring("Kernel_VMInfected_".Length);
            string configPath = Path.Combine(ExtensionLoader.ActiveExtensionInfo.FolderPath, "VMATK", configName + ".xml");

            if (!File.Exists(configPath))
            {
                if (Debug) Log.LogDebug("Config file not found at: " + configPath);
                os.Flags.RemoveFlag(flag);
                return;
            }

            VMAttackConfig config;
            try
            {
                var serializer = new XmlSerializer(typeof(VMAttackConfig));
                using var fs = new FileStream(configPath, FileMode.Open);
                config = (VMAttackConfig)serializer.Deserialize(fs);
            }
            catch (Exception ex)
            {
                if (Debug) Log.LogDebug("Failed to deserialize config: " + ex.Message);
                return;
            }

            VMInfectionManager.CurrentConfig = config;

            if (Debug) Log.LogDebug("Config loaded. Mode = " + config.Mode);

            if (config.Mode == RecoveryMode.FileDeletion)
            {
                string checkPath = Path.Combine(HostileHackerBreakinSequence.GetBaseDirectory(), config.CheckFilePath);
                if (!File.Exists(checkPath))
                {
                    // 播放成功音乐
                    if (!string.IsNullOrEmpty(config.SuccessMusic))
                    {
                        if (Debug) Log.LogDebug("Playing success music before reboot...");
                        string extRoot = ExtensionLoader.ActiveExtensionInfo?.FolderPath?.Replace('\\', '/');
                        string resolved = MusicPathResolver.ResolveMusicPath(config.SuccessMusic, extRoot);
                        MusicManager.loadAsCurrentSong(resolved);
                    }
                    string guideReadFlag = "Kernel_VMGuideRead_" + configName;
                    if (os.Flags.HasFlag(guideReadFlag))
                        os.Flags.RemoveFlag(guideReadFlag);
                    // 清理引导动作完成 Flag
                    string guideActionDoneFlag = "Kernel_VMGuideActionDone_" + configName;
                    if (os.Flags.HasFlag(guideActionDoneFlag))
                        os.Flags.RemoveFlag(guideActionDoneFlag);
                    os.Flags.RemoveFlag(flag);          // 移除感染 Flag
                    os.rebootThisComputer();           // 然后虚拟重启
                    return;
                }
            }
            else if (config.Mode == RecoveryMode.FileExists)
            {
                string checkPath = Path.Combine(HostileHackerBreakinSequence.GetBaseDirectory(), config.CheckFilePath);
                if (File.Exists(checkPath))
                {
                    if (!string.IsNullOrEmpty(config.SuccessMusic))
                    {
                        if (Debug) Log.LogDebug("Playing success music before reboot...");
                        string extRoot = ExtensionLoader.ActiveExtensionInfo?.FolderPath?.Replace('\\', '/');
                        string resolved = MusicPathResolver.ResolveMusicPath(config.SuccessMusic, extRoot);
                        MusicManager.loadAsCurrentSong(resolved);
                    }
                    string guideReadFlag = "Kernel_VMGuideRead_" + configName;
                    if (os.Flags.HasFlag(guideReadFlag))
                        os.Flags.RemoveFlag(guideReadFlag);
                    // 清理引导动作完成 Flag
                    string guideActionDoneFlag = "Kernel_VMGuideActionDone_" + configName;
                    if (os.Flags.HasFlag(guideActionDoneFlag))
                        os.Flags.RemoveFlag(guideActionDoneFlag);
                    os.Flags.RemoveFlag(flag);
                    os.rebootThisComputer();
                    return;
                }
            }

            // 继续触发崩溃
            os.rebootThisComputer();
        }
    }
}