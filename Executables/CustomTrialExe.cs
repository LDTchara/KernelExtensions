using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;          // 用于反射获取私有字段
using System.Xml;
using System.Xml.Serialization;
using Hacknet;
using Hacknet.Effects;
using Hacknet.Extensions;
using Hacknet.Gui;
using Module = Hacknet.Module;   // 消除与 System.Reflection.Module 的歧义
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Pathfinder.Executable;
using Pathfinder.Util;
using KernelExtensions.Config;

namespace KernelExtensions.Executables
{
    /// <summary>
    /// 自定义试炼程序，基于 Pathfinder 的 GameExecutable。
    /// 功能：旋转动画、可选特效、多阶段任务、逐字打印（支持%停顿）、全局/阶段计时器、失败重置（可配置）、颜色自定义等。
    /// 所有文件路径均相对于当前扩展根目录（Extension 文件夹）。
    /// </summary>
    public class CustomTrialExe : GameExecutable
    {
        // ---------- 状态机枚举 ----------
        private enum RunState
        {
            NotStarted,      // 显示开始按钮
            SpinningUp,      // 旋转进度条动画
            Flickering,      // UI 闪烁+节点消失（可选）
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

        // 辅助属性：获取当前阶段配置（索引有效时）
        private PhaseConfig currentPhase => (currentPhaseIdx >= 0 && currentPhaseIdx < config.Phases.Count) ? config.Phases[currentPhaseIdx] : null;

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

        // ---------- 特效相关 ----------
        private List<TraceKillExe.PointImpactEffect> impactEffects = new List<TraceKillExe.PointImpactEffect>(); // 冲击波特效列表
        private float nodeRemovalTimer = 0f;                   // 节点移除计时器
        private float nodeRemovalInterval = 0f;                // 动态计算的节点摧毁间隔
        private Color originalTopBarIconsColor;                // 保存原始顶部栏图标颜色（用于特效恢复）
        private HexGridBackground backgroundEffect;            // 六边形网格背景
        private ExplodingUIElementEffect explosion = new ExplodingUIElementEffect(); // 爆炸特效
        private SoundEffect breakSound;                        // 破碎音效
        private Texture2D circle;                              // 圆形纹理
        private Texture2D circleOutline;                       // 圆形轮廓纹理
        private bool hasStartedNodeDestruction = false;        // 是否已开始节点摧毁（用于计算一次间隔）

        // ---------- 扩展根目录 ----------
        private string extensionRoot = null;                   // 当前扩展的根目录路径

        // ---------- 试炼锁定标志 ----------
        private bool trialLocked = false;                      // 是否因缺少 Flag 而锁定

        // ---------- 自定义颜色缓存 ----------
        private Color cachedBackgroundColor = Color.Transparent;   // 程序背景颜色（透明表示使用主题）
        private Color cachedGlobalTimerColor = Color.Transparent;  // 全局进度条颜色
        private Color cachedPhaseTimerColor = Color.Transparent;   // 阶段进度条颜色
        private Color cachedSpinUpColor = Color.Transparent;       // 旋转动画颜色

        // ---------- 构造函数 ----------
        public CustomTrialExe() : base()
        {
            this.ramCost = 190;                                // 内存占用（MB）
            this.IdentifierName = "CustomTrial";               // 程序内部标识
            this.name = "CustomTrial";                         // 程序显示名称
            this.CanBeKilled = true;                           // 初始允许被 kill 命令关闭
        }

