using BepInEx;
using Hacknet;
using Hacknet.Effects;
using Hacknet.Extensions;
using Hacknet.Gui;
using KernelExtensions.Config;
using KernelExtensions.Storage;   // 用于节点存储
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Pathfinder.Executable;
using Pathfinder.Util;
using Pathfinder.Replacements;
using Pathfinder.Util.XML;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;          // 用于反射获取私有字段
using System.Xml;
using System.Xml.Serialization;
using Module = Hacknet.Module;   // 消除与 System.Reflection.Module 的歧义

namespace KernelExtensions.Executables
{
    /// <summary>
    /// 自定义试炼程序，基于 Pathfinder 的 GameExecutable。
    /// 功能：旋转动画、可选特效、多阶段任务、逐字打印（支持%停顿）、全局/阶段计时器、失败重置（可配置）、颜色自定义、内存缩减等。
    /// 所有文件路径均相对于当前扩展根目录（Extension 文件夹）。
    /// </summary>
    public class CustomTrialExe : GameExecutable
    {
        public override void OnCompleteFailure() { }
        public override void OnCompleteError() { }
        // ---------- 静态实例（用于存档保存事件）----------
        public static CustomTrialExe CurrentInstance { get; private set; }

        // ---------- 状态机枚举 ----------
        private enum RunState
        {
            NotStarted,      // 显示开始按钮
            SpinningUp,      // 旋转进度条动画
            Flickering,      // UI 闪烁+节点消失（可选）
            WaitAfterDestruction,   // 节点摧毁后等待状态
            MailIconDestroy, // 邮件图标爆炸（可选）
            AssignMission,   // 逐字打印任务描述
            OnMission,       // 等待玩家完成任务
            Outro,           // 结束动画
            Exiting          // 退出
        }

        // ---------- 状态变量 ----------
        private RunState currentState = RunState.NotStarted;   // 当前运行状态
        private int currentPhaseIdx = -1;                      // 当前阶段索引（-1 表示未开始）
        private float stateTimer = 0f;                         // 当前状态计时器（秒）
        private TrialConfig config;                            // 加载的配置对象
        private bool trialSucceeded = false;                   // 试炼是否成功
        private bool outroTextCompleted = false;               // Outro 描述文本是否打印完毕

        // 音乐恢复相关
        private string originalMusicName = null;               // 进入试炼前的音乐名

        // 辅助属性：获取当前阶段配置（索引有效时）
        private PhaseConfig CurrentPhase => (currentPhaseIdx >= 0 && currentPhaseIdx < config.Phases.Count) ? config.Phases[currentPhaseIdx] : null;

        // ---------- 逐字打印相关 ----------
        private string currentDisplayText = "";                // 当前需要打印的描述文本
        private int charsRenderedSoFar = 0;                    // 已经打印的字符数

        // ---------- 任务相关 ----------
        private ActiveMission currentMission;                  // 当前加载的任务对象
        private bool missionCompleted = false;                 // 当前阶段任务是否完成
        private bool traceOverrideActive = false;              // 是否已注册追踪超时回调

        // ---------- 全局计时器 ----------
        private float globalTimerRemaining = 0f;               // 全局剩余时间（秒）
        private bool globalTimerActive = false;                // 全局计时器是否激活

        // ---------- 阶段计时器 ----------
        private float phaseTimerRemaining = 0f;                // 阶段剩余时间（秒）
        private bool phaseTimerActive = false;                 // 阶段计时器是否激活

        // ---------- 内存缩减相关 ----------
        private float ramReductionDelayTimer = 0f;             // 缩减延迟计时器
        private bool ramReductionDelaying = false;             // 是否正在延迟等待
        private bool ramReductionActive = false;               // 是否正在缩减内存
        private float ramReductionProgress = 0f;               // 缩减进度（0~1）
        private float currentRamCost = 190f;                   // 当前实际 ramCost（浮点，用于平滑）
        private const float TARGET_RAM_COST = 84f;             // 目标内存占用（固定不可配置）
        private float ramReductionStartValue = 190f;           // 缩减开始时的 ramCost
        private float timerStartY;

        // ---------- 节点删除持久化相关 ----------
        public string CurrentConfigName { get; private set; }  // 当前试炼配置名称（用于存储标识）
        private List<int> deletedNodeIndices = new();          // 本次试炼中已删除的节点索引列表

        // ---------- 特效相关 ----------
        private List<TraceKillExe.PointImpactEffect> impactEffects = new(); // 冲击波特效列表
        private float nodeRemovalTimer = 0f;                   // 节点移除计时器
        private float nodeRemovalInterval = 0f;                // 动态计算的节点摧毁间隔
        private Color originalTopBarIconsColor;                // 保存原始顶部栏图标颜色（用于特效恢复）
        private HexGridBackground backgroundEffect;            // 六边形网格背景
        private float finalScale = 1f;                         // 缩放相关
        private ExplodingUIElementEffect explosion = new();    // 爆炸特效
        private float mailLineTimer = 0f;                      
        private const float MAIL_LINE_INTERVAL = 1f / 60f;     // 径向线条每秒最多生成60次
        private SoundEffect breakSound;                        // 破碎音效
        private SoundEffect glowSound;                         // 完成时的发光音效
        private Texture2D circle;                              // 圆形纹理
        private Texture2D circleOutline;                       // 圆形轮廓纹理
        private bool hasStartedNodeDestruction = false;        // 是否已开始节点摧毁（用于计算一次间隔）
        private bool mailExploded = false;                     // 防止邮件爆炸重复执行
        private string outroDisplayText = "";                  // Outro 描述文本内容

        // ---------- 扩展根目录 ----------
        private string extensionRoot = null;                   // 当前扩展的根目录路径

        // ---------- 试炼锁定标志 ----------
        private bool trialLocked = false;                      // 是否因缺少 Flag 而锁定

        // ---------- 自定义颜色缓存 ----------
        private Color cachedBackgroundColor = Color.Transparent;   // 程序背景颜色（透明表示使用主题）
        private Color cachedGlobalTimerColor = Color.Transparent;  // 全局进度条颜色
        private Color cachedPhaseTimerColor = Color.Transparent;   // 阶段进度条颜色
        private Color cachedSpinUpColor = Color.Transparent;       // 旋转动画颜色

        /// <summary>
        /// 将配置中的音乐字符串转换为 MusicManager.transitionToSong 能识别的路径。
        /// 规则：
        /// 1. 如果字符串包含路径分隔符（/ 或 \），视为相对路径，基于扩展根目录解析并返回 "../Extensions/扩展名/路径"。
        /// 2. 如果是纯文件名（无路径分隔符）：
        ///    a. 首先检查扩展根目录下是否存在该文件（直接拼接），若存在则返回 "../Extensions/扩展名/文件名"。
        ///    b. 检查扩展内 Music 文件夹：检测 Extensions/当前扩展名/Music/文件名.ogg 是否存在，若存在返回 "../Extensions/扩展名/Music/文件名"。
        ///    c. 否则，检查是否为 DLC 音乐：检测 Content/DLC/Music/文件名.ogg 是否存在，若存在返回 "DLC/Music/文件名"。
        ///    d. 以上都不存在，作为原版音乐返回原字符串（MusicManager 会从 Content/Music/ 加载）。
        /// 就很...兜底。比起原版的逻辑多了个不用多填一个Music/的逻辑
        /// </summary>
        private string ResolveMusicPath(string musicPath)
        {
            if (string.IsNullOrEmpty(musicPath))
                return musicPath;

            // 已经是绝对路径或已带有扩展前缀，直接返回
            if (Path.IsPathRooted(musicPath) || musicPath.StartsWith("../Extensions/"))
                return musicPath;

            string extFolder = ExtensionLoader.ActiveExtensionInfo?.GetFoldersafeName();
            if (string.IsNullOrEmpty(extFolder))
                return musicPath;   // 无扩展信息时回退原版

            string extRoot = Path.Combine(Paths.GameRootPath, "Extensions", extFolder).Replace('\\', '/');

            // 本地函数：检查文件是否存在（自动尝试补全 .ogg）
            bool Exists(string directory, string fileName)
            {
                string fullPath = Path.Combine(directory, fileName);
                return File.Exists(fullPath) || File.Exists(fullPath + ".ogg");
            }

            // 去除 .ogg 扩展名（MusicManager 不需要）
            string StripOgg(string name) =>
                name.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase)
                    ? name.Substring(0, name.Length - 4)
                    : name;

            // 若包含路径分隔符 → 视为相对扩展根目录的路径
            if (musicPath.Contains('/') || musicPath.Contains('\\'))
            {
                string cleanPath = musicPath.Replace('\\', '/');
                return $"../Extensions/{extFolder}/{StripOgg(cleanPath)}";
            }

            // 纯文件名：按优先级查找
            if (Exists(extRoot, musicPath))
                return $"../Extensions/{extFolder}/{StripOgg(musicPath)}";

            if (Exists(Path.Combine(extRoot, "Music"), musicPath))
                return $"../Extensions/{extFolder}/Music/{StripOgg(musicPath)}";

            string dlcMusicDir = Path.Combine(Paths.GameRootPath, "Content", "DLC", "Music");
            if (Exists(dlcMusicDir, musicPath))
                return $"DLC/Music/{StripOgg(musicPath)}";

            // 回退原版音乐
            return musicPath;
        }

