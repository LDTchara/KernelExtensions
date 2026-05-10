using BepInEx;
using BepInEx.Hacknet;
using Hacknet;
using Hacknet.Extensions;
using HarmonyLib;
using KernelExtensions.Actions;
using KernelExtensions.Config;
using KernelExtensions.Daemons;
using KernelExtensions.Executables;
using KernelExtensions.Modules;
using KernelExtensions.Patches;
using KernelExtensions.Saving;
using KernelExtensions.Utility;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Pathfinder.Action;
using Pathfinder.Daemon;
using Pathfinder.Event;
using Pathfinder.Event.Loading;
using Pathfinder.Event.Saving;
using Pathfinder.Executable;
using Pathfinder.Replacements;      // 提供 SaveLoader 用于注册存档加载器
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
        public const string ModVer = "0.6.0";
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
|⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠈⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀                                  Version-0.6.0                                    |
#===============================================================================================================#
";

        public static bool Debug = true;   // <--- 调试开关，测试时请改为 true

        public override bool Load()
        {
            // 1. 注册自定义试炼可执行程序
            ExecutableManager.RegisterExecutable<CustomTrialExe>("#CUSTOMTRIAL#");
            Console.WriteLine("[KernelExtensions] CustomTrial registered.");

            // 2. 注册各 Action
            Console.WriteLine("[KernelExtensions] Registering actions...");
            ActionManager.RegisterAction<FailTrialAction>("FailTrial");
            Log.LogDebug("[KernelExtensions] FailTrial action registered.");
            ActionManager.RegisterAction<LaunchVMAttackAction>("LaunchVMAttack");
            Log.LogDebug("[KernelExtensions] LaunchVMAttack action registered.");
            ActionManager.RegisterAction<PlaySoundAction>("PlaySound");
            Log.LogDebug("[KernelExtensions] PlaySound action registered.");
            ActionManager.RegisterAction<TerminalFocusAction>("TerminalFocus");
            Log.LogDebug("[KernelExtensions] TerminalFocus action registered.");
            ActionManager.RegisterAction<TerminalWriteAction>("TerminalWrite");
            Log.LogDebug("[KernelExtensions] TerminalWrite action registered.");
            ActionManager.RegisterAction<TerminalTypeAction>("TerminalType");
            Log.LogDebug("[KernelExtensions] TerminalType action registered.");
            ActionManager.RegisterAction<RenameNodeAction>("RenameNode");
            Log.LogDebug("[KernelExtensions] RenameNode action registered.");
            ActionManager.RegisterAction<RestoreCustomTrialNodesAction>("RestoreCustomTrialNodes");
            Log.LogDebug("[KernelExtensions] RestoreCustomTrialNodes action registered.");

            // 3. 注册各事件处理器
            Console.WriteLine("[KernelExtensions] Registering event handlers...");
            EventManager<OSLoadedEvent>.AddHandler(OnOSLoaded_CheckVMInfection);
            Log.LogDebug("[KernelExtensions] OSLoaded event handler registered.");
            EventManager<SaveEvent>.AddHandler(OnSaveGame);
            Log.LogDebug("[KernelExtensions] Save event handler registered.");

            // 4. 注册自定义存档加载器（用于从存档中读取删除节点）
            Console.WriteLine("[KernelExtensions] Registering save loaders...");
            SaveLoader.RegisterExecutor<CustomTrialSaveExecutor>("CustomTrialData");
            Log.LogDebug("[KernelExtensions] CustomTrialSaveExecutor registered.");

            // 4.5 飞机Daemon相关
            Console.WriteLine("[KernelExtensions] Registering aircraft-related actions and daemons...");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("[KernelExtensions] Thanks for April_Crystal");
            Console.ResetColor();
            ActionManager.RegisterAction<HideAircraftOverlay>("HideAircraftOverlay");
            Log.LogDebug("[KernelExtensions] HideAircraftOverlay action registered.");
            ActionManager.RegisterAction<ShowAircraftOverlay>("ShowAircraftOverlay");
            Log.LogDebug("[KernelExtensions] ShowAircraftOverlay action registered.");
            DaemonManager.RegisterDaemon<FlightDaemon>();
            Log.LogDebug("[KernelExtensions] FlightDaemon registered.");
            ActionManager.RegisterAction<UploadAircraftSysFile>("UploadAircraftSysFile");
            Log.LogDebug("[KernelExtensions] UploadAircraftSysFile action registered.");
            ActionManager.RegisterAction<AttackAircraft>("AttackAircraft");
            Log.LogDebug("[KernelExtensions] AttackAircraft action registered.");

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
    }
}