        // ---------- 初始化（类似 LoadContent） ----------
        public override void OnInitialize()
        {
            base.OnInitialize();

            // 获取当前扩展的根目录（必须作为扩展运行）
            if (ExtensionLoader.ActiveExtensionInfo != null)
                extensionRoot = ExtensionLoader.ActiveExtensionInfo.FolderPath.Replace('\\', '/');

            // ---- 检测所有 CustomTrial_ 开头的 Flag ----
            List<string> trialFlags = new List<string>();
            // 使用反射获取 ProgressionFlags 的私有字段 Flags（因为未公开）
            var flagsField = typeof(ProgressionFlags).GetField("Flags", BindingFlags.NonPublic | BindingFlags.Instance);
            if (flagsField != null)
            {
                var flagsList = flagsField.GetValue(os.Flags) as List<string>;
                if (flagsList != null)
                {
                    foreach (var f in flagsList)
                        if (f.StartsWith("CustomTrial_"))
                            trialFlags.Add(f);
                }
            }

            // 没有找到任何试炼 Flag → 锁定试炼
            if (trialFlags.Count == 0)
            {
                trialLocked = true;
                Console.WriteLine("[KernelExtensions] No CustomTrial_ flag found. Trial locked.");
                config = null;
                return;
            }
            // 多个 Flag 时警告，取第一个作为有效配置
            else if (trialFlags.Count > 1)
            {
                Console.WriteLine($"[KernelExtensions] Warning: Multiple CustomTrial_ flags found: {string.Join(", ", trialFlags)}. Using the first one: {trialFlags[0]}.");
            }
            string flag = trialFlags[0];

            // ---- 加载配置（根据 Flag 后缀读取对应的 XML）----
            LoadConfig(flag);

            if (config == null)
            {
                trialLocked = true;
                Console.WriteLine("[KernelExtensions] Failed to load trial config. Check that the config file exists and is valid.");
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

            // ---- 播放启动音乐 ----
            if (!string.IsNullOrEmpty(config.StartMusic))
                MusicManager.transitionToSong(config.StartMusic);
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
                Console.WriteLine("[KernelExtensions] Error: No extension root found. This mod must be run as part of an extension.");
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

            // 配置文件必须位于扩展根目录下的 Trial 文件夹中
            if (string.IsNullOrEmpty(extensionRoot))
            {
                Console.WriteLine("[KernelExtensions] Error: Cannot locate trial config because no extension root is available.");
                isExiting = true;
                return;
            }

            string configPath = Path.Combine(extensionRoot, "Trial", configName + ".xml").Replace('\\', '/');
            if (!File.Exists(configPath))
            {
                Console.WriteLine($"[KernelExtensions] Error: Trial config '{configName}.xml' not found at '{configPath}'. Please ensure the file exists and the flag '{flag}' is correct.");
                isExiting = true;
                return;
            }

            // 反序列化 XML 为 TrialConfig 对象
            try
            {
                XmlSerializer serializer = new XmlSerializer(typeof(TrialConfig));
                using (FileStream fs = new FileStream(configPath, FileMode.Open))
                    config = (TrialConfig)serializer.Deserialize(fs);
            }
            catch (Exception e)
            {
                Console.WriteLine($"[KernelExtensions] Error loading config: {e.Message}");
                isExiting = true;
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

            // 这什么，怎么还有LDT的名
            if (colorStr.Equals("LDTchara", StringComparison.OrdinalIgnoreCase))
            {
                // 使用 HSV 转换，色调随时间变化
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
                // 解析失败返回透明
                return Color.Transparent;
            }
        }

        /// <summary>
        /// HSV 转 RGB 辅助方法（用于别的什么用途或者彩蛋，请勿删除）。
        /// </summary>
        private Color HSVToColor(float hue, float saturation, float value)
        {
            // hue: 0-1, saturation: 0-1, value: 0-1
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

            stateTimer += t; // 当前状态计时器累加

            switch (currentState)
            {
                case RunState.NotStarted:
                    // 等待玩家点击按钮，无更新逻辑
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
                case RunState.MailIconDestroy:
                    UpdateMailIconDestroy(t);
                    if (stateTimer >= config.MailIconDestroyDuration)
                        TransitionToNextState();
                    break;
                case RunState.AssignMission:
                    UpdateAssignMission(t);
                    break;
                case RunState.OnMission:
                    // 阶段计时器更新（仅在 OnMission 状态且任务未完成时递减）
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
                        isExiting = true;
                    }
                    break;
            }

            // 更新特效
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
                    // 点击开始按钮后：禁止 kill，切换音乐，启动全局计时器
                    this.CanBeKilled = false;
                    if (!string.IsNullOrEmpty(config.TrialStartMusic))
                        MusicManager.transitionToSong(config.TrialStartMusic);
                    if (config.GlobalTimeout > 0)
                    {
                        globalTimerRemaining = config.GlobalTimeout;
                        globalTimerActive = true;
                    }
                    // 执行试炼开始时的动作（如果配置了）
                    ExecuteActionFile(config.OnStart?.FilePath);
                    currentState = RunState.SpinningUp;
                    break;

                case RunState.SpinningUp:
                    // 根据配置决定是否进入特效状态
                    if (config.EnableFlickering)
                        currentState = RunState.Flickering;
                    else if (config.EnableMailIconDestroy)
                        currentState = RunState.MailIconDestroy;
                    else
                        StartNextPhase(); // 无特效则直接开始第一阶段
                    break;

                case RunState.Flickering:
                    if (config.EnableMailIconDestroy)
                        currentState = RunState.MailIconDestroy;
                    else
                        StartNextPhase();
                    break;

                case RunState.MailIconDestroy:
                    StartNextPhase();
                    break;

                case RunState.AssignMission:
                    // 描述文本输出完毕，进入任务执行阶段
                    currentState = RunState.OnMission;
                    LoadCurrentMission();           // 加载当前阶段的任务
                    missionCompleted = false;
                    // 启动阶段计时器（如果配置了超时）
                    if (currentPhase.Timeout > 0)
                    {
                        phaseTimerRemaining = currentPhase.Timeout;
                        phaseTimerActive = true;
                    }
                    else phaseTimerActive = false;
                    // 播放阶段音乐（如果有配置）
                    if (!string.IsNullOrEmpty(currentPhase.Music))
                        MusicManager.transitionToSong(currentPhase.Music);
                    // 注册追踪超时回调（用于失败处理）
                    if (currentPhase.Timeout > 0 && !traceOverrideActive)
                    {
                        os.traceCompleteOverrideAction += OnTraceTimeout;
                        traceOverrideActive = true;
                    }
                    break;

                case RunState.OnMission:
                    // 任务完成，执行阶段完成动作，然后进入下一阶段
                    ExecuteActionFile(currentPhase?.OnComplete?.FilePath);
                    currentPhaseIdx++;
                    if (currentPhaseIdx >= config.Phases.Count)
                    {
                        // 所有阶段完成，进入结束动画
                        currentState = RunState.Outro;
                        stateTimer = 0f;
                    }
                    else
                    {
                        // 切换到下一个阶段的 AssignMission
                        currentState = RunState.AssignMission;
                        PrepareAssignMission();
                    }
                    break;
            }
        }

