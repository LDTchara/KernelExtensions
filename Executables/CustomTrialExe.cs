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

        // 音乐恢复相关
        private string originalMusicName = null;   // 进入试炼前的音乐名

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
        private const float TARGET_RAM_COST = 96f;             // 目标内存占用（固定不可配置）
        private float ramReductionStartValue = 190f;           // 缩减开始时的 ramCost

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
        private SoundEffect breakSound;                        // 破碎音效
        private Texture2D circle;                              // 圆形纹理
        private Texture2D circleOutline;                       // 圆形轮廓纹理
        private bool hasStartedNodeDestruction = false;        // 是否已开始节点摧毁（用于计算一次间隔）
        private bool mailExploded = false;   // 防止邮件爆炸重复执行

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
        /// 1. 如果字符串包含路径分隔符（/ 或 \），直接返回（认为用户已写完整路径）。
        /// 2. 否则，检查是否为 DLC 音乐：通过检测 Content/DLC/Music/文件名.ogg 是否存在，若存在返回 "DLC/Music/文件名"。
        /// 3. 检查是否为扩展内自定义音乐：通过检测 Extensions/当前扩展名/Music/文件名.ogg 是否存在，若存在返回 "../Extensions/扩展名/Music/文件名"。
        /// 4. 否则，作为原版音乐返回原字符串（MusicManager 会从 Content/Music/ 加载）。
        /// </summary>
        private string ResolveMusicPath(string musicPath)
        {
            if (string.IsNullOrEmpty(musicPath))
                return musicPath;

            // 如果已经是绝对路径或已经以 "../Extensions/" 开头，直接返回
            if (Path.IsPathRooted(musicPath) || musicPath.StartsWith("../Extensions/"))
                return musicPath;

            // 如果包含路径分隔符，则视为相对于扩展根目录的路径，转换为 "../Extensions/当前扩展名/..."
            if (musicPath.Contains('/') || musicPath.Contains('\\'))
            {
                if (ExtensionLoader.ActiveExtensionInfo != null)
                {
                    string extFolder = ExtensionLoader.ActiveExtensionInfo.GetFoldersafeName();
                    // 去除可能的 .ogg 扩展名
                    string name = musicPath;
                    if (name.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase))
                        name = name.Substring(0, name.Length - 4);
                    return "../Extensions/" + extFolder + "/" + name;
                }
                // 如果没有扩展信息，则原样返回（可能失败）
                return musicPath;
            }

            // 纯文件名：按原逻辑处理 DLC 或扩展 Music 文件夹
            string dlcOggPath = Path.Combine(Paths.GameRootPath, "Content", "DLC", "Music", musicPath + ".ogg");
            if (File.Exists(dlcOggPath))
                return "DLC/Music/" + musicPath;

            if (ExtensionLoader.ActiveExtensionInfo != null)
            {
                string extFolder = ExtensionLoader.ActiveExtensionInfo.GetFoldersafeName();
                string extOggPath = Path.Combine(Paths.GameRootPath, "Extensions", extFolder, "Music", musicPath + ".ogg");
                if (File.Exists(extOggPath))
                    return "../Extensions/" + extFolder + "/Music/" + musicPath;
            }

            return musicPath;
        }

        // ---------- 构造函数 ----------
        public CustomTrialExe() : base()
        {
            this.ramCost = 190;                                // 内存占用（MB）
            this.IdentifierName = "CustomTrial";               // 程序内部标识
            this.name = "CustomTrial";                         // 程序显示名称
            this.CanBeKilled = true;                           // 初始允许被 kill 命令关闭
            CurrentInstance = this;                            // 设置静态实例
        }

        // ---------- 初始化（类似 LoadContent） ----------
        public override void OnInitialize()
        {
            base.OnInitialize();

            // 获取当前扩展的根目录（必须作为扩展运行）
            if (ExtensionLoader.ActiveExtensionInfo != null)
                extensionRoot = ExtensionLoader.ActiveExtensionInfo.FolderPath.Replace('\\', '/');

            // ---- 检测 CustomTrial_ 开头的 Flag ----
            string flag = os.Flags.GetFlagStartingWith("CustomTrial_");
            if (string.IsNullOrEmpty(flag))
            {
                trialLocked = true;
                Console.WriteLine("[KernelExtensions]CustomTrialExe: No CustomTrial_ flag found. Trial locked.");
                config = null;
                return;
            }
            Console.WriteLine($"[KernelExtensions]CustomTrialExe: Found flag '{flag}'");

            // 从 flag 中提取配置名
            string configName = flag.Substring("CustomTrial_".Length);
            if (string.IsNullOrEmpty(configName)) configName = "Default";
            CurrentConfigName = configName;

            // 加载配置（LoadConfig 内部需要相应调整，可以传入 configName 或继续用 flag）
            LoadConfig(flag); // 或者改为 LoadConfig(configName)

            if (config == null)
            {
                trialLocked = true;
                Console.WriteLine("[KernelExtensions]CustomTrialExe: Failed to load trial config. Check that the config file exists and is valid.");
                return;
            }

            // ---- 解析自定义颜色（如果配置了）----
            cachedBackgroundColor = ParseColor(config.BackgroundColor);
            cachedGlobalTimerColor = ParseColor(config.GlobalTimerColor);
            cachedPhaseTimerColor = ParseColor(config.PhaseTimerColor);
            cachedSpinUpColor = ParseColor(config.SpinUpColor);

            // ---- 保存原始顶部栏图标颜色（用于邮件爆炸特效恢复）----
            originalTopBarIconsColor = os.topBarIconsColor;

            // ---- 初始化特效系统 ----
            backgroundEffect = new HexGridBackground(os.content);
            explosion.Init(os.content);
            breakSound = os.content.Load<SoundEffect>("SFX/DoomShock");

            // ---- 加载纹理 ----
            circle = os.content.Load<Texture2D>("Circle");
            circleOutline = os.content.Load<Texture2D>("CircleOutlineLarge");

            // 保存当前音乐名（用于未开始时退出恢复）
            originalMusicName = MusicManager.currentSongName;

            // ---- 播放启动音乐 ----
            if (!string.IsNullOrEmpty(config.StartMusic))
                MusicManager.transitionToSong(ResolveMusicPath(config.StartMusic));

            // ---- 应用持久化删除的节点（从存档中恢复删除状态）----
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
            if (globalTimerActive && !isExiting)
            {
                globalTimerRemaining -= t;
                if (globalTimerRemaining <= 0)
                {
                    OnGlobalTimeout(); // 全局超时处理
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
            if (ramReductionActive && ramReductionProgress < 1f)
            {
                ramReductionProgress += t / config.RamReductionDuration;
                if (ramReductionProgress >= 1f)
                {
                    ramReductionProgress = 1f;
                    ramReductionActive = false;
                }
                // 线性插值计算当前 ramCost
                currentRamCost = MathHelper.Lerp(ramReductionStartValue, TARGET_RAM_COST, ramReductionProgress);
                this.ramCost = (int)currentRamCost;
            }
            if (ramReductionProgress >= 1f && !ramReductionActive && finalScale != TARGET_RAM_COST / 190f)
            {
                finalScale = TARGET_RAM_COST / 190f;
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
                    if (stateTimer >= 2f)
                    {
                        ExecuteActionFile(config.OnComplete?.FilePath);
                        DeleteCurrentTrialFlag();   // 完成后删除 Flag
                        Result = CompletionResult.Success;   // 设置成功状态，会自动 isExiting = true
                        currentState = RunState.Exiting;   // 防止重复执行
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
                    // 注意：原来执行 OnStart 的地方已移除，动画完成后将执行 OnAnimationComplete
                    currentState = RunState.SpinningUp;
                    break;

                case RunState.SpinningUp:
                    // 清空终端
                    os.execute("clear");
                    // 切换主题
                    ApplyThemeSwitch();
                    if (config.EnableFlickering)
                        currentState = RunState.Flickering;
                    else if (config.EnableMailIconDestroy)
                        currentState = RunState.MailIconDestroy;
                    else
                        StartNextPhase();
                    break;

                case RunState.Flickering:
                    if (config.EnableMailIconDestroy)
                    {
                        if (config.PostDestructionDelay > 0)
                            currentState = RunState.WaitAfterDestruction;
                        else
                            currentState = RunState.MailIconDestroy;
                    }
                    else
                        StartNextPhase();
                    break;

                case RunState.WaitAfterDestruction:
                    currentState = RunState.MailIconDestroy;
                    break;

                case RunState.MailIconDestroy:
                    // 执行动画完成后的动作
                    ExecuteActionFile(config.OnAnimationComplete?.FilePath);
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
                        currentState = RunState.Outro;
                        stateTimer = 0f;
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

        // ---------- 失败处理（不再输出失败信息到终端） ----------
        private void OnPhaseTimeout()
        {
            ExecuteActionFile(CurrentPhase?.OnFail?.FilePath);
            if (CurrentPhase.EnableResetOnFail)
                ResetCurrentPhase();
            else
            {
                Result = CompletionResult.Failure;   // 设置失败状态，不重置则退出
                currentState = RunState.Exiting;   // 防止重复执行
            }
        }

        private void OnGlobalTimeout()
        {
            ExecuteActionFile(config.OnGlobalFail?.FilePath);
            Result = CompletionResult.Failure;   // 设置失败状态，不重置则退出
            currentState = RunState.Exiting;   // 防止重复执行
        }

        private void OnTraceTimeout()
        {
            ExecuteActionFile(CurrentPhase?.OnFail?.FilePath);
            if (CurrentPhase.EnableResetOnFail)
                ResetCurrentPhase();
            else
            {
                Result = CompletionResult.Failure;   // 设置失败状态，不重置则退出
                currentState = RunState.Exiting;   // 防止重复执行
            }
        }

        private void ResetCurrentPhase()
        {
            // 重置状态到 AssignMission，保持 currentPhaseIdx 不变
            currentState = RunState.AssignMission;
            // 重置计时器
            stateTimer = 0f;
            charsRenderedSoFar = 0;
            // 重新加载描述文本
            string rawText = CurrentPhase.DescriptionText;
            string resolvedPath = ResolvePath(rawText);
            if (resolvedPath != null && File.Exists(resolvedPath))
                currentDisplayText = Utils.readEntireFile(resolvedPath);
            else
                currentDisplayText = rawText ?? "";
            // 执行阶段开始时的动作（可选，但可能不需要在重置时再次执行）
            // ExecuteActionFile(CurrentPhase.OnPhaseStart?.FilePath);
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
    base.OnComplete();
    // 如果是在未开始试炼的状态下退出（例如玩家 kill 了程序），恢复音乐
    if (currentState == RunState.NotStarted && !trialLocked && !string.IsNullOrEmpty(originalMusicName))
    {
        MusicManager.transitionToSong(originalMusicName);
    }
    // 如果试炼成功或失败，停止当前音乐
    if (Result == CompletionResult.Success || Result == CompletionResult.Failure || Result == CompletionResult.Error)
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
                using FileStream fs = new(fullPath, FileMode.Open);
                using XmlReader reader = XmlReader.Create(fs);
                reader.MoveToContent();
                if (reader.Name == "Actions" && reader.IsStartElement())
                {
                    reader.ReadStartElement("Actions");
                    while (reader.NodeType != XmlNodeType.EndElement)
                    {
                        if (reader.NodeType == XmlNodeType.Element)
                        {
                            SerializableAction action = SerializableAction.Deserialize(reader);
                            action.Trigger(os);
                        }
                        else reader.Read();
                    }
                    reader.ReadEndElement();
                }
                else
                {
                    while (reader.Read())
                    {
                        if (reader.NodeType == XmlNodeType.Element)
                        {
                            SerializableAction action = SerializableAction.Deserialize(reader);
                            action.Trigger(os);
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
            Vector2 screenPos = comp.getScreenSpacePosition();

            // 记录删除的节点索引（持久化）
            if (!deletedNodeIndices.Contains(nodeIdx))
                deletedNodeIndices.Add(nodeIdx);
            CustomTrialNodeStorage.AddDeletedNode(CurrentConfigName, nodeIdx);

            impactEffects.Add(new TraceKillExe.PointImpactEffect
            {
                location = screenPos,
                scaleModifier = 3f,
                cne = new ConnectedNodeEffect(os, true),
                timeEnabled = 0f,
                HasHighlightCircle = true
            });
            visible.Remove(nodeIdx);
        }

        // ---------- 邮件爆炸特效 ----------
        private void UpdateMailIconDestroy(float t)
        {
            float progress = Math.Min(1f, stateTimer / config.MailIconDestroyDuration);
            if (progress > 0.2f && stateTimer % 0.6f <= t)
                SFX.addCircle(os.mailicon.pos + new Vector2(20f, 6f), Utils.AddativeRed, 100f + 200f * progress);

            // 仅当尚未爆炸时才随机切换顶栏颜色，避免爆炸后继续变色
            if (!mailExploded)
                os.topBarIconsColor = (Utils.randm(1f) < progress) ? Color.Red : originalTopBarIconsColor;

            // 爆炸触发（只执行一次）
            if (progress >= 0.99f && !mailExploded)
            {
                os.DisableEmailIcon = true;
                breakSound.Play();
                explosion.Explode(1500, new Vector2(-0.1f, 3.1415926f), os.mailicon.pos + new Vector2(20f, 6f), 1f, 8f, 100f, 1600f, 1000f, 1200f, 3f, 7f);
                // 恢复顶栏颜色（使用切换主题后保存的颜色）
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
                if (effect.timeEnabled > 5f)
                    impactEffects.RemoveAt(i--);
                else
                    impactEffects[i] = effect;
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

        // ---------- 绘制 ----------
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
                DrawLockedMessage(bgRect);
                return;
            }

            // 计算内存缩减缩放因子（仅当缩减过程中）
            float scale = finalScale;  // 默认使用最终缩放
            if (ramReductionActive)
                scale = currentRamCost / 190f;
            else if (ramReductionDelaying)
                scale = 1f;

            switch (currentState)
            {
                case RunState.NotStarted:
                    DrawStartScreen(bgRect, contentRect, scale);  // 背景和按钮区域分开
                    break;
                case RunState.SpinningUp:
                    DrawSpinningUp(contentRect);
                    break;
                case RunState.Flickering:
                case RunState.MailIconDestroy:
                case RunState.WaitAfterDestruction:
                case RunState.AssignMission:
                case RunState.OnMission:
                    DrawPhaseTitle(bgRect, contentRect, scale);
                    break;
            }

            DrawImpactEffects();
            explosion.Render(spriteBatch);

            // 绘制计时器（全局和阶段），这些元素随 scale 缩放
            int timerY = bounds.Y + bounds.Height - (int)(70 * scale);
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

        private void DrawLockedMessage(Rectangle bgRect)
        {
            if (backgroundEffect == null)
            {
                RenderedRectangle.doRectangle(bgRect.X, bgRect.Y, bgRect.Width, bgRect.Height, Color.Black);
            }
            else
            {
                Color gridColor = GetDynamicColor(config.BackgroundColor, os.highlightColor);
                backgroundEffect.Update(0f);
                backgroundEffect.Draw(bgRect, spriteBatch, Color.Black, gridColor * 0.2f, HexGridBackground.ColoringAlgorithm.CorrectedSinWash, 0f);
            }

            string lockText = GetLocalizedLockedText();
            int btnHeight = 30;
            // 文字和按钮仍基于内容区域（或也可以基于 bgRect，但通常居中）
            Rectangle textRect = new(bgRect.X + 10, bgRect.Y + bgRect.Height / 2 - btnHeight / 2, bgRect.Width - 20, btnHeight);
            spriteBatch.Draw(Utils.white, textRect, Color.Black * 0.6f);
            TextItem.doCenteredFontLabel(textRect, lockText, GuiData.font, Color.Red, false);

            // 退出按钮（右下角）
            int exitBtnWidth = 60;
            int exitBtnHeight = 22;
            Rectangle exitBtnRect = new(bgRect.X + bgRect.Width - exitBtnWidth - 10, bgRect.Y + bgRect.Height - exitBtnHeight - 10, exitBtnWidth, exitBtnHeight);
            if (Button.doButton(8310102 + PID, exitBtnRect.X, exitBtnRect.Y, exitBtnRect.Width, exitBtnRect.Height, "Exit", new Color?(os.lockedColor)))
            {
                isExiting = true;
            }
        }

        private void DrawStartScreen(Rectangle bgRect, Rectangle contentRect, float scale)
        {
            // 绘制背景
            Color gridColor = GetDynamicColor(config.BackgroundColor, os.highlightColor);
            backgroundEffect.Update(0f);
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

        private void DrawSpinningUp(Rectangle contentRect)
        {
            // 绘制旋转进度条（使用 contentRect）
            Color spinColor = GetDynamicColor(config.SpinUpColor, os.highlightColor);
            Utils.LCG.reSeed(PID);
            for (int y = 0; y < contentRect.Height; y++)
            {
                float noise = Utils.LCG.NextFloatScaled();
                float lineProgress = Math.Min(1f, stateTimer / (config.SpinUpDuration * noise));
                int width = (int)(lineProgress * contentRect.Width);
                Color col = Color.Lerp(Utils.AddativeWhite * 0.1f, spinColor, Utils.LCG.NextFloatScaled());
                spriteBatch.Draw(Utils.white, new Rectangle(contentRect.X, contentRect.Y + y, width, 1), col);
            }
        }

        /// <summary>
        /// 绘制阶段标题（在窗口中央），支持内存缩减时的缩放。
        /// </summary>
        private void DrawPhaseTitle(Rectangle bgRect, Rectangle contentRect, float scale)
        {
            if (CurrentPhase == null) return;

            // 绘制背景
            Color gridColor = GetDynamicColor(config.BackgroundColor, os.highlightColor);
            backgroundEffect.Update(0f);
            backgroundEffect.Draw(bgRect, spriteBatch, Color.Black, gridColor * 0.2f, HexGridBackground.ColoringAlgorithm.CorrectedSinWash, 0f);

            // 标题矩形：宽度保持全宽（不缩放），高度随 scale 缩放
            int titleHeight = Math.Max(28, (int)(40 * scale));
            // 标题 Y 坐标：保持相对位置（向上缩放效果）
            Rectangle titleRect = new(contentRect.X, contentRect.Y + contentRect.Height / 6 - titleHeight / 2, contentRect.Width, titleHeight);
            spriteBatch.Draw(Utils.white, titleRect, Color.Black * 0.6f);
            // 使用 doFontLabelToSize 自动适应矩形高度，文字居中
            TextItem.doFontLabelToSize(titleRect, CurrentPhase.Title ?? "", GuiData.font, Color.White, true, false);
            if (!string.IsNullOrEmpty(CurrentPhase.Subtitle))
            {
                int subtitleHeight = Math.Max(16, (int)(22 * scale));
                Rectangle subtitleRect = new(contentRect.X, titleRect.Y + titleRect.Height, contentRect.Width, subtitleHeight);
                spriteBatch.Draw(Utils.white, subtitleRect, Color.Black * 0.4f);
                TextItem.doFontLabelToSize(subtitleRect, CurrentPhase.Subtitle, GuiData.font, Utils.AddativeWhite * 0.9f, true, false);
            }
        }

        private void DrawImpactEffects()
        {
            foreach (var effect in impactEffects)
            {
                float progress = effect.timeEnabled / 2f;
                Color col = Utils.AddativeRed * (1f - progress);
                if (effect.HasHighlightCircle)
                    spriteBatch.Draw(circle, effect.location, null, col, 0f, new Vector2(circle.Width / 2, circle.Height / 2), effect.scaleModifier, SpriteEffects.None, 0f);
                effect.cne.draw(spriteBatch, effect.location);
            }
        }

        // ---------- 公共方法（供外部获取删除节点列表）----------
        public List<int> GetDeletedNodeIndices() => new(deletedNodeIndices);
    }
}