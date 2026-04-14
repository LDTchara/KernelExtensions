using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using Hacknet;
using Hacknet.Effects;
using Hacknet.Extensions;
using Hacknet.Gui;
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
    /// 功能：旋转动画、可选特效、多阶段任务、逐字打印（支持%停顿）、超时重置、外部动作执行等。
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
        private RunState currentState = RunState.NotStarted;
        private int currentPhaseIdx = -1;          // 当前执行的阶段索引
        private float stateTimer = 0f;             // 当前状态的计时器
        private TrialConfig config;                // 加载的配置对象

        // 辅助属性：获取当前阶段配置
        private PhaseConfig currentPhase => (currentPhaseIdx >= 0 && currentPhaseIdx < config.Phases.Count) ? config.Phases[currentPhaseIdx] : null;

        // ---------- 逐字打印相关（使用 TextWriterTimed） ----------
        private string currentDisplayText = "";
        private int charsRenderedSoFar = 0;        // 已输出的字符数
        private float textWriterTimer = 0f;        // 用于 TextWriterTimed 的计时（实际未使用，但保留接口）

        // ---------- 任务相关 ----------
        private ActiveMission currentMission;
        private bool missionCompleted = false;
        private bool traceOverrideActive = false;

        // ---------- 独立计时器（超时倒计时） ----------
        private float customTimerRemaining = 0f;
        private bool showCustomTimer = false;

        // ---------- 特效相关 ----------
        private List<TraceKillExe.PointImpactEffect> impactEffects = new List<TraceKillExe.PointImpactEffect>();
        private float nodeRemovalTimer = 0f;
        private float nodeRemovalInterval = 0f;     // 动态计算的节点摧毁间隔
        private Color originalTopBarIconsColor;
        private HexGridBackground backgroundEffect;
        private ExplodingUIElementEffect explosion = new ExplodingUIElementEffect();
        private SoundEffect breakSound;
        private Texture2D circle;
        private Texture2D circleOutline;
        private bool hasStartedNodeDestruction = false; // 标记是否已开始摧毁节点

        // ---------- 扩展根目录 ----------
        private string extensionRoot = null;

        // ---------- 试炼锁定标志 ----------
        private bool trialLocked = false;           // 是否因缺少 Flag 而锁定

        // ---------- 构造函数 ----------
        public CustomTrialExe() : base()
        {
            this.ramCost = 190;
            this.IdentifierName = "CustomTrial";
            this.name = "CustomTrial";
            this.CanBeKilled = true;   // 初始允许 kill
        }

        // ---------- 初始化（类似 LoadContent） ----------
        public override void OnInitialize()
        {
            base.OnInitialize();

            // 获取扩展根目录
            if (ExtensionLoader.ActiveExtensionInfo != null)
                extensionRoot = ExtensionLoader.ActiveExtensionInfo.FolderPath.Replace('\\', '/');

            // 1. 检查是否存在 CustomTrial_ Flag
            string flag = os.Flags.GetFlagStartingWith("CustomTrial_");
            if (string.IsNullOrEmpty(flag))
            {
                // 没有找到任何试炼 Flag → 锁定试炼，输出日志
                trialLocked = true;
                Console.WriteLine("[KernelExtensions] No CustomTrial_ flag found. Trial locked.");
                config = null;
                return;
            }

            // 2. 加载配置（从 Flag 读取配置名）
            LoadConfig();

            if (config == null)
            {
                // 配置加载失败（例如文件不存在），也视为锁定，输出日志
                trialLocked = true;
                Console.WriteLine("[KernelExtensions] Failed to load trial config. Check that the config file exists and is valid.");
                return;
            }

            // 3. 保存原始顶部栏图标颜色
            originalTopBarIconsColor = os.topBarIconsColor;

            // 4. 初始化特效系统
            backgroundEffect = new HexGridBackground(os.content);
            explosion.Init(os.content);
            breakSound = os.content.Load<SoundEffect>("SFX/DoomShock");

            // 5. 加载纹理
            circle = os.content.Load<Texture2D>("Circle");
            circleOutline = os.content.Load<Texture2D>("CircleOutlineLarge");

            // 6. 播放启动音乐
            if (!string.IsNullOrEmpty(config.StartMusic))
                MusicManager.transitionToSong(config.StartMusic);
        }

        /// <summary>
        /// 将相对路径转换为基于扩展根目录的绝对路径。
        /// </summary>
        private string ResolvePath(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath)) return null;
            if (Path.IsPathRooted(relativePath)) return relativePath;

            if (string.IsNullOrEmpty(extensionRoot))
            {
                Console.WriteLine("[KernelExtensions] Error: No extension root found. This mod must be run as part of an extension.");
                return null;
            }

            return Path.Combine(extensionRoot, relativePath).Replace('\\', '/');
        }

        // ---------- 加载配置 ----------
        private void LoadConfig()
        {
            string flag = os.Flags.GetFlagStartingWith("CustomTrial_");
            string configName = "Default";
            if (!string.IsNullOrEmpty(flag) && flag.StartsWith("CustomTrial_"))
            {
                configName = flag.Substring("CustomTrial_".Length);
                if (string.IsNullOrEmpty(configName)) configName = "Default";
            }

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

        // ---------- 每帧更新 ----------
        public override void Update(float t)
        {
            base.Update(t);
            if (config == null) return; // 锁定或加载失败时无动作

            stateTimer += t;

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
                case RunState.MailIconDestroy:
                    UpdateMailIconDestroy(t);
                    if (stateTimer >= config.MailIconDestroyDuration)
                        TransitionToNextState();
                    break;
                case RunState.AssignMission:
                    UpdateAssignMission(t);
                    break;
                case RunState.OnMission:
                    UpdateOnMission(t);
                    break;
                case RunState.Outro:
                    if (stateTimer >= 2f)
                    {
                        ExecuteActionFile(config.OnComplete?.FilePath);
                        isExiting = true;
                    }
                    break;
            }

            explosion.Update(t);
            UpdateImpactEffects(t);
        }

        // ---------- 状态转换 ----------
        private void TransitionToNextState()
        {
            stateTimer = 0f;
            switch (currentState)
            {
                case RunState.NotStarted:
                    this.CanBeKilled = false;
                    if (!string.IsNullOrEmpty(config.TrialStartMusic))
                        MusicManager.transitionToSong(config.TrialStartMusic);
                    currentState = RunState.SpinningUp;
                    break;

                case RunState.SpinningUp:
                    if (config.EnableFlickering)
                        currentState = RunState.Flickering;
                    else if (config.EnableMailIconDestroy)
                        currentState = RunState.MailIconDestroy;
                    else
                        StartNextPhase();
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
                    currentState = RunState.OnMission;
                    LoadCurrentMission();
                    missionCompleted = false;
                    if (currentPhase.Timeout > 0)
                    {
                        customTimerRemaining = currentPhase.Timeout;
                        showCustomTimer = true;
                    }
                    else showCustomTimer = false;
                    if (!string.IsNullOrEmpty(currentPhase.Music))
                        MusicManager.transitionToSong(currentPhase.Music);
                    if (currentPhase.Timeout > 0)
                    {
                        if (!traceOverrideActive)
                        {
                            os.traceCompleteOverrideAction += OnTraceTimeout;
                            traceOverrideActive = true;
                        }
                    }
                    break;

                case RunState.OnMission:
                    ExecuteActionFile(currentPhase?.OnComplete?.FilePath);
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

        private void StartNextPhase()
        {
            currentPhaseIdx = 0;
            currentState = RunState.AssignMission;
            PrepareAssignMission();
        }

        private void PrepareAssignMission()
        {
            stateTimer = 0f;
            charsRenderedSoFar = 0;
            string rawText = currentPhase.DescriptionText;
            string resolvedPath = ResolvePath(rawText);
            if (resolvedPath != null && File.Exists(resolvedPath))
                currentDisplayText = Utils.readEntireFile(resolvedPath);
            else
                currentDisplayText = rawText ?? "";
            os.write($"\n--- {currentPhase.Title} ---");
            if (!string.IsNullOrEmpty(currentPhase.Subtitle))
                os.write(currentPhase.Subtitle.Replace("\\n", "\n"));
            os.write("");
            showCustomTimer = false;
        }

        // ---------- 使用 TextWriterTimed 支持停顿 ----------
        private void UpdateAssignMission(float t)
        {
            if (string.IsNullOrEmpty(currentDisplayText))
            {
                TransitionToNextState();
                return;
            }

            // 使用原版 TextWriterTimed 逐字输出，自动支持 % 和 %% 停顿
            charsRenderedSoFar = TextWriterTimed.WriteTextToTerminal(
                currentDisplayText,
                os,
                0.04f,      // 每个字符的基础间隔
                1f,         // 行间距（无关紧要）
                20f,        // 每行最大宽度（无关紧要）
                stateTimer, // 当前状态计时器
                charsRenderedSoFar
            );

            if (charsRenderedSoFar >= currentDisplayText.Length)
                TransitionToNextState();
        }

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

        private void UpdateOnMission(float t)
        {
            if (missionCompleted) return;

            if (showCustomTimer && customTimerRemaining > 0)
            {
                customTimerRemaining -= t;
                if (customTimerRemaining <= 0)
                {
                    OnMissionFailed();
                    return;
                }
            }

            if (currentMission != null && currentMission.isComplete(null))
            {
                missionCompleted = true;
                TransitionToNextState();
            }
        }

        private void OnMissionFailed()
        {
            ExecuteActionFile(currentPhase?.OnFail?.FilePath);
            currentState = RunState.AssignMission;
            PrepareAssignMission();
        }

        private void OnTraceTimeout() => OnMissionFailed();

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
                            else
                            {
                                reader.Read();
                            }
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
            }
            catch (Exception e)
            {
                os.write($"Error executing actions: {e.Message}");
            }
        }

        // ---------- 节点摧毁特效（动态间隔，可配置开关，跳过玩家节点，直到只剩玩家节点） ----------
        private void UpdateFlickering(float t)
        {
            if (!config.EnableFlickering) return;

            // 动态计算摧毁间隔（与原版一致：FlickeringDuration / 2 / 当前可见节点数）
            if (!hasStartedNodeDestruction && config.EnableNodeDestruction && os.netMap.visibleNodes.Count > 1)
            {
                int nodeCount = os.netMap.visibleNodes.Count;
                if (nodeCount > 0)
                    nodeRemovalInterval = config.FlickeringDuration / 2f / nodeCount;
                else
                    nodeRemovalInterval = 0.5f; // fallback
                hasStartedNodeDestruction = true;
            }

            if (!config.EnableNodeDestruction) return;

            // 如果可见节点只剩玩家节点，则不再摧毁
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
            var visible = os.netMap.visibleNodes; // List<int> 节点索引
            if (visible.Count <= 1) return;

            // 收集非玩家节点的索引
            List<int> nonPlayerIndices = new List<int>();
            foreach (int idx in visible)
            {
                if (os.netMap.nodes[idx] != os.thisComputer)
                    nonPlayerIndices.Add(idx);
            }
            if (nonPlayerIndices.Count == 0) return;

            int randomIdx = Utils.random.Next(nonPlayerIndices.Count);
            int nodeIdx = nonPlayerIndices[randomIdx];
            var comp = os.netMap.nodes[nodeIdx];
            Vector2 screenPos = comp.getScreenSpacePosition();
            impactEffects.Add(new TraceKillExe.PointImpactEffect
            {
                location = screenPos,
                scaleModifier = 3f,
                cne = new ConnectedNodeEffect(os, true),
                timeEnabled = 0f,
                HasHighlightCircle = true
            });
            visible.Remove(nodeIdx); // 现在类型匹配：int
        }

        // ---------- 邮件爆炸特效 ----------
        private void UpdateMailIconDestroy(float t)
        {
            float progress = Math.Min(1f, stateTimer / config.MailIconDestroyDuration);
            if (progress > 0.2f && stateTimer % 0.6f <= t)
                SFX.addCircle(os.mailicon.pos + new Vector2(20f, 6f), Utils.AddativeRed, 100f + 200f * progress);
            os.topBarIconsColor = (Utils.randm(1f) < progress) ? Color.Red : originalTopBarIconsColor;
            if (progress >= 0.99f)
            {
                os.DisableEmailIcon = true;
                breakSound.Play();
                explosion.Explode(1500, new Vector2(-0.1f, 3.1415926f), os.mailicon.pos + new Vector2(20f, 6f), 1f, 8f, 100f, 1600f, 1000f, 1200f, 3f, 7f);
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

        // ---------- 绘制 ----------
        public override void Draw(float t)
        {
            base.Draw(t);
            drawTarget();
            drawOutline();

            Rectangle contentRect = new Rectangle(bounds.X + 2, bounds.Y + Module.PANEL_HEIGHT + 2, bounds.Width - 4, bounds.Height - Module.PANEL_HEIGHT - 4);

            // 如果试炼被锁定，在开始按钮的位置显示锁定文字
            if (trialLocked || config == null)
            {
                DrawLockedMessage(contentRect);
                return;
            }

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

            if (showCustomTimer && customTimerRemaining > 0 && currentPhase != null)
            {
                Rectangle timerRect = new Rectangle(os.display.bounds.Width / 2 - 100, 10, 200, 30);
                float percent = customTimerRemaining / currentPhase.Timeout;
                RenderedRectangle.doRectangle(timerRect.X, timerRect.Y, timerRect.Width, timerRect.Height, Color.Black * 0.7f);
                RenderedRectangle.doRectangle(timerRect.X, timerRect.Y, (int)(timerRect.Width * percent), timerRect.Height, Color.Red);
                RenderedRectangle.doRectangleOutline(timerRect.X, timerRect.Y, timerRect.Width, timerRect.Height, 1, Color.White);
                TextItem.doCenteredFontLabel(timerRect, $"{(int)customTimerRemaining}s", GuiData.font, Color.White, false);
            }
        }

        /// <summary>
        /// 绘制锁定消息（在原本开始按钮的位置显示文字）
        /// </summary>
        private void DrawLockedMessage(Rectangle rect)
        {
            backgroundEffect.Update(0f);
            backgroundEffect.Draw(rect, spriteBatch, Color.Black, os.highlightColor * 0.2f, HexGridBackground.ColoringAlgorithm.CorrectedSinWash, 0f);

            string lockText = GetLocalizedLockedText();
            int btnHeight = 30;
            Rectangle textRect = new Rectangle(rect.X + 10, rect.Y + rect.Height / 2 - btnHeight / 2, rect.Width - 20, btnHeight);
            // 绘制半透明黑色背景
            spriteBatch.Draw(Utils.white, textRect, Color.Black * 0.6f);
            TextItem.doCenteredFontLabel(textRect, lockText, GuiData.font, Color.Red, false);
        }

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

        private void DrawSpinningUp(Rectangle rect)
        {
            Utils.LCG.reSeed(PID);
            for (int y = 0; y < rect.Height; y++)
            {
                float noise = Utils.LCG.NextFloatScaled();
                float lineProgress = Math.Min(1f, stateTimer / (config.SpinUpDuration * noise));
                int width = (int)(lineProgress * rect.Width);
                Color col = Color.Lerp(Utils.AddativeWhite * 0.1f, os.highlightColor, Utils.LCG.NextFloatScaled());
                spriteBatch.Draw(Utils.white, new Rectangle(rect.X, rect.Y + y, width, 1), col);
            }
        }

        private void DrawPhaseTitle(Rectangle rect)
        {
            backgroundEffect.Update(0f);
            backgroundEffect.Draw(rect, spriteBatch, Color.Black, os.highlightColor * 0.2f, HexGridBackground.ColoringAlgorithm.CorrectedSinWash, 0f);
            int titleHeight = 40;
            Rectangle titleRect = new Rectangle(rect.X, rect.Y + rect.Height / 3 - titleHeight / 2, rect.Width, titleHeight);
            spriteBatch.Draw(Utils.white, titleRect, Color.Black * 0.6f);
            TextItem.doFontLabelToSize(titleRect, currentPhase?.Title ?? "", GuiData.font, Color.White, true, false);
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
    }
}