        /// <summary>
        /// 开始执行第一个阶段（从 AssignMission 开始）。
        /// </summary>
        private void StartNextPhase()
        {
            currentPhaseIdx = 0;
            currentState = RunState.AssignMission;
            PrepareAssignMission();
        }

        /// <summary>
        /// 准备一个阶段的描述文本输出（重置逐字打印进度，并执行阶段开始动作）。
        /// </summary>
        private void PrepareAssignMission()
        {
            stateTimer = 0f;
            charsRenderedSoFar = 0;
            string rawText = currentPhase.DescriptionText;
            // 尝试作为文件路径解析（相对于扩展根目录）
            string resolvedPath = ResolvePath(rawText);
            if (resolvedPath != null && File.Exists(resolvedPath))
                currentDisplayText = Utils.readEntireFile(resolvedPath);
            else
                currentDisplayText = rawText ?? ""; // 当作直接文本
            // 不再输出标题和副标题到终端，仅通过 DrawPhaseTitle 绘制
            // 执行阶段开始时的动作（新增功能）
            ExecuteActionFile(currentPhase.OnPhaseStart?.FilePath);
        }

        /// <summary>
        /// 逐字打印任务描述文本，支持 % 和 %% 停顿。
        /// </summary>
        private void UpdateAssignMission(float t)
        {
            if (string.IsNullOrEmpty(currentDisplayText))
            {
                TransitionToNextState(); // 没有文本则直接进入下一状态
                return;
            }

            // 调用原版方法逐字输出，自动支持 % 和 %% 停顿
            charsRenderedSoFar = TextWriterTimed.WriteTextToTerminal(
                currentDisplayText, os, 0.04f, 1f, 20f, stateTimer, charsRenderedSoFar);
            if (charsRenderedSoFar >= currentDisplayText.Length)
                TransitionToNextState(); // 全部输出完毕
        }

