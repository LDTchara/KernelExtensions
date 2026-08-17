using BepInEx;
using BepInEx.Hacknet;
using Hacknet;
using Hacknet.Extensions;
using HarmonyLib;
using KernelExtensions.Actions;
using KernelExtensions.Actions.Aircraft;
using KernelExtensions.Actions.CustomTrial;
using KernelExtensions.Actions.PhaseSwift;
using KernelExtensions.Actions.VMAttack;
using KernelExtensions.Config;
using KernelExtensions.Daemons;
using KernelExtensions.Executables;
using KernelExtensions.Modules;
using KernelExtensions.Saving;
using KernelExtensions.Storage;
using KernelExtensions.Utility;
using Pathfinder.Action;
using Pathfinder.Daemon;
using Pathfinder.Event;
using Pathfinder.Event.Loading;
using Pathfinder.Event.Saving;
using Pathfinder.Executable;
using Pathfinder.Replacements;      // 提供 SaveLoader 用于注册存档加载器
using Pathfinder.Util.XML;          // 提供 ParseOption
using System.Runtime.InteropServices;
using System.Text;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace KernelExtensions
{
    [BepInPlugin(ModGUID, ModName, ModVer)]
    public class KernelExtensions : HacknetPlugin
    {
        public const string ModGUID = "com.LDTchara.KernelExtensions";
        public const string ModName = "KernelExtensions";
        public const string ModVer = "0.7.0";
        // 在类体顶部增加静态字段（与已有的 harmony 变量合并）
        private static Harmony _harmony;
        string KEArt = $@"
#===============================================================================================================#
|⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀                                                                                   |
|⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⢸⡿⠛⠛⠷⢶⣤⣄⠀⠀⠀⠀⠀⠀⠀ ██╗  ██╗███████╗██████╗ ███╗   ██╗███████╗██╗                                     |
|⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⣀⣀⣼⣷⣤⣀⡀⠀⠈⠙⠿⣦⡀⠀⠀⠀⠀ ██║ ██╔╝██╔════╝██╔══██╗████╗  ██║██╔════╝██║                                     |
|⠀⠀⠀⠀⠀⠀⠀⠀⣠⣶⣿⣿⣿⣿⣿⣿⣿⣿⣷⣦⡀⠀⠈⢻⣆⠀⠀⠀ █████╔╝ █████╗  ██████╔╝██╔██╗ ██║█████╗  ██║                                     |
|⠀⠀⠀⠀⠀⠀⢠⣾⣿⣿⡿⠋⠉⠁⠀⠈⠉⠻⢿⣿⣿⣦⠀⠀⠹⣧⠀⠀ ██╔═██╗ ██╔══╝  ██╔══██╗██║╚██╗██║██╔══╝  ██║                                     |
|⠀⠀⠀⠀⠀⢠⣿⣿⣿⠋⠀⣠⡶⠿⠿⢿⣶⣄⠀⠹⣿⣿⣧⠀⠀⠙⠀⠀ ██║  ██╗███████╗██║  ██║██║ ╚████║███████╗███████╗                                |
|⠀⠀⣀⣀⣀⣼⣿⣿⡇⠀⣸⡏⠁⠀⠀⠀⠈⣿⡆⠀⢹⣿⣿⡇⠀⢠⠀⠀ ╚═╝  ╚═╝╚══════╝╚═╝  ╚═╝╚═╝  ╚═══╝╚══════╝╚══════╝                                |
|⠀⠀⣿⡟⠛⢿⣿⣿⡆⠀⢿⡇⠀⠀⠀⠀⠀⣽⡇⠀⢸⣿⣿⡇⠀⠀⠀⠀ ███████╗██╗  ██╗████████╗███████╗███╗   ██╗███████╗██╗ ██████╗ ███╗   ██╗███████╗ |
|⠀⠀⢻⡇⠀⠸⣿⣿⣧⡀⠈⠻⣦⣄⣀⣠⣾⠟⠀⢠⣿⣿⡿⠀⠀⠘⠀⠀ ██╔════╝╚██╗██╔╝╚══██╔══╝██╔════╝████╗  ██║██╔════╝██║██╔═══██╗████╗  ██║██╔════╝ |
|⠀⠀⠀⢿⡄⠀⠹⣿⣿⣷⣄⡀⠀⠉⠉⠉⠀⣀⣴⣿⣿⡿⠃⠀⢠⡄⠀⠀ █████╗   ╚███╔╝    ██║   █████╗  ██╔██╗ ██║███████╗██║██║   ██║██╔██╗ ██║███████╗ |
|⠀⠀⠀⠈⢿⣄⠀⠈⠻⣿⣿⣿⣿⣶⣶⣶⣿⣿⣿⡿⠋⠀⠀⣰⡟⠁⠀⠀ ██╔══╝   ██╔██╗    ██║   ██╔══╝  ██║╚██╗██║╚════██║██║██║   ██║██║╚██╗██║╚════██║ |
|⠀⠀⠀⠀⠀⠙⢷⣄⡀⠀⠉⠛⠻⠿⠿⠿⠛⠋⠉⠀⢀⣠⡾⠋⠀⠀⠀⠀ ███████╗██╔╝ ██╗   ██║   ███████╗██║ ╚████║███████║██║╚██████╔╝██║ ╚████║███████║ |
|⠀⠀⠀⠀⠀⠀⠀⠉⠻⢶⣦⣄⠀⠀⠀⠀⠀⠀⢠⡶⠛⠉⠀⠀⠀⠀⠀⠀ ╚══════╝╚═╝  ╚═╝   ╚═╝   ╚══════╝╚═╝  ╚═══╝╚══════╝╚═╝ ╚═════╝ ╚═╝  ╚═══╝╚══════╝ |
|⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠈⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀                                  Version-0.7.0                                    |
#===============================================================================================================#
";


        public override bool Load()
        {
            // 0. 绑定 BepInEx 配置
            // 1. 注册自定义可执行程序
            KELog.Init();
            Console.WriteLine("[KernelExtensions] Registering executables...");
            ExecutableManager.RegisterExecutable<CustomTrialExe>("#CUSTOMTRIAL#");
            KELog.Info("CustomTrial registered.");
            ExecutableManager.RegisterExecutable<PhaseSwiftExe>("#PHASESWIFT#");
            KELog.Info("PhaseSwift registered.");
            ExecutableManager.RegisterExecutable<EffectsPlayerExe>("#EFFECTS#"); 
            KELog.Info("EffectsPlayer registered.");
            ExecutableManager.RegisterExecutable<WPTEST>("#WPTEST#");
            KELog.Info("WPTEST registered.");

            // 2. 注册各 Action
            Console.WriteLine("[KernelExtensions] Registering actions...");
            ActionManager.RegisterAction<FailTrialAction>("FailTrial");
            KELog.Info("FailTrial action registered.");
            ActionManager.RegisterAction<LaunchVMAttackAction>("LaunchVMAttack");
            KELog.Info("LaunchVMAttack action registered.");
            ActionManager.RegisterAction<PlaySoundAction>("PlaySound");
            KELog.Info("PlaySound action registered.");

            ActionManager.RegisterAction<StartScreenBleedEffectWCCAction>("StartScreenBleedEffectWCC");
            KELog.Info("StartScreenBleedEffectWCC action registered.");

            ActionManager.RegisterAction<PhaseSwiftSceneAction>("PhaseSwiftScene");
            KELog.Info("PhaseSwiftScene action registered.");
            ActionManager.RegisterAction<PhaseSwiftInitAction>("PhaseSwiftInit");
            KELog.Info("PhaseSwiftInit action registered.");
            ActionManager.RegisterAction<PhaseSwiftStopAction>("PhaseSwiftStop");
            KELog.Info("PhaseSwiftStop action registered.");
            ActionManager.RegisterAction<PhaseSwiftFadeOutAction>("PhaseSwiftFadeOut");
            KELog.Info("PhaseSwiftFadeOut action registered.");
            ActionManager.RegisterAction<PhaseSwiftMusicAction>("PhaseSwiftMusic");
            ActionManager.RegisterAction<BlockNodeAction>("BlockNode");
            ActionManager.RegisterAction<UnblockNodeAction>("UnblockNode");
            KELog.Info("PhaseSwiftMusic action registered.");
            ActionManager.RegisterAction<SwitchThemeAction>("SwitchToThemeKeepLayout");
            KELog.Info("SwitchToThemeKeepLayout action registered.");
            ActionManager.RegisterAction<TerminalFocusAction>("TerminalFocus");
            KELog.Info("TerminalFocus action registered.");
            ActionManager.RegisterAction<TerminalWriteAction>("TerminalWrite");
            KELog.Info("TerminalWrite action registered.");
            ActionManager.RegisterAction<TerminalTypeAction>("TerminalType");
            KELog.Info("TerminalType action registered.");
            ActionManager.RegisterAction<RenameNodeAction>("RenameNode");
            KELog.Info("RenameNode action registered.");
            ActionManager.RegisterAction<RestoreCustomTrialNodesAction>("RestoreCustomTrialNodes");
            KELog.Info("RestoreCustomTrialNodes action registered.");

            // 2.5 注册节点图标 Action
            ActionManager.RegisterAction<SetNodeIconAction>("SetNodeIcon");
            KELog.Info("SetNodeIcon action registered.");

            // 3. 注册各事件处理器
            Console.WriteLine("[KernelExtensions] Registering event handlers...");
            EventManager<OSLoadedEvent>.AddHandler(OnOSLoaded_CheckVMInfection);
            KELog.Info("OSLoaded event handler registered.");
            EventManager<SaveComputerEvent>.AddHandler(NodeIconEventHandlers.OnSaveComputer);
            EventManager<SaveComputerLoadedEvent>.AddHandler(NodeIconEventHandlers.OnLoadComputer);
            KELog.Info("NodeIcon save/load event handlers registered.");
            EventManager<OSLoadedEvent>.AddHandler(NodeIconEventHandlers.OnOSLoaded);
            KELog.Info("NodeIcon OSLoaded handler registered.");
            EventManager<OSLoadedEvent>.AddHandler((e) => { try { KEConfigLoader.Load(); } catch { } });
            EventManager<OSLoadedEvent>.AddHandler(OnOSLoaded_AutoRestorePhaseSwift);
            KELog.Info("KEConfigLoader handler registered.");
            EventManager<SaveEvent>.AddHandler(OnSaveGame);
            KELog.Info("Save event handler registered.");

            // 4. 注册自定义存档加载器（用于从存档中读取删除节点）
            Console.WriteLine("[KernelExtensions] Registering save loaders...");
            SaveLoader.RegisterExecutor<CustomTrialSaveExecutor>("CustomTrialData");
            // ParseInterior：必须解析子元素（DiscoveredScene/OrigLink 等），否则读档时 Children 恒为空
            SaveLoader.RegisterExecutor<PhaseSwiftSaveExecutor>("PhaseSwiftData", ParseOption.ParseInterior);
            KELog.Info("CustomTrialSaveExecutor registered.");

            // 4.5 飞机Daemon相关
            Console.WriteLine("[KernelExtensions] Registering aircraft-related actions and daemons...");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("[KernelExtensions] Thanks for April_Crystal");
            Console.ResetColor();
            ActionManager.RegisterAction<HideAircraftOverlay>("HideAircraftOverlay");
            KELog.Info("HideAircraftOverlay action registered.");
            ActionManager.RegisterAction<ShowAircraftOverlay>("ShowAircraftOverlay");
            KELog.Info("ShowAircraftOverlay action registered.");
            DaemonManager.RegisterDaemon<FlightDaemon>();
            KELog.Info("FlightDaemon registered.");
            ActionManager.RegisterAction<UploadAircraftSysFile>("UploadAircraftSysFile");
            KELog.Info("UploadAircraftSysFile action registered.");
            ActionManager.RegisterAction<AttackAircraft>("AttackAircraft");
            KELog.Info("AttackAircraft action registered.");
            ActionManager.RegisterAction<FlashScreenAction>("FlashScreen");
            KELog.Info("FlashScreen action registered.");
            ActionManager.RegisterAction<ClockStartAction>("ClockStart");
            KELog.Info("ClockStart action registered.");
            ActionManager.RegisterAction<ClockStopAction>("ClockStop");
            KELog.Info("ClockStop action registered.");

            // 5. 加载 Harmony 补丁
            Console.WriteLine("[KernelExtensions] Applying Harmony patches...");
            _harmony = new Harmony("com.LDTchara.KernelExtensions");
            _harmony.PatchAll();

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("[KernelExtensions] All is well ** SUCCESS!!");
            Console.ResetColor();
            PrintGradientAscii(KEArt);
            return true;
        }

        public override bool Unload()
        {
            PhaseSwiftExe.CleanupAll();
            // 清理 IRC 日志静态列表（退出扩展时清空，避免残留到下一局）
            FileEntry.filenames?.Clear();
            FileEntry.fileData?.Clear();
            _harmony?.UnpatchSelf();
            _harmony = null;
            return base.Unload();
        }

        /// <summary>
        /// 在游戏保存存档时触发，将当前正在运行的试炼中已删除的节点列表
        /// 写入存档的自定义节点 <CustomTrialData> 中。
        /// </summary>
        private void OnSaveGame(SaveEvent e)
        {
            OS os = e.Os;

            // ========== CustomTrial 存档数据 ==========
            // 获取当前正在运行的 CustomTrialExe 实例
            var currentTrial = CustomTrialExe.CurrentInstance;
            if (currentTrial != null)
            {
                // 获取当前试炼的配置名（用于区分不同试炼）
                string configName = currentTrial.CurrentConfigName;
                if (!string.IsNullOrEmpty(configName))
                {
                    // 获取已删除的节点索引列表
                    var deletedNodes = currentTrial.GetDeletedNodeIndices();
                    if (deletedNodes.Count > 0)
                    {
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

            // ========== PhaseSwift 存档数据（9.6b） ==========
            // 只在 PS 运行时写入；未运行时不写（读档时无 PhaseSwift_ flag 也不会恢复）
            if (PhaseSwiftManager.IsRunning && PhaseSwiftManager.Config != null)
            {
                // 刷新内存中的发现状态 + admin 记录（切场景时才保存，存档前补一次）
                PhaseSwiftManager.RefreshPersistentState();

                // 配置名：从 flag PhaseSwift_{ConfigName} 解析
                string flag = os?.Flags.GetFlagStartingWith("PhaseSwift_");
                string psConfigName = flag?.Substring("PhaseSwift_".Length);
                if (string.IsNullOrEmpty(psConfigName)) psConfigName = "Default";

                XElement psNode = new("PhaseSwiftData",
                    new XAttribute("ConfigName", psConfigName),
                    new XAttribute("CurrentScene", PhaseSwiftManager.CurrentScene),
                    new XAttribute("MusicPhase", PhaseSwiftManager.CurrentMusicPhase),
                    new XAttribute("Theme", PhaseSwiftManager.DefaultTheme ?? ""));

                // 各场景已发现节点
                foreach (var kv in PhaseSwiftManager.GetSceneDiscoveredNodes())
                {
                    if (kv.Value == null || kv.Value.Count == 0) continue;
                    var sceneEl = new XElement("DiscoveredScene", new XAttribute("Index", kv.Key));
                    foreach (var id in kv.Value)
                        sceneEl.Add(new XElement("Node", id));
                    psNode.Add(sceneEl);
                }

                // 原始链接：int 索引 → 节点 ID（跨会话安全）
                foreach (var kv in PhaseSwiftManager.GetOriginalLinks())
                {
                    var targets = new System.Collections.Generic.List<string>();
                    foreach (var idx in kv.Value)
                    {
                        if (idx >= 0 && os != null && os.netMap != null && idx < os.netMap.nodes.Count
                            && os.netMap.nodes[idx] != null)
                            targets.Add(os.netMap.nodes[idx].idName);
                    }
                    if (targets.Count == 0) continue;
                    psNode.Add(new XElement("OrigLink",
                        new XAttribute("NodeId", kv.Key),
                        new XAttribute("Targets", string.Join(",", targets))));
                }

                // 运行时黑名单（9.8）
                foreach (var kv in PhaseSwiftManager.GetRuntimeBlockedNodes())
                {
                    if (kv.Value == null || kv.Value.Count == 0) continue;
                    var sceneEl = new XElement("RuntimeBlockedScene", new XAttribute("Index", kv.Key));
                    foreach (var id in kv.Value)
                        sceneEl.Add(new XElement("Node", id));
                    psNode.Add(sceneEl);
                }

                // 9.16 已废弃（2026-08-05）：Hacknet 密码机制下无法真正回收 admin，
                // AdminScene 不再写入存档（旧存档残留元素读档时忽略，无副作用）

                e.Save.Add(psNode);
            }
        }

        private void OnOSLoaded_CheckVMInfection(OSLoadedEvent e)
        {
            OS os = e.Os;
            string flag = os.Flags.GetFlagStartingWith("Kernel_VMInfected_");

            // 以下是原有感染分支，也加入少量调试
            if (KEConfigLoader.Debug) Log.LogDebug("Infection flag found: " + (flag ?? "null"));

            // 没有感染标记，直接返回
            if (flag == null)
            {
                return;
            }

            string configName = flag.Substring("Kernel_VMInfected_".Length);
            string configPath = Path.Combine(ExtensionLoader.ActiveExtensionInfo.FolderPath, "VMATK", configName + ".xml");

            if (!File.Exists(configPath))
            {
                if (KEConfigLoader.Debug) Log.LogDebug("Config file not found at: " + configPath);
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
                if (KEConfigLoader.Debug) Log.LogDebug("Failed to deserialize config: " + ex.Message);
                return;
            }

            VMInfectionManager.CurrentConfig = config;

            if (KEConfigLoader.Debug) Log.LogDebug("Config loaded. Mode = " + config.Mode);

            if (config.Mode == RecoveryMode.FileDeletion)
            {
                string checkPath = Path.Combine(HostileHackerBreakinSequence.GetBaseDirectory(), config.CheckFilePath);
                if (!File.Exists(checkPath))
                {
                    // 播放成功音乐
                    if (!string.IsNullOrEmpty(config.SuccessMusic))
                    {
                        if (KEConfigLoader.Debug) Log.LogDebug("Playing success music before reboot...");
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
                    // CheckFilePattern：文件内容必须与参考文件一致
                    bool contentMatch = true;
                    if (!string.IsNullOrEmpty(config.CheckFilePattern))
                    {
                        try
                        {
                            string extRoot = ExtensionLoader.ActiveExtensionInfo?.FolderPath?.Replace('\\', '/');
                            string refPath = System.IO.Path.Combine(extRoot, config.CheckFilePattern);
                            contentMatch = System.IO.File.Exists(refPath) && FilesMatch(checkPath, refPath);
                            if (KEConfigLoader.Debug)
                                KELog.Debug($"[VM] CheckFilePattern: comparing with {refPath} -> {(contentMatch ? "match" : "mismatch")}");
                        }
                        catch
                        {
                            contentMatch = false;
                        }
                    }
                    if (contentMatch)
                    {
                        // 播放成功音乐
                        if (!string.IsNullOrEmpty(config.SuccessMusic))
                        {
                            if (KEConfigLoader.Debug) Log.LogDebug("Playing success music before reboot...");
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
            }

            // 继续触发崩溃
            os.rebootThisComputer();
        }
        //神奇妙妙渐变色
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetStdHandle(int nStdHandle);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);
        private static bool EnableAnsiColors()
        {
            try
            {
                IntPtr handle = GetStdHandle(-11);
                uint mode;
                if (!GetConsoleMode(handle, out mode)) return false;
                const uint ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x0004;
                uint newMode = mode | ENABLE_VIRTUAL_TERMINAL_PROCESSING;
                return SetConsoleMode(handle, newMode);
            }
            catch { return false; }
        }
        /// <summary>
        /// HSL 转 RGB 辅助函数。
        /// </summary>
        /// <param name="h">色相 (0-360)</param>
        /// <param name="s">饱和度 (0-1)</param>
        /// <param name="l">亮度 (0-1)</param>
        /// <returns>(R, G, B) 元组，值范围 0-255</returns>
        private static (byte r, byte g, byte b) HslToRgb(double h, double s, double l)
        {
            double c = (1 - Math.Abs(2 * l - 1)) * s;
            double x = c * (1 - Math.Abs((h / 60) % 2 - 1));
            double m = l - c / 2;
            double r1, g1, b1;

            if (h < 60) { r1 = c; g1 = x; b1 = 0; }
            else if (h < 120) { r1 = x; g1 = c; b1 = 0; }
            else if (h < 180) { r1 = 0; g1 = c; b1 = x; }
            else if (h < 240) { r1 = 0; g1 = x; b1 = c; }
            else if (h < 300) { r1 = x; g1 = 0; b1 = c; }
            else { r1 = c; g1 = 0; b1 = x; }

            byte r = (byte)Math.Max(0, Math.Min(255, (r1 + m) * 255));
            byte g = (byte)Math.Max(0, Math.Min(255, (g1 + m) * 255));
            byte b = (byte)Math.Max(0, Math.Min(255, (b1 + m) * 255));
            return (r, g, b);
        }

        /// <summary>
        /// 输出彩虹渐变 ASCII 艺术字。
        /// </summary>
        public static void PrintGradientAscii(string art)
        {
            Console.OutputEncoding = Encoding.UTF8;
            bool ansi = EnableAnsiColors();

            string[] lines = art.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            int maxLen = 0;
            for (int i = 0; i < lines.Length; i++)
                if (lines[i].Length > maxLen) maxLen = lines[i].Length;

            for (int r = 0; r < lines.Length; r++)
            {
                string line = lines[r];
                for (int c = 0; c < line.Length; c++)
                {
                    // 将水平位置映射到 0-1
                    float t = maxLen <= 1 ? 0f : (float)c / (maxLen - 1);
                    // 色相从 0° 到 360° (红→红，经过彩虹)
                    double hue = t * 360.0;
                    // 饱和度 1.0，亮度 0.5 产生纯色
                    var (r_val, g_val, b_val) = HslToRgb(hue, 1.0, 0.5);

                    if (ansi)
                    {
                        Console.Write($"\x1b[38;2;{r_val};{g_val};{b_val}m{line[c]}");
                    }
                    else
                    {
                        // 非 ANSI 终端：使用黑底白字反转效果
                        Console.BackgroundColor = ConsoleColor.White;
                        Console.ForegroundColor = ConsoleColor.Black;
                        Console.Write(line[c]);
                        // 非 ANSI 的简单近似
                        /*
                        ConsoleColor col;
                        if (hue < 60) col = ConsoleColor.Red;
                        else if (hue < 120) col = ConsoleColor.DarkYellow;
                        else if (hue < 180) col = ConsoleColor.Green;
                        else if (hue < 240) col = ConsoleColor.Cyan;
                        else if (hue < 300) col = ConsoleColor.Blue;
                        else col = ConsoleColor.Magenta;
                        Console.ForegroundColor = col;
                        Console.Write(line[c]);
                        */
                    }
                }
                if (ansi) Console.Write("\x1b[0m");
                Console.ResetColor();
                Console.WriteLine();
            }
        }

        private static bool FilesMatch(string pathA, string pathB)
        {
            try
            {
                byte[] a = System.IO.File.ReadAllBytes(pathA);
                byte[] b = System.IO.File.ReadAllBytes(pathB);
                if (a.Length != b.Length) return false;
                for (int i = 0; i < a.Length; i++)
                    if (a[i] != b[i]) return false;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void OnOSLoaded_AutoRestorePhaseSwift(OSLoadedEvent e)
        {
            OS os = e.Os;
            string flag = os.Flags.GetFlagStartingWith("PhaseSwift_");
            if (flag == null) return;

            string configName = flag.Substring("PhaseSwift_".Length);
            var restore = PhaseSwiftManager.PendingRestore;

            PhaseSwiftManager.Initialize(os, configName);

            if (restore != null && restore.ConfigName == configName)
            {
                PhaseSwiftManager.OverrideOriginalLinks(restore.OriginalLinkIds, os);
                PhaseSwiftManager.RestorePersistentState(restore);
                PhaseSwiftManager.PendingRestore = null;
            }

            PhaseSwiftManager.Start(overrideScene: restore?.Scene);
        }
    }
}