        // ---------- 构造函数 ----------
        public CustomTrialExe() : base()
        {
            this.ramCost = 190;                                // 内存占用（MB）
            this.IdentifierName = "CustomTrial";               // 程序内部标识
            this.name = "CustomTrial";                         // 程序显示名称
            this.CanBeKilled = true;                           // 初始允许被 kill 命令关闭
            this.ErrorReturn = null;                             // 抑制默认失败输出
            CurrentInstance = this;                            // 设置静态实例
        }

        // ---------- 初始化（类似 LoadContent） ----------
        public override void OnInitialize()
        {
            base.OnInitialize();

            // 获取当前扩展的根目录
            if (ExtensionLoader.ActiveExtensionInfo != null)
                extensionRoot = ExtensionLoader.ActiveExtensionInfo.FolderPath.Replace('\\', '/');

            // ---- 公共资源初始化（无论是否锁定都需要） ----
            // 初始化特效系统
            backgroundEffect = new HexGridBackground(os.content);
            explosion.Init(os.content);
            breakSound = os.content.Load<SoundEffect>("SFX/DoomShock");
            glowSound = os.content.Load<SoundEffect>("SFX/Ending/PorthackSpindown");

            // 加载纹理
            circle = os.content.Load<Texture2D>("Circle");
            circleOutline = os.content.Load<Texture2D>("CircleOutlineLarge");

            // 保存当前音乐名
            originalMusicName = MusicManager.currentSongName;

            // ---- 检测 CustomTrial_ 开头的 Flag ----
            string flag = os.Flags.GetFlagStartingWith("CustomTrial_");
            if (string.IsNullOrEmpty(flag))
            {
                trialLocked = true;
                Console.WriteLine("[KernelExtensions]CustomTrialExe: No CustomTrial_ flag found. Trial locked.");
                config = null;
                return; // 锁定，不再继续加载配置相关
            }

            Console.WriteLine($"[KernelExtensions]CustomTrialExe: Found flag '{flag}'");

            // 从 flag 中提取配置名
            string configName = flag.Substring("CustomTrial_".Length);
            if (string.IsNullOrEmpty(configName)) configName = "Default";
            CurrentConfigName = configName;

            // 加载配置
            LoadConfig(flag);

            if (config == null)
            {
                trialLocked = true;
                Console.WriteLine("[KernelExtensions]CustomTrialExe: Failed to load trial config.");
                return;
            }

            // 以下为仅当配置存在时才需要的初始化
            // 设置程序显示名称
            if (!string.IsNullOrEmpty(config.ProgramName))
            {
                this.IdentifierName = config.ProgramName;
                this.name = config.ProgramName;
            }

            // 解析自定义颜色
            cachedBackgroundColor = ParseColor(config.BackgroundColor);
            cachedGlobalTimerColor = ParseColor(config.GlobalTimerColor);
            cachedPhaseTimerColor = ParseColor(config.PhaseTimerColor);
            cachedSpinUpColor = ParseColor(config.SpinUpColor);

            // 保存原始顶部栏图标颜色
            originalTopBarIconsColor = os.topBarIconsColor;

            // 播放启动音乐
            if (!string.IsNullOrEmpty(config.StartMusic))
                MusicManager.transitionToSong(ResolveMusicPath(config.StartMusic));

            // 应用持久化删除的节点
            ApplyPersistedDeletedNodes();
        }