        /// <summary>
        /// 加载当前阶段的任务 XML。
        /// </summary>
        private void LoadCurrentMission()
        {
            if (!string.IsNullOrEmpty(currentPhase.MissionFile))
            {
                string missionPath = ResolvePath(currentPhase.MissionFile);
                if (missionPath == null || !File.Exists(missionPath))
                {
                    os.write($"Error: Mission file not found: {currentPhase.MissionFile}");
                    currentMission = null;
                }
                else
                {
                    currentMission = (ActiveMission)ComputerLoader.readMission(missionPath);
                }
            }
            else currentMission = null;
        }

        /// <summary>
        /// 任务执行阶段的更新：检测任务是否完成。
        /// </summary>
        private void UpdateOnMission(float t)
        {
            if (missionCompleted) return;
            if (currentMission != null && currentMission.isComplete(null))
            {
                missionCompleted = true;
                TransitionToNextState(); // 任务完成，进入下一阶段
            }
        }

        // ---------- 失败处理（不再输出失败信息到终端） ----------
        private void OnPhaseTimeout()
        {
            // 执行阶段失败动作（如果配置了）
            ExecuteActionFile(currentPhase?.OnFail?.FilePath);
            // 不再输出 os.write("Trial failed due to phase timeout.");
            if (currentPhase.EnableResetOnFail)
                ResetCurrentPhase();      // 重置当前阶段，重新开始
            else
                isExiting = true;         // 直接退出
        }

        private void OnGlobalTimeout()
        {
            ExecuteActionFile(config.OnGlobalFail?.FilePath);
            // 不再输出 os.write("Trial failed due to global timeout.");
            isExiting = true;
        }

        private void OnTraceTimeout()
        {
            ExecuteActionFile(currentPhase?.OnFail?.FilePath);
            // 不再输出 os.write("Trial failed due to trace timeout.");
            if (currentPhase.EnableResetOnFail)
                ResetCurrentPhase();
            else
                isExiting = true;
        }

        /// <summary>
        /// 重置当前阶段：重新输出描述文本、重新加载任务、重置阶段计时器。
        /// </summary>
        private void ResetCurrentPhase()
        {
            // 重置状态到 AssignMission，保持 currentPhaseIdx 不变
            currentState = RunState.AssignMission;
            PrepareAssignMission();       // 重新准备描述文本
            // 重置阶段计时器（如果超时时间 > 0）
            if (currentPhase.Timeout > 0)
            {
                phaseTimerRemaining = currentPhase.Timeout;
                phaseTimerActive = true;
            }
            else phaseTimerActive = false;
            // 重置任务完成标志
            missionCompleted = false;
            // 重新加载任务
            LoadCurrentMission();
            // 注意：不重置全局计时器，全局计时器继续倒计时
        }

        /// <summary>
        /// 删除当前试炼对应的 CustomTrial_ Flag，防止下次启动时自动加载同一配置。
        /// </summary>
        private void DeleteCurrentTrialFlag()
        {
            string currentFlag = os.Flags.GetFlagStartingWith("CustomTrial_");
            if (!string.IsNullOrEmpty(currentFlag))
            {
                os.Flags.RemoveFlag(currentFlag);
                Console.WriteLine($"[KernelExtensions] Removed flag: {currentFlag}");
            }
        }

        // ---------- 动作执行 ----------
        /// <summary>
        /// 执行外部动作 XML 文件（使用 Hacknet 原生的 SerializableAction）。
        /// 支持 <Actions> 包裹或多个动作平铺两种格式。
        /// </summary>
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
                using (FileStream fs = new FileStream(fullPath, FileMode.Open))
                using (XmlReader reader = XmlReader.Create(fs))
                {
                    reader.MoveToContent();
                    // 格式1：根元素为 <Actions>
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
                    // 格式2：根元素直接是动作元素（或多个平铺）
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
            }
            catch (Exception e)
            {
                os.write($"Error executing actions: {e.Message}");
            }
        }

        // ---------- 节点摧毁特效 ----------
        private void UpdateFlickering(float t)
        {
            if (!config.EnableFlickering) return;

            // 第一次进入时计算动态摧毁间隔（与原版一致：总时长 / 2 / 节点数）
            if (!hasStartedNodeDestruction && config.EnableNodeDestruction && os.netMap.visibleNodes.Count > 1)
            {
                int nodeCount = os.netMap.visibleNodes.Count;
                nodeRemovalInterval = (nodeCount > 0) ? config.FlickeringDuration / 2f / nodeCount : 0.5f;
                hasStartedNodeDestruction = true;
            }

            if (!config.EnableNodeDestruction) return; // 未启用节点摧毁
            if (os.netMap.visibleNodes.Count <= 1) return; // 只剩玩家节点，不再摧毁

            nodeRemovalTimer += t;
            if (nodeRemovalTimer >= nodeRemovalInterval)
            {
                nodeRemovalTimer -= nodeRemovalInterval;
                RemoveRandomNode();
            }
        }

        /// <summary>
        /// 随机删除一个非玩家节点。
        /// </summary>
        private void RemoveRandomNode()
        {
            var visible = os.netMap.visibleNodes; // List<int> 节点索引
            if (visible.Count <= 1) return;

            // 收集非玩家节点的索引
            List<int> nonPlayerIndices = new List<int>();
            foreach (int idx in visible)
                if (os.netMap.nodes[idx] != os.thisComputer)
                    nonPlayerIndices.Add(idx);
            if (nonPlayerIndices.Count == 0) return;

            int randomIdx = Utils.random.Next(nonPlayerIndices.Count);
            int nodeIdx = nonPlayerIndices[randomIdx];
            var comp = os.netMap.nodes[nodeIdx];
            Vector2 screenPos = comp.getScreenSpacePosition();
            // 添加冲击波特效
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
            // 定期添加红色圆圈特效
            if (progress > 0.2f && stateTimer % 0.6f <= t)
                SFX.addCircle(os.mailicon.pos + new Vector2(20f, 6f), Utils.AddativeRed, 100f + 200f * progress);
            // 顶部栏图标随机闪烁红色
            os.topBarIconsColor = (Utils.randm(1f) < progress) ? Color.Red : originalTopBarIconsColor;
            // 爆炸完成
            if (progress >= 0.99f)
            {
                os.DisableEmailIcon = true;
                breakSound.Play();
                explosion.Explode(1500, new Vector2(-0.1f, 3.1415926f), os.mailicon.pos + new Vector2(20f, 6f), 1f, 8f, 100f, 1600f, 1000f, 1200f, 3f, 7f);
                // 注意：这里没有显式恢复顶部栏颜色，因为爆炸后程序会进入 AssignMission 状态，
                // 该状态下顶部栏颜色会被系统重新设定，且原版 DLC 也未恢复，因此保留原逻辑。
            }
        }

        /// <summary>
        /// 更新冲击波特效列表，移除过时的特效。
        /// </summary>
        private void UpdateImpactEffects(float t)
        {
            for (int i = 0; i < impactEffects.Count; i++)
            {
                var effect = impactEffects[i];
                effect.timeEnabled += t;
                if (effect.timeEnabled > 5f) // 特效存活超过5秒则移除
                    impactEffects.RemoveAt(i--);
                else
                    impactEffects[i] = effect;
            }
        }