        /// <summary>
        /// 将相对路径转换为基于扩展根目录的绝对路径。
        /// 如果扩展根目录不存在（模组作为全局插件运行），则返回 null 并输出错误。
        /// </summary>
        private string ResolvePath(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath)) return null;
            if (Path.IsPathRooted(relativePath)) return relativePath; // 绝对路径直接返回

            if (string.IsNullOrEmpty(extensionRoot))
            {
                Console.WriteLine("[KernelExtensions]CustomTrialExe: Error: No extension root found. This mod must be run as part of an extension.");
                return null;
            }
            return Path.Combine(extensionRoot, relativePath).Replace('\\', '/');
        }

        /// <summary>
        /// 根据 Flag 加载对应的试炼配置 XML。
        /// </summary>
        private void LoadConfig(string flag)
        {
            // 从 Flag 中提取配置文件名（例如 "CustomTrial_MyTrial" → "MyTrial"）
            string configName = "Default";
            if (!string.IsNullOrEmpty(flag) && flag.StartsWith("CustomTrial_"))
            {
                configName = flag.Substring("CustomTrial_".Length);
                if (string.IsNullOrEmpty(configName)) configName = "Default";
            }
            CurrentConfigName = configName;  // 保存配置名

            // 配置文件必须位于扩展根目录下的 Trial 文件夹中
            if (string.IsNullOrEmpty(extensionRoot))
            {
                Console.WriteLine("[KernelExtensions]CustomTrialExe: Error: Cannot locate trial config because no extension root is available.");
                isExiting = true;
                return;
            }

            string configPath = Path.Combine(extensionRoot, "Trial", configName + ".xml").Replace('\\', '/');
            if (!File.Exists(configPath))
            {
                Console.WriteLine($"[KernelExtensions]CustomTrialExe: Error: Trial config '{configName}.xml' not found at '{configPath}'. Please ensure the file exists and the flag '{flag}' is correct.");
                isExiting = true;
                return;
            }

            // 反序列化 XML 为 TrialConfig 对象
            try
            {
                XmlSerializer serializer = new(typeof(TrialConfig));
                using FileStream fs = new(configPath, FileMode.Open);
                config = (TrialConfig)serializer.Deserialize(fs);
            }
            catch (Exception e)
            {
                Console.WriteLine($"[KernelExtensions]CustomTrialExe: Error loading config: {e.Message}");
                isExiting = true;
            }
        }

        /// <summary>
        /// 应用持久化存储的删除节点列表（从 CustomTrialNodeStorage 中读取），
        /// 将这些节点从 visibleNodes 中移除，并同步到本地的 deletedNodeIndices。
        /// </summary>
        private void ApplyPersistedDeletedNodes()
        {
            if (string.IsNullOrEmpty(CurrentConfigName)) return;
            var persistedNodes = CustomTrialNodeStorage.GetDeletedNodes(CurrentConfigName);
            if (persistedNodes.Count == 0) return;

            var visible = os.netMap.visibleNodes;
            foreach (int idx in persistedNodes)
            {
                if (visible.Contains(idx))
                    visible.Remove(idx);
                if (!deletedNodeIndices.Contains(idx))
                    deletedNodeIndices.Add(idx);
            }
        }

        /// <summary>
        /// 解析颜色字符串，支持：
        /// - 颜色名称（如 "Red", "Blue"）
        /// - 十六进制（如 "#FF0000", "#0F0"）
        /// - 好像还有彩虹色？
        /// - 其他无效值返回 Color.Transparent（表示使用主题色）
        /// </summary>
        private Color ParseColor(string colorStr)
        {
            if (string.IsNullOrEmpty(colorStr)) return Color.Transparent;

            // 彩蛋：LDTchara → 动态彩虹色
            if (colorStr.Equals("LDTchara", StringComparison.OrdinalIgnoreCase))
            {
                float hue = (float)(OS.currentElapsedTime * 0.1) % 1.0f;
                return HSVToColor(hue, 1.0f, 1.0f);
            }

            // 尝试使用 XNA 的颜色转换器
            try
            {
                var converter = new Microsoft.Xna.Framework.Design.ColorConverter();
                return (Color)converter.ConvertFromString(colorStr);
            }
            catch
            {
                // 手动解析十六进制颜色（支持 #RRGGBB 和 #RGB）
                if (colorStr.StartsWith("#"))
                {
                    try
                    {
                        string hex = colorStr.Substring(1);
                        if (hex.Length == 6)
                        {
                            byte r = byte.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
                            byte g = byte.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
                            byte b = byte.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
                            return new Color(r, g, b);
                        }
                        else if (hex.Length == 3)
                        {
                            byte r = byte.Parse(hex[0].ToString() + hex[0], System.Globalization.NumberStyles.HexNumber);
                            byte g = byte.Parse(hex[1].ToString() + hex[1], System.Globalization.NumberStyles.HexNumber);
                            byte b = byte.Parse(hex[2].ToString() + hex[2], System.Globalization.NumberStyles.HexNumber);
                            return new Color(r, g, b);
                        }
                    }
                    catch { }
                }
                // 解析失败返回透明，后续会使用默认主题色
                return Color.Transparent;
            }
        }

        private void AddRadialMailLine()
        {
            SFX.AddRadialLine(
                os.mailicon.pos + new Vector2(20f, 10f),
                (float)(3.141592653589793 + (double)Utils.rand(3.1415927f)),
                600f + Utils.randm(300f),
                800f,
                500f,
                200f + Utils.randm(400f),
                0.35f,
                Color.Lerp(Utils.makeColor(100, 0, 0, byte.MaxValue), Utils.AddativeRed, Utils.randm(1f)),
                3f,
                false
            );
        }

        /// <summary>
        /// HSV 转 RGB 辅助方法
        /// </summary>
        private Color HSVToColor(float hue, float saturation, float value)
        {
            int hi = (int)(hue * 6) % 6;
            float f = hue * 6 - hi;
            float p = value * (1 - saturation);
            float q = value * (1 - f * saturation);
            float t = value * (1 - (1 - f) * saturation);
            float r, g, b;
            switch (hi)
            {
                case 0: r = value; g = t; b = p; break;
                case 1: r = q; g = value; b = p; break;
                case 2: r = p; g = value; b = t; break;
                case 3: r = p; g = q; b = value; break;
                case 4: r = t; g = p; b = value; break;
                default: r = value; g = p; b = q; break;
            }
            return new Color(r, g, b);
        }

        /// <summary>
        /// 根据配置切换主题（使用 EffectsUpdater.StartThemeSwitch）
        /// </summary>
        private void ApplyThemeSwitch()
        {
            if (string.IsNullOrEmpty(config.ThemeToSwitch))
                return;

            // 尝试解析为主题枚举
            if (Enum.TryParse<OSTheme>(config.ThemeToSwitch, true, out OSTheme themeEnum))
            {
                // 预设主题
                os.EffectsUpdater.StartThemeSwitch(config.ThemeFlickerDuration, themeEnum, os, null);
            }
            else
            {
                // 自定义主题文件路径
                os.EffectsUpdater.StartThemeSwitch(config.ThemeFlickerDuration, OSTheme.Custom, os, config.ThemeToSwitch);
            }
            // 主题切换后，重新保存顶部栏颜色（因为顶栏颜色可能已改变）
            originalTopBarIconsColor = os.topBarIconsColor;
        }

        // ---------- 每帧更新 ----------
        public override void Update(float t)
        {
            base.Update(t);
            if (config == null) return; // 锁定或加载失败时无动作

            // 全局计时器更新（从点击按钮后开始）
            if (globalTimerActive && !isExiting && currentState != RunState.Outro)
            {
                globalTimerRemaining -= t;
                if (globalTimerRemaining <= 0)
                {
                    OnGlobalTimeout();
                    return;
                }
            }

            // 内存缩减逻辑
            if (ramReductionDelaying)
            {
                ramReductionDelayTimer -= t;
                if (ramReductionDelayTimer <= 0f)
                {
                    ramReductionDelaying = false;
                    ramReductionActive = true;
                    ramReductionProgress = 0f;
                    ramReductionStartValue = currentRamCost;
                }
            }
            if (ramReductionActive)
            {
                // 确定目标 ramCost：动态缩减模式 or 固定目标
                int targetRam;
                if (config.DynamicRamReduction)
                {
                    targetRam = CalculateRequiredRamCost();
                }
                else
                {
                    targetRam = (int)TARGET_RAM_COST; // 固定目标 84
                }

                // 检查是否已经到达目标
                if (Math.Abs(currentRamCost - targetRam) < 0.5f && ramReductionProgress >= 1f)
                {
                    currentRamCost = targetRam;
                    ramReductionActive = false;
                }
                else
                {
                    // 当目标值发生变化时，重置插值起始点以保证平滑
                    if (Math.Abs(targetRam - ramReductionStartValue) > 0.1f)
                    {
                        // 如果目标变化较大，重新设置起始值和进度
                        if (Math.Abs(targetRam - currentRamCost) > 5f)
                        {
                            ramReductionStartValue = currentRamCost;
                            ramReductionProgress = 0f;
                        }
                    }

                    // 递增进度
                    ramReductionProgress += t / config.RamReductionDuration;
                    if (ramReductionProgress >= 1f)
                        ramReductionProgress = 1f;

                    // 线性插值
                    currentRamCost = MathHelper.Lerp(ramReductionStartValue, targetRam, ramReductionProgress);
                }

                this.ramCost = (int)currentRamCost;
            }

            // 当缩减完成后，保持 finalScale 同步
            if (!ramReductionActive && !ramReductionDelaying)
            {
                finalScale = currentRamCost / 190f;
            }

            stateTimer += t; // 当前状态计时器累加

            switch (currentState)
            {
                case RunState.NotStarted:
                    break;
                case RunState.SpinningUp:
                    if (stateTimer >= config.SpinUpDuration)
                        TransitionToNextState();
                    break;
                case RunState.Flickering:
                    UpdateFlickering(t);
                    if (stateTimer >= config.FlickeringDuration)
                        TransitionToNextState();
                    break;
                case RunState.WaitAfterDestruction:
                    if (stateTimer >= config.PostDestructionDelay)
                        TransitionToNextState();
                    break;
                case RunState.MailIconDestroy:
                    UpdateMailIconDestroy(t);
                    if (stateTimer >= config.MailIconDestroyDuration)
                        TransitionToNextState();
                    break;
                case RunState.AssignMission:
                    UpdateAssignMission(t);
                    break;
                case RunState.OnMission:
                    if (phaseTimerActive && !missionCompleted)
                    {
                        phaseTimerRemaining -= t;
                        if (phaseTimerRemaining <= 0)
                        {
                            OnPhaseTimeout();
                            return;
                        }
                    }
                    UpdateOnMission(t);
                    break;
                case RunState.Outro:
                    if (!outroTextCompleted)
                    {
                        if (!string.IsNullOrEmpty(outroDisplayText))
                        {
                            charsRenderedSoFar = TextWriterTimed.WriteTextToTerminal(
                                outroDisplayText, os, 0.04f, 1f, 20f, stateTimer, charsRenderedSoFar);
                            if (charsRenderedSoFar >= outroDisplayText.Length)
                            {
                                outroTextCompleted = true;
                                stateTimer = 0f;   // 重置计时器，准备显示结果文字
                            }
                        }
                        else
                        {
                            outroTextCompleted = true;
                            stateTimer = 0f;
                        }
                    }
                    else
                    {
                        // 等待 2 秒显示结果文字
                        if (stateTimer >= 2f)
                        {
                            if (trialSucceeded)
                            {
                                // ---- 转连处理 ----
                                if (!string.IsNullOrEmpty(config.ConnectTarget))
                                {
                                    Computer targetComp = Programs.getComputer(os, config.ConnectTarget);
                                    if (targetComp != null)
                                    {
                                        if (config.StopMusicOnConnect)
                                            MusicManager.stop();
                                        os.runCommand("connect " + targetComp.ip);
                                    }
                                    else
                                    {
                                        Console.WriteLine($"[KernelExtensions]CustomTrialExe: ConnectTarget '{config.ConnectTarget}' not found.");
                                    }
                                }
                                ExecuteActionFile(config.OnComplete?.FilePath);
                                DeleteCurrentTrialFlag();
                                Result = CompletionResult.Success;
                            }
                            else
                            {
                                // 失败时已经执行过各自失败动作，直接设置 Result 退出
                                // Result = CompletionResult.Failure;
                                // 新：失败处理：直接将 Result 设为 Success 以抑制基类输出
                                Result = CompletionResult.Success;
                            }
                            // 恢复邮件图标显示
                            os.DisableEmailIcon = false;
                            currentState = RunState.Exiting;
                        }
                    }
                    break;
            }

            explosion.Update(t);
            UpdateImpactEffects(t);
        }

        // ---------- 状态转换核心逻辑 ----------
        private void TransitionToNextState()
        {
            stateTimer = 0f;
            switch (currentState)
            {
                case RunState.NotStarted:
                    this.CanBeKilled = false;
                    if (!string.IsNullOrEmpty(config.TrialStartMusic))
                        MusicManager.playSongImmediatley(ResolveMusicPath(config.TrialStartMusic));
                    ExecuteActionFile(config.OnStart?.FilePath);
                    currentState = RunState.SpinningUp;
                    break;

                case RunState.SpinningUp:
                    // 断开当前连接
                    os.execute("dc");
                    // 清空终端
                    os.execute("clear");
                    // 切换主题
                    ApplyThemeSwitch();
                    if (config.EnableFlickering)
                    {
                        // 禁用邮件图标交互
                        os.mailicon.isEnabled = false;
                        currentState = RunState.Flickering;
                    }    
                    else if (config.EnableMailIconDestroy)
                    {
                        // 进入邮件摧毁前同样禁用
                        os.mailicon.isEnabled = false;
                        currentState = RunState.MailIconDestroy;
                    }
                    else
                        StartNextPhase();
                    break;

                case RunState.Flickering:
                    // 关闭色差特效
                    PostProcessor.EndingSequenceFlashOutActive = false;
                    PostProcessor.EndingSequenceFlashOutPercentageComplete = 0f;
                    if (config.EnableMailIconDestroy)
                    {
                        if (config.PostDestructionDelay > 0)
                            currentState = RunState.WaitAfterDestruction;
                        else
                            currentState = RunState.MailIconDestroy;
                    }
                    else
                    {
                        // 如果没有邮件摧毁阶段，直接恢复交互
                        os.mailicon.isEnabled = true;
                        StartNextPhase();
                    }
                    break;

                case RunState.WaitAfterDestruction:
                    // 确保交互仍被禁用（可能已禁用，但显式设置更安全）
                    os.mailicon.isEnabled = false;
                    currentState = RunState.MailIconDestroy;
                    break;

                case RunState.MailIconDestroy:
                    // 关闭色差特效
                    PostProcessor.EndingSequenceFlashOutActive = false;
                    PostProcessor.EndingSequenceFlashOutPercentageComplete = 0f;
                    // 执行动画完成后的动作
                    ExecuteActionFile(config.OnAnimationComplete?.FilePath);
                    os.mailicon.isEnabled = true;        // 恢复点击（尽管稍后会被永久隐藏）
                    StartNextPhase();
                    mailExploded = false;   // 重置，以便下次进入该状态时正常爆炸
                    break;

                case RunState.AssignMission:
                    currentState = RunState.OnMission;
                    LoadCurrentMission();
                    missionCompleted = false;
                    if (CurrentPhase.Timeout > 0)
                    {
                        phaseTimerRemaining = CurrentPhase.Timeout;
                        phaseTimerActive = true;
                    }
                    else phaseTimerActive = false;
                    if (!string.IsNullOrEmpty(CurrentPhase.Music))
                        MusicManager.transitionToSong(ResolveMusicPath(CurrentPhase.Music));
                    if (CurrentPhase.Timeout > 0 && !traceOverrideActive)
                    {
                        os.traceCompleteOverrideAction += OnTraceTimeout;
                        traceOverrideActive = true;
                    }
                    break;

                case RunState.OnMission:
                    ExecuteActionFile(CurrentPhase?.OnComplete?.FilePath);
                    currentPhaseIdx++;
                    if (currentPhaseIdx >= config.Phases.Count)
                    {
                        trialSucceeded = true;
                        // 播放完成音效并切换音乐
                        globalTimerActive = false;  // 立即停用全局计时器
                        // 只有在启用完成聚焦特效时才播放完成音效，避免突兀
                        if (config.EnableTrialCompleteFocus)
                            glowSound?.Play();
                        MusicManager.transitionToSong("Music/Ambient/AmbientDrone_Clipped");
                        currentState = RunState.Outro;
                        stateTimer = 0f;
                        PrepareOutro();
                    }
                    else
                    {
                        currentState = RunState.AssignMission;
                        PrepareAssignMission();
                    }
                    break;
            }
        }

        /// <summary>
        /// 开始执行第一个阶段（从 AssignMission 开始）。
        /// 同时启动内存缩减延迟计时器（仅在第一次进入时）。
        /// </summary>
        private void StartNextPhase()
        {
            // 启动全局计时器（如果配置了且尚未激活）
            if (!globalTimerActive && config.GlobalTimeout > 0)
            {
                globalTimerRemaining = config.GlobalTimeout;
                globalTimerActive = true;
            }
            currentPhaseIdx = 0;
            currentState = RunState.AssignMission;
            PrepareAssignMission();

            // 启动内存缩减延迟（仅一次）
            if (!ramReductionDelaying && !ramReductionActive && config.RamReductionDuration > 0)
            {
                ramReductionDelayTimer = config.RamReductionDelay;
                ramReductionDelaying = true;
            }
        }

        /// <summary>
        /// 准备一个阶段的描述文本输出（重置逐字打印进度，并执行阶段开始动作）。
        /// </summary>
        private void PrepareAssignMission()
        {
            // 清空终端
            os.execute("clear");
            stateTimer = 0f;
            charsRenderedSoFar = 0;
            string rawText = CurrentPhase.DescriptionText;
            string resolvedPath = ResolvePath(rawText);
            if (resolvedPath != null && File.Exists(resolvedPath))
                currentDisplayText = Utils.readEntireFile(resolvedPath);
            else
                currentDisplayText = rawText ?? "";
            // 执行阶段开始时的动作
            ExecuteActionFile(CurrentPhase.OnPhaseStart?.FilePath);
        }

        private void PrepareOutro()
        {
            os.execute("clear");
            stateTimer = 0f;
            charsRenderedSoFar = 0;
            outroTextCompleted = false;

            if (!string.IsNullOrEmpty(config.OutroText))
            {
                string resolvedPath = ResolvePath(config.OutroText);
                if (resolvedPath != null && File.Exists(resolvedPath))
                    outroDisplayText = Utils.readEntireFile(resolvedPath);
                else
                    outroDisplayText = config.OutroText;
            }
            else
            {
                outroDisplayText = "";   // 无文本，直接跳过打印
            }
        }

        /// <summary>
        /// 逐字打印任务描述文本，支持 % 和 %% 停顿。
        /// </summary>
        private void UpdateAssignMission(float _)
        {
            if (string.IsNullOrEmpty(currentDisplayText))
            {
                TransitionToNextState();
                return;
            }

            charsRenderedSoFar = TextWriterTimed.WriteTextToTerminal(
                currentDisplayText, os, 0.04f, 1f, 20f, stateTimer, charsRenderedSoFar);
            if (charsRenderedSoFar >= currentDisplayText.Length)
                TransitionToNextState();
        }

        private void LoadCurrentMission()
        {
            if (!string.IsNullOrEmpty(CurrentPhase.MissionFile))
            {
                string missionPath = ResolvePath(CurrentPhase.MissionFile);
                if (missionPath == null || !File.Exists(missionPath))
                {
                    Console.WriteLine($"[KernelExtensions]CustomTrialExe: Error: Mission file not found: {CurrentPhase.MissionFile}");
                    currentMission = null;
                }
                else
                {
                    currentMission = (ActiveMission)ComputerLoader.readMission(missionPath);
                }
            }
            else currentMission = null;
        }

        private void UpdateOnMission(float _)
        {
            if (missionCompleted) return;
            if (currentMission != null && currentMission.isComplete(null))
            {
                missionCompleted = true;
                TransitionToNextState();
            }
        }

        // ---------- 失败处理 ----------
        private void OnPhaseTimeout()
        {
            if (!phaseTimerActive) return;       // 防止重复触发
            phaseTimerActive = false;            // 立即停用阶段计时器
            ExecuteActionFile(CurrentPhase?.OnFail?.FilePath);
            if (CurrentPhase.EnableResetOnFail)
                ResetCurrentPhase();
            else
            {
                trialSucceeded = false;
                // 进入 Outro 显示失败文字
                globalTimerActive = false;  // 立即停用全局计时器
                currentState = RunState.Outro;
                currentPhaseIdx = -1;           // 清空阶段
                stateTimer = 0f;
                outroTextCompleted = true;   // 跳过描述文本
                outroDisplayText = "";          // 清空描述文本
            }
        }

        private void OnGlobalTimeout()
        {
            if (!globalTimerActive) return;      // 防止重复触发
            globalTimerActive = false;           // 关键：立即停用全局计时器
            phaseTimerActive = false;
            ExecuteActionFile(config.OnGlobalFail?.FilePath);
            trialSucceeded = false;
            currentState = RunState.Outro;
            currentPhaseIdx = -1;           // 清空阶段
            stateTimer = 0f;
            outroTextCompleted = true;
            outroDisplayText = "";          // 清空描述文本
        }

        private void OnTraceTimeout()
        {
            if (!phaseTimerActive) return;
            phaseTimerActive = false;
            ExecuteActionFile(CurrentPhase?.OnFail?.FilePath);
            if (CurrentPhase.EnableResetOnFail)
                ResetCurrentPhase();
            else
            {
                trialSucceeded = false;
                globalTimerActive = false;  // 立即停用全局计时器
                currentState = RunState.Outro;
                currentPhaseIdx = -1;           // 清空阶段
                stateTimer = 0f;
                outroTextCompleted = true;
                outroDisplayText = "";          // 清空描述文本
            }
        }

        private void ResetCurrentPhase()
        {
            // 重置状态到 AssignMission，保持 currentPhaseIdx 不变
            currentState = RunState.AssignMission;
            // 重置计时器
            stateTimer = 0f;
            charsRenderedSoFar = 0;
            // 原始描述文本
            string rawDesc = CurrentPhase.DescriptionText;
            string resolvedDesc = ResolvePath(rawDesc);
            string descText = (resolvedDesc != null && File.Exists(resolvedDesc)) ? Utils.readEntireFile(resolvedDesc) : rawDesc ?? "";

            // 重置文本（可选）
            string resetRaw = CurrentPhase.ResetText;
            string resetText = "";
            if (!string.IsNullOrEmpty(resetRaw))
            {
                string resolvedReset = ResolvePath(resetRaw);
                resetText = (resolvedReset != null && File.Exists(resolvedReset)) ? Utils.readEntireFile(resolvedReset) : resetRaw;
                resetText += "\n\n";   // 与原始描述隔开
            }

            // 合并为当前要打印的文本
            currentDisplayText = resetText + descText;
            // 根据配置决定是否执行阶段开始动作
            if (CurrentPhase.ExecuteOnPhaseStartOnReset)
                ExecuteActionFile(CurrentPhase.OnPhaseStart?.FilePath);
            // 重置任务相关
            missionCompleted = false;
            // 重新加载任务
            LoadCurrentMission();
            // 重置阶段计时器
            if (CurrentPhase.Timeout > 0)
            {
                phaseTimerRemaining = CurrentPhase.Timeout;
                phaseTimerActive = true;
            }
            else phaseTimerActive = false;
        }

        private void DeleteCurrentTrialFlag()
        {
            string currentFlag = os.Flags.GetFlagStartingWith("CustomTrial_");
            if (!string.IsNullOrEmpty(currentFlag))
            {
                os.Flags.RemoveFlag(currentFlag);
                Console.WriteLine($"[KernelExtensions]CustomTrialExe: Removed flag: {currentFlag}");
            }
        }

        public override void OnComplete()
        {
            // 确保关闭色差特效
            PostProcessor.EndingSequenceFlashOutActive = false;
            PostProcessor.EndingSequenceFlashOutPercentageComplete = 0f;
            base.OnComplete();
            // 如果是在未开始试炼的状态下退出（例如玩家 kill 了程序），恢复音乐
            if (currentState == RunState.NotStarted && !trialLocked && !string.IsNullOrEmpty(originalMusicName))
            {
                MusicManager.transitionToSong(originalMusicName);
            }
            // 如果试炼失败，停止当前音乐
            if (!trialSucceeded && !trialLocked && config != null)
            {
                MusicManager.stop();
            }
        }

        // ---------- 动作执行 ----------
        private void ExecuteActionFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return;
            string fullPath = ResolvePath(filePath);
            if (fullPath == null || !File.Exists(fullPath))
            {
                os.write($"Action file not found: {filePath}");
                return;
            }

            try
            {
                // 使用 EventExecutor 解析 XML，它支持 ConditionalActions 等多种格式
                var executor = new EventExecutor(fullPath, true);

                // 注册 ConditionalActions 处理器
                executor.RegisterExecutor("ConditionalActions", (exec, info) =>
                {
                    var sets = ActionsLoader.LoadActionSets(info);
                    os.ConditionalActions.Actions.AddRange(sets.Actions);
                    // 立即触发一次更新，让 Instantly 条件立刻执行
                    if (!os.ConditionalActions.IsUpdating)
                        os.ConditionalActions.Update(0f, os);
                }, ParseOption.ParseInterior);

                // 注册 Actions 处理器（标准的动作列表）
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
                    // 如果解析失败，可能是旧格式（直接包含动作元素，无根包裹）
                    // 回退到原有的简单解析逻辑
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
                Console.WriteLine($"[KernelExtensions]CustomTrialExe: Error executing actions: {e.Message}");
            }
        }

        // ---------- 节点摧毁特效 ----------
        private void UpdateFlickering(float t)
        {
            if (!config.EnableFlickering) return;

            // 激活全屏色差特效，并随时间改变强度（模拟原版 EndingSequenceFlashOut）
            if (!PostProcessor.EndingSequenceFlashOutActive)
            {
                PostProcessor.EndingSequenceFlashOutActive = true;
                PostProcessor.EndingSequenceFlashOutPercentageComplete = 0f;
            }

            // 计算当前进度（0~1），用于控制特效强度
            float progress = Math.Min(1f, stateTimer / config.FlickeringDuration);
            PostProcessor.EndingSequenceFlashOutPercentageComplete = 1f - progress; // 原版是1f - num2，此处等效

            if (!hasStartedNodeDestruction && config.EnableNodeDestruction && os.netMap.visibleNodes.Count > 1)
            {
                int nodeCount = os.netMap.visibleNodes.Count;
                nodeRemovalInterval = (nodeCount > 0) ? config.FlickeringDuration / 2f / nodeCount : 0.5f;
                hasStartedNodeDestruction = true;
            }

            if (!config.EnableNodeDestruction) return;
            if (os.netMap.visibleNodes.Count <= 1) return;

            nodeRemovalTimer += t;
            if (nodeRemovalTimer >= nodeRemovalInterval)
            {
                nodeRemovalTimer -= nodeRemovalInterval;
                RemoveRandomNode();
            }
        }

        private void RemoveRandomNode()
        {
            var visible = os.netMap.visibleNodes;
            if (visible.Count <= 1) return;

            List<int> nonPlayerIndices = new();
            foreach (int idx in visible)
                if (os.netMap.nodes[idx] != os.thisComputer)
                    nonPlayerIndices.Add(idx);
            if (nonPlayerIndices.Count == 0) return;

            int randomIdx = Utils.random.Next(nonPlayerIndices.Count);
            int nodeIdx = nonPlayerIndices[randomIdx];
            var comp = os.netMap.nodes[nodeIdx];
            // ----- 新增：设置 adminIP 为自己，使 shell 失去权限自动关闭 -----
            comp.adminIP = comp.ip;
            Vector2 screenPos = comp.getScreenSpacePosition();

            // 记录删除的节点索引（持久化）
            if (!deletedNodeIndices.Contains(nodeIdx))
                deletedNodeIndices.Add(nodeIdx);
            CustomTrialNodeStorage.AddDeletedNode(CurrentConfigName, nodeIdx);

            // 添加主要冲击特效，缩放系数参考原版：安全等级 > 2 时增加 1f
            float scaleMod = 3f + ((comp.securityLevel > 2) ? 1f : 0f);
            impactEffects.Add(new TraceKillExe.PointImpactEffect
            {
                location = screenPos,
                scaleModifier = scaleMod,
                cne = new ConnectedNodeEffect(os, true),
                timeEnabled = 0f,
                HasHighlightCircle = true
            });

            // 安全等级 > 3 时添加额外的小圆圈
            if (comp.securityLevel > 3)
            {
                int extraCount = Math.Min(comp.securityLevel, 6);
                for (int i = 0; i < extraCount; i++)
                {
                    impactEffects.Add(new TraceKillExe.PointImpactEffect
                    {
                        location = screenPos,
                        scaleModifier = (float)Math.Min(8, i),
                        cne = new ConnectedNodeEffect(os, true),
                        timeEnabled = 0f,
                        HasHighlightCircle = false
                    });
                }
            }
            visible.Remove(nodeIdx);
        }

        // ---------- 邮件爆炸特效 ----------
        private void UpdateMailIconDestroy(float t)
        {
            float progress = Math.Min(1f, stateTimer / config.MailIconDestroyDuration);

            // ----- 激活色差特效并随时间改变强度 -----
            if (!PostProcessor.EndingSequenceFlashOutActive)
            {
                PostProcessor.EndingSequenceFlashOutActive = true;
                PostProcessor.EndingSequenceFlashOutPercentageComplete = 0f;
            }
            PostProcessor.EndingSequenceFlashOutPercentageComplete = 1f - progress;

            // 生成径向线条（按时间频率，而非每帧，目前模拟60帧，生成一次会生成两条，进度超过20%时加倍）
            mailLineTimer += t;
            while (mailLineTimer >= MAIL_LINE_INTERVAL)
            {
                mailLineTimer -= MAIL_LINE_INTERVAL;
                AddRadialMailLine();
                AddRadialMailLine();
                if (progress > 0.2f)
                {
                    AddRadialMailLine();
                    AddRadialMailLine();
                }
            }

            if (progress > 0.2f && stateTimer % 0.6f <= t)
                SFX.addCircle(os.mailicon.pos + new Vector2(20f, 6f), Utils.AddativeRed, 100f + 200f * progress);

            if (!mailExploded)
                os.topBarIconsColor = (Utils.randm(1f) < progress) ? Color.Red : originalTopBarIconsColor;

            // 爆炸触发（只执行一次）
            if (progress >= 0.99f && !mailExploded)
            {
                os.DisableEmailIcon = true;
                breakSound.Play();

                // 主爆炸
                explosion.Explode(1500, new Vector2(-0.1f, 3.1415926f), os.mailicon.pos + new Vector2(20f, 6f), 1f, 8f, 100f, 1600f, 1000f, 1200f, 3f, 7f);

                // 延时二次爆炸（与原版一致）
                os.delayer.Post(ActionDelayer.Wait(0.1), () =>
                {
                    explosion.Explode(100, new Vector2(-0.1f, 3.1415926f), os.mailicon.pos + new Vector2(20f, 6f), 1f, 6f, 100f, 1300f, 1000f, 1300f, 3f, 7f);
                });

                // 添加多个扩散圆圈（原版有12个延时圆圈）
                for (int i = 0; i < 12; i++)
                {
                    double time = i / 7.0;
                    os.delayer.Post(ActionDelayer.Wait(time), () =>
                    {
                        SFX.addCircle(os.mailicon.pos + new Vector2(20f, 6f), Utils.AddativeRed * 0.8f, 400f);
                    });
                }

                os.topBarIconsColor = originalTopBarIconsColor;
                mailExploded = true;
            }
        }

        private void UpdateImpactEffects(float t)
        {
            for (int i = 0; i < impactEffects.Count; i++)
            {
                var effect = impactEffects[i];
                effect.timeEnabled += t;
                // 总生命周期 5 秒（2秒淡入 + 3秒淡出）
                if (effect.timeEnabled > 5f)
                {
                    impactEffects.RemoveAt(i);
                    i--;
                }
                else
                {
                    impactEffects[i] = effect;
                }
            }
        }

        private Color GetDynamicColor(string colorStr, Color defaultColor)
        {
            if (string.IsNullOrEmpty(colorStr))
                return defaultColor;
            if (colorStr.Equals("LDTchara", StringComparison.OrdinalIgnoreCase))
            {
                float hue = (float)(OS.currentElapsedTime * 0.1) % 1.0f;
                return HSVToColor(hue, 1.0f, 1.0f);
            }
            // 实时解析颜色字符串（支持颜色名称、十六进制等）
            Color parsed = ParseColor(colorStr);
            return parsed != Color.Transparent ? parsed : defaultColor;
        }

        /// <summary>
        /// 根据当前显示的控件（标题、计时条等）计算所需的最小窗口高度（即 ramCost）。
        /// 返回的值会被限制在 [84, 190] 范围内。
        /// </summary>
        private int CalculateRequiredRamCost()
        {
            float scale = currentRamCost / 190f;

            int topMargin = Module.PANEL_HEIGHT + 4;
            int bottomMargin = 12;

            int titleHeight = Math.Max(32, (int)(40 * scale));
            int subtitleHeight = 0;
            // ... 判断副标题 ...
            bool hasSubtitle = (CurrentPhase != null && !string.IsNullOrEmpty(CurrentPhase.Subtitle)) ||
                       (CurrentPhase == null && (currentState == RunState.Flickering ||
                                                 currentState == RunState.WaitAfterDestruction ||
                                                 currentState == RunState.MailIconDestroy));
            if (hasSubtitle)
                subtitleHeight = Math.Max(18, (int)(22 * scale));
            int totalTextHeight = titleHeight + subtitleHeight;

            int timerTotalHeight = 0;
            if (globalTimerActive && config.EnableGlobalTimer && globalTimerRemaining > 0)
                timerTotalHeight += (int)(30 * scale);
            if (phaseTimerActive && config.EnablePhaseTimer && phaseTimerRemaining > 0 && CurrentPhase != null)
                timerTotalHeight += (int)(30 * scale);

            int spacing = (timerTotalHeight > 0) ? 12 : 0;

            int requiredTotalHeight = topMargin + totalTextHeight + spacing + timerTotalHeight + bottomMargin;

            if (requiredTotalHeight < 84) requiredTotalHeight = 84;
            if (requiredTotalHeight > 190) requiredTotalHeight = 190;
            return requiredTotalHeight;
        }

        /// <summary>
        /// 绘制邮件摧毁阶段的终端聚焦遮罩（参考原版 UpdateMailePhaseOut）
        /// </summary>
        private void DrawMailPhaseDarken()
        {
            if (!config.MailPhaseDarkenEnabled) return;
            if (os.terminal == null) return;

            float progress = Math.Min(1f, stateTimer / config.MailIconDestroyDuration);
            float alpha = 0.8f * progress;

            Utils.FillEverywhereExcept(
                Utils.InsetRectangle(os.terminal.bounds, 1),
                Utils.GetFullscreen(),
                spriteBatch,
                Color.Black * alpha
            );
        }

        /// <summary>
        /// 绘制终端聚焦特效（遮罩 + 扩散边框），进度与描述文本打印同步
        /// </summary>
        private void DrawTerminalFocusOverlay()
        {
            if (os.terminal == null) return;

            // 描述文本是否已全部打印完毕？
            bool textCompleted = (charsRenderedSoFar >= currentDisplayText.Length);
            if (textCompleted) return; // 文本完成，特效彻底结束

            // 遮罩：始终以固定透明度覆盖，直到文本打印完成
            Utils.FillEverywhereExcept(
                Utils.InsetRectangle(os.terminal.bounds, 1),
                Utils.GetFullscreen(),
                spriteBatch,
                Color.Black * 0.8f
            );

            // 边框扩散动画：固定时长（如 2 秒），与文本打印进度无关
            float borderDuration = 2f;
            float borderProgress = Math.Min(1f, stateTimer / borderDuration);
            if (borderProgress >= 1f) return; // 边框动画结束，但遮罩仍保留

            float num = 1f - borderProgress;
            num = Utils.CubicInCurve(num);
            float expandAmount = 200f;
            Rectangle borderRect = Utils.InsetRectangle(os.terminal.bounds, (int)(-1f * expandAmount * num));
            float alpha = 1f - num;
            if (alpha >= 0.8f)
                alpha = (1f - (alpha - 0.8f) * 5f) * 0.8f;
            int thickness = (int)(60f * (0.06f + num));

            RenderedRectangle.doRectangleOutline(
                borderRect.X, borderRect.Y,
                borderRect.Width, borderRect.Height,
                thickness,
                new Color?(os.highlightColor * alpha)
            );
        }

        private void DrawOutroFocusOverlay()
        {
            if (os.terminal == null) return;

            // 检查 Outro 文本是否已全部打印完毕
            bool textCompleted = string.IsNullOrEmpty(outroDisplayText) || (charsRenderedSoFar >= outroDisplayText.Length);
            if (textCompleted) return;

            // 遮罩：固定透明度
            Utils.FillEverywhereExcept(
                Utils.InsetRectangle(os.terminal.bounds, 1),
                Utils.GetFullscreen(),
                spriteBatch,
                Color.Black * 0.8f
            );

            // 边框扩散动画（固定2秒）
            float borderDuration = 2f;
            float borderProgress = Math.Min(1f, stateTimer / borderDuration);
            if (borderProgress >= 1f) return;

            float num = 1f - borderProgress;
            num = Utils.CubicInCurve(num);
            float expandAmount = 200f;
            Rectangle borderRect = Utils.InsetRectangle(os.terminal.bounds, (int)(-1f * expandAmount * num));
            float alpha = 1f - num;
            if (alpha >= 0.8f)
                alpha = (1f - (alpha - 0.8f) * 5f) * 0.8f;
            int thickness = (int)(60f * (0.06f + num));

            RenderedRectangle.doRectangleOutline(
                borderRect.X, borderRect.Y,
                borderRect.Width, borderRect.Height,
                thickness,
                new Color?(os.highlightColor * alpha)
            );
        }

        /// <summary>
        /// 绘制程序界面。注意绘制顺序：先绘制背景和所有 UI 元素（标题、计时条等），最后绘制聚焦遮罩，
        /// 确保遮罩能覆盖计时条。
        /// </summary>
        public override void Draw(float t)
        {
            base.Draw(t);
            drawTarget();
            drawOutline();

            // 内容区域（用于标题、按钮等 UI 元素）
            Rectangle contentRect = new(bounds.X + 2, bounds.Y + Module.PANEL_HEIGHT + 2, bounds.Width - 4, bounds.Height - Module.PANEL_HEIGHT - 4);
            // 背景区域（仿原版 DLCIntroExe 的 dest2）
            Rectangle bgRect = new(bounds.X + 2, bounds.Y + Module.PANEL_HEIGHT + 10, bounds.Width - 4, bounds.Height - (Module.PANEL_HEIGHT + 6));

            if (trialLocked || config == null)
            {
                DrawLockedMessage(bgRect, t, this.fade);
                return;
            }

            // 计算内存缩减缩放因子（仅当缩减过程中）
            float scale = finalScale;  // 默认使用最终缩放
            if (ramReductionActive)
                scale = currentRamCost / 190f;
            else if (ramReductionDelaying)
                scale = 1f;

            // ---------- 第一步：绘制各状态的基础内容（不包含聚焦遮罩） ----------
            switch (currentState)
            {
                case RunState.NotStarted:
                    DrawStartScreen(bgRect, contentRect, scale, t);  // 背景和按钮区域分开
                    break;
                case RunState.SpinningUp:
                    DrawSpinningUp(contentRect);
                    break;
                case RunState.Flickering:
                case RunState.WaitAfterDestruction:
                    DrawPhaseTitle(bgRect, contentRect, scale, t, this.fade);
                    break;
                case RunState.MailIconDestroy:
                    DrawPhaseTitle(bgRect, contentRect, scale, t, this.fade);
                    DrawMailPhaseDarken();
                    break;
                case RunState.AssignMission:
                    DrawPhaseTitle(bgRect, contentRect, scale, t, this.fade);
                    break;
                case RunState.OnMission:
                    DrawPhaseTitle(bgRect, contentRect, scale, t, this.fade);
                    // 执行任务期间不需要聚焦特效
                    break;
                case RunState.Outro:
                    DrawPhaseTitle(bgRect, contentRect, scale, t, this.fade);
                    break;
                case RunState.Exiting:
                    // 保持当前画面淡出，复用标题绘制即可
                    DrawPhaseTitle(bgRect, contentRect, scale, t, this.fade);
                    break;
            }

            // ---------- 第二步：绘制计时条（位于标题下方，聚焦遮罩之前） ----------
            // 绘制计时器（全局和阶段），这些元素随 scale 缩放
            int timerY = (int)timerStartY;
            if (globalTimerActive && config.EnableGlobalTimer && globalTimerRemaining > 0)
            {
                Rectangle globalRect = new(bounds.X + 10, timerY, bounds.Width - 20, (int)(25 * scale));
                Color globalColor = GetDynamicColor(config.GlobalTimerColor, os.highlightColor);
                DrawTimerBar(globalRect, globalTimerRemaining / config.GlobalTimeout, globalTimerRemaining, globalColor, scale);
                timerY += (int)(30 * scale);
            }
            if (phaseTimerActive && config.EnablePhaseTimer && phaseTimerRemaining > 0 && CurrentPhase != null)
            {
                Rectangle phaseRect = new(bounds.X + 10, timerY, bounds.Width - 20, (int)(25 * scale));
                Color phaseColor = GetDynamicColor(config.PhaseTimerColor, os.highlightColor);
                DrawTimerBar(phaseRect, phaseTimerRemaining / CurrentPhase.Timeout, phaseTimerRemaining, phaseColor, scale);
            }

            // ---------- 第三步：绘制聚焦遮罩（覆盖在计时条之上） ----------
            // 注意：MailIconDestroy 的遮罩已在 case 内部绘制，此处不再重复
            switch (currentState)
            {
                case RunState.AssignMission:
                    if (config.EnablePhaseStartFocus)
                        DrawTerminalFocusOverlay();
                    break;
                case RunState.Outro:
                    if (config.EnableTrialCompleteFocus)
                        DrawOutroFocusOverlay();
                    break;
            }

            // 绘制节点冲击波特效和邮件爆炸
            DrawImpactEffects();
            explosion.Render(spriteBatch);

        }

        /// <summary>
        /// 绘制一个计时条（背景、进度填充、边框、右侧文字）。
        /// </summary>
        private void DrawTimerBar(Rectangle rect, float percent, float remaining, Color customColor, float scale)
        {
            Color useColor = (customColor != Color.Transparent) ? customColor : os.highlightColor;

            RenderedRectangle.doRectangle(rect.X, rect.Y, rect.Width, rect.Height, Color.Black * 0.7f);
            int fillWidth = (int)(rect.Width * Math.Max(0, Math.Min(1, percent)));
            RenderedRectangle.doRectangle(rect.X, rect.Y, fillWidth, rect.Height, useColor);
            RenderedRectangle.doRectangleOutline(rect.X, rect.Y, rect.Width, rect.Height, 1, Color.White);
            string timerText = $"{(int)remaining}s";
            Vector2 textSize = GuiData.font.MeasureString(timerText);
            float scaledWidth = textSize.X * scale;
            float scaledHeight = textSize.Y * scale;
            Vector2 textPos = new(rect.X + rect.Width - scaledWidth - 5, rect.Y + (rect.Height - scaledHeight) / 2);
            spriteBatch.DrawString(GuiData.font, timerText, textPos, Color.White, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }

        private void DrawLockedMessage(Rectangle bgRect, float t, float fade)
        {
            // 始终绘制网格背景，若配置不存在则使用系统高亮色
            Color gridColor = (config != null) ? GetDynamicColor(config.BackgroundColor, os.highlightColor) : os.highlightColor;
            backgroundEffect.Update(t);
            backgroundEffect.Draw(bgRect, spriteBatch, Color.Black, gridColor * 0.2f, HexGridBackground.ColoringAlgorithm.CorrectedSinWash, 0f);

            string lockText = GetLocalizedLockedText();
            int btnHeight = 30;
            // 文字和按钮仍基于内容区域（或也可以基于 bgRect，但通常居中）
            Rectangle textRect = new(bgRect.X + 10, bgRect.Y + bgRect.Height / 2 - btnHeight / 2, bgRect.Width - 20, btnHeight);
            spriteBatch.Draw(Utils.white, textRect, Color.Black * 0.6f * fade);
            TextItem.doCenteredFontLabel(textRect, lockText, GuiData.font, Color.White * fade, false);

            // 退出按钮（右下角）
            int exitBtnWidth = 60;
            int exitBtnHeight = 22;
            string exitText = GetLocalizedExitButton();
            Rectangle exitBtnRect = new(bgRect.X + bgRect.Width - exitBtnWidth - 10, bgRect.Y + bgRect.Height - exitBtnHeight - 10, exitBtnWidth, exitBtnHeight);
            if (!isExiting)
            {
                if (Button.doButton(8310102 + PID, exitBtnRect.X, exitBtnRect.Y, exitBtnRect.Width, exitBtnRect.Height, exitText, new Color?(os.lockedColor)))
                {
                    isExiting = true;
                }
            }
        }

        private void DrawStartScreen(Rectangle bgRect, Rectangle contentRect, float scale, float t)
        {
            // 绘制背景
            Color gridColor = GetDynamicColor(config.BackgroundColor, os.highlightColor);
            backgroundEffect.Update(t);
            backgroundEffect.Draw(bgRect, spriteBatch, Color.Black, gridColor * 0.2f, HexGridBackground.ColoringAlgorithm.CorrectedSinWash, 0f);

            // 绘制按钮（使用 contentRect 定位）
            int btnHeight = (int)(30 * scale);
            Rectangle btnRect = new(contentRect.X + 10, contentRect.Y + contentRect.Height / 2 - btnHeight / 2, (int)((contentRect.Width - 20) * scale), btnHeight);
            string buttonText = GetLocalizedBeginButton();
            if (Button.doButton(8310101 + PID, btnRect.X, btnRect.Y, btnRect.Width, btnRect.Height, buttonText, new Color?(os.highlightColor)))
                TransitionToNextState();
        }

        private string GetLocalizedBeginButton()
        {
            string lang = Settings.ActiveLocale?.ToLowerInvariant() ?? "en-us";
            if (lang.StartsWith("zh")) return "开始试炼";
            if (lang.StartsWith("ja")) return "試験を開始";
            if (lang.StartsWith("ko")) return "시험 시작";
            if (lang.StartsWith("ru")) return "НАЧАТЬ ИСПЫТАНИЕ";
            if (lang.StartsWith("de")) return "BEGINNE DIE PRÜFUNG";
            if (lang.StartsWith("fr")) return "COMMENCER L'ÉPREUVE";
            if (lang.StartsWith("es")) return "COMENZAR PRUEBA";
            if (lang.StartsWith("tr")) return "DENEMEYE BAŞLA";
            if (lang.StartsWith("nl")) return "BEGIN MET PROEF";
            return "BEGIN TRIAL";
        }

        private string GetLocalizedLockedText()
        {
            string lang = Settings.ActiveLocale?.ToLowerInvariant() ?? "en-us";
            if (lang.StartsWith("zh")) return "试炼已锁定";
            if (lang.StartsWith("ja")) return "トライアルはロックされています";
            if (lang.StartsWith("ko")) return "시험이 잠겼습니다";
            if (lang.StartsWith("ru")) return "ИСПЫТАНИЕ ЗАБЛОКИРОВАНО";
            if (lang.StartsWith("de")) return "PRÜFUNG GESPERRT";
            if (lang.StartsWith("fr")) return "ÉPREUVE VERROUILLÉE";
            if (lang.StartsWith("es")) return "PRUEBA BLOQUEADA";
            if (lang.StartsWith("tr")) return "DENEME KİLİTLİ";
            if (lang.StartsWith("nl")) return "PROEF GESLOTEN";
            return "TRIAL LOCKED";
        }

        private string GetLocalizedInitializingText()
        {
            string lang = Settings.ActiveLocale?.ToLowerInvariant() ?? "en-us";
            if (lang.StartsWith("zh")) return "正在初始化";
            if (lang.StartsWith("ja")) return "初期化中";
            if (lang.StartsWith("ko")) return "초기화 중";
            if (lang.StartsWith("ru")) return "ИНИЦИАЛИЗАЦИЯ";
            if (lang.StartsWith("de")) return "INITIALISIERUNG";
            if (lang.StartsWith("fr")) return "INITIALISATION";
            if (lang.StartsWith("es")) return "INICIALIZANDO";
            if (lang.StartsWith("tr")) return "BAŞLATILIYOR";
            if (lang.StartsWith("nl")) return "INITIALISEREN";
            return "INITIALIZING";
        }

        private string GetLocalizedCompleteText()
        {
            string lang = Settings.ActiveLocale?.ToLowerInvariant() ?? "en-us";
            if (lang.StartsWith("zh")) return "完成";
            if (lang.StartsWith("ja")) return "完了";
            if (lang.StartsWith("ko")) return "완료";
            if (lang.StartsWith("ru")) return "ЗАВЕРШЕНО";
            if (lang.StartsWith("de")) return "ABGESCHLOSSEN";
            if (lang.StartsWith("fr")) return "TERMINÉ";
            if (lang.StartsWith("es")) return "COMPLETADO";
            if (lang.StartsWith("tr")) return "TAMAMLANDI";
            if (lang.StartsWith("nl")) return "VOLTOOID";
            return "COMPLETE";
        }

        private string GetLocalizedFailedText()
        {
            string lang = Settings.ActiveLocale?.ToLowerInvariant() ?? "en-us";
            if (lang.StartsWith("zh")) return "失败";
            if (lang.StartsWith("ja")) return "失敗";
            if (lang.StartsWith("ko")) return "실패";
            if (lang.StartsWith("ru")) return "ПРОВАЛ";
            if (lang.StartsWith("de")) return "FEHLGESCHLAGEN";
            if (lang.StartsWith("fr")) return "ÉCHEC";
            if (lang.StartsWith("es")) return "FALLIDO";
            if (lang.StartsWith("tr")) return "BAŞARISIZ";
            if (lang.StartsWith("nl")) return "MISLUKT";
            return "FAILED";
        }

        private string GetLocalizedExitButton()
        {
            string lang = Settings.ActiveLocale?.ToLowerInvariant() ?? "en-us";
            if (lang.StartsWith("zh")) return "退出";
            if (lang.StartsWith("ja")) return "終了";
            if (lang.StartsWith("ko")) return "종료";
            if (lang.StartsWith("ru")) return "ВЫХОД";
            if (lang.StartsWith("de")) return "BEENDEN";
            if (lang.StartsWith("fr")) return "QUITTER";
            if (lang.StartsWith("es")) return "SALIR";
            if (lang.StartsWith("tr")) return "ÇIKIŞ";
            if (lang.StartsWith("nl")) return "AFSLUITEN";
            return "EXIT";
        }

        private void DrawSpinningUp(Rectangle contentRect)
        {
            // 旋转动画颜色（使用配置或主题高亮色）
            Color spinColor = GetDynamicColor(config.SpinUpColor, os.highlightColor);

            // 重置随机数生成器，确保每帧画面稳定（与原版一致）
            Utils.LCG.reSeed(PID);

            for (int y = 0; y < contentRect.Height; y++)
            {
                // 每条线都有一个随机的“完成时间阈值”，从 0 到 SpinUpDuration
                float threshold = Utils.LCG.NextFloatScaled() * config.SpinUpDuration;

                // 基础线性进度：当前时间 / 该线的时间阈值 (0~1)
                float baseProgress = Math.Min(1f, stateTimer / threshold);
                float finalProgress;

                // 原版将线条分为两组，使用不同的缓动曲线
                if (Utils.LCG.NextFloatScaled() > 0.5f)
                {
                    // 第一组：80% 进度以前是缓慢的线性，80% 以后加速完成（二次缓出）
                    float inflectionPoint = 0.8f;
                    float slowPart = baseProgress * (1f - inflectionPoint);
                    if (baseProgress > inflectionPoint)
                    {
                        // 超出转折点后，将剩余部分 (baseProgress - inflectionPoint) 重新映射到 0~1，
                        // 并使用 QuadraticOutCurve 产生先快后慢的加速效果
                        float remaining = 1f - slowPart;
                        float t = (baseProgress - inflectionPoint) / (1f - inflectionPoint);
                        t = Utils.QuadraticOutCurve(t);
                        finalProgress = slowPart + remaining * t;
                    }
                    else
                    {
                        finalProgress = slowPart;
                    }
                }
                else
                {
                    // 第二组：直接使用二次缓出曲线
                    finalProgress = Utils.QuadraticOutCurve(baseProgress);
                }

                int width = (int)(finalProgress * contentRect.Width);
                // 颜色混合：白色到主题色，带随机变化
                Color col = Color.Lerp(Utils.AddativeWhite * 0.1f, spinColor, Utils.LCG.NextFloatScaled());
                spriteBatch.Draw(Utils.white, new Rectangle(contentRect.X, contentRect.Y + y, width, 1), col);
            }
        }

        /// <summary>
        /// 绘制阶段标题（在窗口中央），支持内存缩减时的缩放。
        /// </summary>
        private void DrawPhaseTitle(Rectangle bgRect, Rectangle contentRect, float scale, float t, float fade)
        {
            // 背景网格
            Color gridColor = GetDynamicColor(config.BackgroundColor, os.highlightColor);
            backgroundEffect.Update(t);
            backgroundEffect.Draw(bgRect, spriteBatch, Color.Black, gridColor * 0.2f, HexGridBackground.ColoringAlgorithm.CorrectedSinWash, 0f);

            // 获取文本
            string titleText = null;
            string subtitleText = null;
            Color? titleColor = null;

            if (CurrentPhase != null)
            {
                titleText = CurrentPhase.Title;
                subtitleText = CurrentPhase.Subtitle;
            }
            else
            {
                switch (currentState)
                {
                    case RunState.Flickering:
                    case RunState.WaitAfterDestruction:
                    case RunState.MailIconDestroy:
                        titleText = GetLocalizedInitializingText();
                        subtitleText = "---";
                        break;
                    case RunState.Outro:
                    case RunState.Exiting:  // 退出时沿用 Outro 的显示逻辑
                        if (outroTextCompleted)
                        {
                            titleText = trialSucceeded ? GetLocalizedCompleteText() : GetLocalizedFailedText();
                            subtitleText = "---";
                            titleColor = trialSucceeded ? Color.White : Color.Red;
                        }
                        break;
                }
            }

            if (string.IsNullOrEmpty(titleText))
                return;

            int titleHeight = Math.Max(32, (int)(40 * scale));
            int subtitleHeight = string.IsNullOrEmpty(subtitleText) ? 0 : Math.Max(18, (int)(22 * scale));
            int totalTextHeight = titleHeight + subtitleHeight;

            bool hasGlobalTimer = globalTimerActive && config.EnableGlobalTimer && globalTimerRemaining > 0;
            bool hasPhaseTimer = phaseTimerActive && config.EnablePhaseTimer && phaseTimerRemaining > 0 && CurrentPhase != null;
            bool hasAnyTimer = hasGlobalTimer || hasPhaseTimer;

            // 计算计时条区域总高度（与 Draw 中累加的 timerY 增量一致）
            int timerTotalHeight = 0;
            if (hasGlobalTimer) timerTotalHeight += (int)(30 * scale);
            if (hasPhaseTimer) timerTotalHeight += (int)(30 * scale);

            // 计算标题组与计时条之间的间距（固定值，比如 12 像素）
            int spacing = hasAnyTimer ? 12 : 0;

            // 整个内容块的高度 = 标题组 + 间距 + 计时条区域
            int contentBlockHeight = totalTextHeight + spacing + timerTotalHeight;

            // 内容区域可用高度（排除顶部 Panel 和底部留白）
            int usableHeight = bounds.Height - Module.PANEL_HEIGHT - 20;

            // 居中整个内容块
            int blockStartY = bounds.Y + Module.PANEL_HEIGHT + (usableHeight - contentBlockHeight) / 2;

            // 标题组起始 Y
            int groupY = blockStartY;

            // 绘制全宽背景
            Rectangle groupRect = new(contentRect.X, groupY - 5, contentRect.Width, totalTextHeight + 10);
            spriteBatch.Draw(Utils.white, groupRect, Color.Black * 0.6f * fade);

            // 标题
            Rectangle titleRect = new(groupRect.X, groupRect.Y + 5, groupRect.Width, titleHeight);
            TextItem.doFontLabelToSize(titleRect, titleText, GuiData.font, (titleColor ?? Color.White) * fade, true, false);

            // 副标题
            if (!string.IsNullOrEmpty(subtitleText))
            {
                Rectangle subtitleRect = new(groupRect.X, titleRect.Bottom, groupRect.Width, subtitleHeight);
                TextItem.doFontLabelToSize(subtitleRect, subtitleText, GuiData.font, (Utils.AddativeWhite * 0.9f) * fade, true, false);
            }

            // 保存计时条的起始 Y 坐标，供 Draw 方法使用（通过字段传递）
            // 我们可以在类中添加一个字段：private float timerStartY;
            // 但为了避免改动过大，可以直接在 Draw 方法中基于相同逻辑重新计算 timerY。
            // 因此这里不需要额外操作，只需在 Draw 中使用相同公式。
            // 计算计时条起始 Y
            timerStartY = groupY + totalTextHeight + spacing;
        }

        private void DrawImpactEffects()
        {
            Utils.LCG.reSeed(PID);

            for (int i = 0; i < impactEffects.Count; i++)
            {
                var effect = impactEffects[i];
                Vector2 location = effect.location;

                // 颜色：红色基调，带随机变化
                Color value = Color.Lerp(Utils.AddativeWhite, Utils.AddativeRed, 0.6f + 0.4f * Utils.LCG.NextFloatScaled()) *
                              (0.6f + 0.4f * Utils.LCG.NextFloatScaled());

                // 计算两阶段时间常量（与原版一致：淡入2秒，淡出3秒）
                float transInTime = 2f;
                float transOutTime = 3f;
                float time = effect.timeEnabled;

                // 淡入进度 (0~1)
                float fadeInProgress = Utils.QuadraticOutCurve(time / transInTime);
                // 整体进度
                float overallProgress = Utils.QuadraticOutCurve(Utils.QuadraticOutCurve(time / (transInTime + transOutTime)));

                // 淡出进度：仅在时间超过淡入时间后计算，否则设为 -1 使条件判断为假
                float fadeOutProgress = (time > transInTime) ?
                    Utils.QuadraticOutCurve((time - transInTime) / transOutTime) : -1f;

                effect.cne.color = value * fadeInProgress;
                effect.cne.ScaleFactor = overallProgress * effect.scaleModifier;

                if (time > transInTime)
                {
                    effect.cne.color = value * (1f - fadeOutProgress);
                }

                // 绘制高亮圆圈
                if (fadeInProgress >= 0f && effect.HasHighlightCircle)
                {
                    // 透明度计算：淡入阶段为 1 - fadeInProgress，淡出阶段为 fadeOutProgress - fadeInProgress
                    float circleAlpha = 1f - fadeInProgress - ((fadeOutProgress >= 0f) ? (1f - fadeOutProgress) : 0f);
                    float circleScale = fadeInProgress / (float)circle.Width * 60f;

                    spriteBatch.Draw(circle, location, null,
                        value * circleAlpha,
                        0f,
                        new Vector2(circle.Width / 2, circle.Height / 2),
                        circleScale,
                        SpriteEffects.None,
                        0.7f);
                }

                // 绘制连接的节点效果（核心视觉）
                effect.cne.draw(spriteBatch, location);
            }
        }

        // ---------- 公共方法（供外部获取删除节点列表）----------
        public List<int> GetDeletedNodeIndices() => new(deletedNodeIndices);
        /// <summary>
        /// 强制当前试炼立即失败，进入 Outro 状态并跳过描述文本。
        /// 如果试炼已经结束或未开始，则无操作。
        /// </summary>
        public void ForceFail()
        {
            // 如果已经退出或未开始，则忽略
            if (isExiting || currentState == RunState.NotStarted || currentState == RunState.Exiting || config == null)
                return;

            // 停止所有计时器
            globalTimerActive = false;
            phaseTimerActive = false;

            // 设置失败标志
            trialSucceeded = false;

            // 直接跳转到 Outro 状态，并跳过描述文本
            currentState = RunState.Outro;
            currentPhaseIdx = -1;           // 清空阶段
            stateTimer = 0f;
            outroTextCompleted = true;
            outroDisplayText = "";

            // 清空终端（可选，使失败提示更明显）
            os.execute("clear");
        }
    }
}