        // ---------- 绘制 ----------
        public override void Draw(float t)
        {
            base.Draw(t);
            drawTarget();    // 绘制目标指示
            drawOutline();   // 绘制边框

            // 程序窗口内的可用内容区域（减去顶部面板）
            Rectangle contentRect = new Rectangle(bounds.X + 2, bounds.Y + Module.PANEL_HEIGHT + 2, bounds.Width - 4, bounds.Height - Module.PANEL_HEIGHT - 4);

            // 如果试炼被锁定，显示锁定消息
            if (trialLocked || config == null)
            {
                DrawLockedMessage(contentRect);
                return;
            }

            // 绘制自定义背景（如果配置了背景色）
            if (cachedBackgroundColor != Color.Transparent)
            {
                RenderedRectangle.doRectangle(contentRect.X, contentRect.Y, contentRect.Width, contentRect.Height, cachedBackgroundColor);
            }

            // 根据当前状态绘制不同内容
            switch (currentState)
            {
                case RunState.NotStarted:
                    DrawStartScreen(contentRect);
                    break;
                case RunState.SpinningUp:
                    DrawSpinningUp(contentRect);
                    break;
                case RunState.Flickering:
                case RunState.MailIconDestroy:
                case RunState.AssignMission:
                case RunState.OnMission:
                    DrawPhaseTitle(contentRect);
                    break;
            }

            DrawImpactEffects();
            explosion.Render(spriteBatch);

            // 绘制计时器（放在程序窗口底部，避免遮挡标题）
            int timerY = bounds.Y + bounds.Height - 70; // 底部偏移
            if (globalTimerActive && config.EnableGlobalTimer && globalTimerRemaining > 0)
            {
                Rectangle globalRect = new Rectangle(bounds.X + 10, timerY, bounds.Width - 20, 25);
                DrawTimerBar(globalRect, globalTimerRemaining / config.GlobalTimeout, globalTimerRemaining, cachedGlobalTimerColor);
                timerY -= 30; // 为阶段计时器留出空间
            }
            if (phaseTimerActive && config.EnablePhaseTimer && phaseTimerRemaining > 0)
            {
                Rectangle phaseRect = new Rectangle(bounds.X + 10, timerY, bounds.Width - 20, 25);
                DrawTimerBar(phaseRect, phaseTimerRemaining / currentPhase.Timeout, phaseTimerRemaining, cachedPhaseTimerColor);
            }
        }

        /// <summary>
        /// 绘制一个计时条（背景、进度填充、边框、右侧文字）。
        /// </summary>
        /// <param name="rect">计时条区域</param>
        /// <param name="percent">完成百分比 0-1</param>
        /// <param name="remaining">剩余秒数</param>
        /// <param name="customColor">自定义颜色（若为透明则使用主题高亮色）</param>
        private void DrawTimerBar(Rectangle rect, float percent, float remaining, Color customColor)
        {
            Color useColor = (customColor != Color.Transparent) ? customColor : os.highlightColor;

            // 背景
            RenderedRectangle.doRectangle(rect.X, rect.Y, rect.Width, rect.Height, Color.Black * 0.7f);
            // 进度填充
            int fillWidth = (int)(rect.Width * Math.Max(0, Math.Min(1, percent)));
            RenderedRectangle.doRectangle(rect.X, rect.Y, fillWidth, rect.Height, useColor);
            // 边框
            RenderedRectangle.doRectangleOutline(rect.X, rect.Y, rect.Width, rect.Height, 1, Color.White);
            // 右侧显示剩余秒数
            string timerText = $"{(int)remaining}s";
            Vector2 textSize = GuiData.font.MeasureString(timerText);
            Vector2 textPos = new Vector2(rect.X + rect.Width - textSize.X - 5, rect.Y + (rect.Height - textSize.Y) / 2);
            spriteBatch.DrawString(GuiData.font, timerText, textPos, Color.White);
        }

        /// <summary>
        /// 绘制锁定消息（在原本开始按钮的位置显示文字）。
        /// </summary>
        private void DrawLockedMessage(Rectangle rect)
        {
            backgroundEffect.Update(0f);
            backgroundEffect.Draw(rect, spriteBatch, Color.Black, os.highlightColor * 0.2f, HexGridBackground.ColoringAlgorithm.CorrectedSinWash, 0f);
            string lockText = GetLocalizedLockedText();
            int btnHeight = 30;
            Rectangle textRect = new Rectangle(rect.X + 10, rect.Y + rect.Height / 2 - btnHeight / 2, rect.Width - 20, btnHeight);
            spriteBatch.Draw(Utils.white, textRect, Color.Black * 0.6f);
            TextItem.doCenteredFontLabel(textRect, lockText, GuiData.font, Color.Red, false);
        }

        /// <summary>
        /// 绘制开始屏幕（背景 + 按钮）。
        /// </summary>
        private void DrawStartScreen(Rectangle rect)
        {
            backgroundEffect.Update(0f);
            backgroundEffect.Draw(rect, spriteBatch, Color.Black, os.highlightColor * 0.2f, HexGridBackground.ColoringAlgorithm.CorrectedSinWash, 0f);
            int btnHeight = 30;
            Rectangle btnRect = new Rectangle(rect.X + 10, rect.Y + rect.Height / 2 - btnHeight / 2, rect.Width - 20, btnHeight);
            string buttonText = GetLocalizedBeginButton();
            if (Button.doButton(8310101 + PID, btnRect.X, btnRect.Y, btnRect.Width, btnRect.Height, buttonText, new Color?(os.highlightColor)))
                TransitionToNextState();
        }

        /// <summary>
        /// 多语言按钮文本（硬编码，根据游戏当前语言返回）。
        /// </summary>
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

        /// <summary>
        /// 多语言锁定文字。
        /// </summary>
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

        /// <summary>
        /// 绘制旋转进度条（逐行扫描），支持自定义颜色。
        /// </summary>
        private void DrawSpinningUp(Rectangle rect)
        {
            Color spinColor = (cachedSpinUpColor != Color.Transparent) ? cachedSpinUpColor : os.highlightColor;
            Utils.LCG.reSeed(PID);
            for (int y = 0; y < rect.Height; y++)
            {
                float noise = Utils.LCG.NextFloatScaled();
                float lineProgress = Math.Min(1f, stateTimer / (config.SpinUpDuration * noise));
                int width = (int)(lineProgress * rect.Width);
                Color col = Color.Lerp(Utils.AddativeWhite * 0.1f, spinColor, Utils.LCG.NextFloatScaled());
                spriteBatch.Draw(Utils.white, new Rectangle(rect.X, rect.Y + y, width, 1), col);
            }
        }

        /// <summary>
        /// 绘制阶段标题（在窗口中央），标题和副标题仅在此处显示，不再输出到终端。
        /// </summary>
        private void DrawPhaseTitle(Rectangle rect)
        {
            backgroundEffect.Update(0f);
            backgroundEffect.Draw(rect, spriteBatch, Color.Black, os.highlightColor * 0.2f, HexGridBackground.ColoringAlgorithm.CorrectedSinWash, 0f);
            int titleHeight = 40;
            // 标题垂直居中于内容区域的上半部分
            Rectangle titleRect = new Rectangle(rect.X, rect.Y + rect.Height / 3 - titleHeight / 2, rect.Width, titleHeight);
            spriteBatch.Draw(Utils.white, titleRect, Color.Black * 0.6f);
            TextItem.doFontLabelToSize(titleRect, currentPhase?.Title ?? "", GuiData.font, Color.White, true, false);
            // 如果有副标题，在标题下方绘制
            if (!string.IsNullOrEmpty(currentPhase?.Subtitle))
            {
                int subtitleHeight = 22;
                Rectangle subtitleRect = new Rectangle(rect.X, titleRect.Y + titleRect.Height + 5, rect.Width, subtitleHeight);
                spriteBatch.Draw(Utils.white, subtitleRect, Color.Black * 0.4f);
                TextItem.doFontLabelToSize(subtitleRect, currentPhase.Subtitle, GuiData.font, Utils.AddativeWhite * 0.9f, true, false);
            }
        }

        /// <summary>
        /// 绘制节点消失冲击波特效。
        /// </summary>
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
    }
}