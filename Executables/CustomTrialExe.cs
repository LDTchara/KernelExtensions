using BepInEx;
using Hacknet;
using Hacknet.Effects;
using Hacknet.Gui;
using KernelExtensions.Config;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Pathfinder.Executable;
using Pathfinder.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;

namespace KernelExtensions.Executables
{
    public class CustomTrialExe : GameExecutable
    {
        // 状态机
        private enum RunState
        {
            NotStarted,
            SpinningUp,
            Flickering,
            MailIconDestroy,
            AssignMission,
            OnMission,
            Outro,
            Exiting
        }

        private RunState currentState = RunState.NotStarted;
        private int currentPhaseIdx = -1;
        private float stateTimer = 0f;
        private TrialConfig config;
        private PhaseConfig currentPhase => (currentPhaseIdx >= 0 && currentPhaseIdx < config.Phases.Count) ? config.Phases[currentPhaseIdx] : null;

        // 逐字打印
        private string currentDisplayText = "";
        private int charsPrinted = 0;
        private float textPrintAccum = 0f;
        private const float PrintDelay = 0.04f;

        // 任务相关
        private ActiveMission currentMission;
        private bool missionCompleted = false;
        private bool traceOverrideActive = false;
        private float missionTimeoutTimer = 0f;

        // 独立计时器
        private float customTimerRemaining = 0f;
        private bool showCustomTimer = false;

        // 特效相关
        private List<TraceKillExe.PointImpactEffect> impactEffects = new List<TraceKillExe.PointImpactEffect>();
        private float nodeRemovalTimer = 0f;
        private const float NodeRemovalInterval = 1f;
        private Color originalTopBarIconsColor;
        private HexGridBackground backgroundEffect;
        private ExplodingUIElementEffect explosion = new ExplodingUIElementEffect();
        private SoundEffect breakSound;
        private bool canBeKilled = true;

        // 纹理资源
        private Texture2D circle;
        private Texture2D circleOutline;

        // 构造函数
        public CustomTrialExe() : base()
        {
            this.ramCost = 190;
            this.IdentifierName = "CustomTrial";
            this.name = "CustomTrial";
            this.CanBeKilled = true;  // 初始允许被 kill
        }

        // 初始化（相当于 LoadContent）
        public override void OnInitialize()
        {
            base.OnInitialize();

            // 暂时使用固定配置名 "Default"，后续可改为从 os.Flags 或全局变量读取
            LoadConfig(new string[] { "Default" });

            if (config == null)
            {
                os.write("Error: Failed to load trial config.");
                isExiting = true;
                return;
            }

            // 保存原始颜色
            originalTopBarIconsColor = os.topBarIconsColor;

            // 初始化特效
            backgroundEffect = new HexGridBackground(os.content);
            explosion.Init(os.content);
            breakSound = os.content.Load<SoundEffect>("SFX/DoomShock");

            // 加载纹理
            circle = os.content.Load<Texture2D>("Circle");
            circleOutline = os.content.Load<Texture2D>("CircleOutlineLarge");

            // 播放启动音乐
            if (!string.IsNullOrEmpty(config.StartMusic))
                MusicManager.transitionToSong(config.StartMusic);
        }

        private void LoadConfig(string[] args)
        {
            string configName = (args != null && args.Length > 0) ? args[0] : "Default";
            string configPath = Path.Combine(Paths.GameRootPath, "Mods", "KernelExtensions", "Trials", configName + ".xml");
            if (!File.Exists(configPath))
            {
                os.write($"Error: Trial config '{configName}.xml' not found.");
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
                os.write($"Error loading config: {e.Message}");
                isExiting = true;
            }
        }

        public override void Update(float t)
        {
            base.Update(t);
            if (config == null) return;

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
                    if (stateTimer >= 10f)
                        TransitionToNextState();
                    break;
                case RunState.MailIconDestroy:
                    UpdateMailIconDestroy(t);
                    if (stateTimer >= 3.82f)
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
                        // 执行最终完成动作（简化：直接输出文本，不再调用外部 Action）
                        os.write("All trials completed. Exiting.");
                        isExiting = true;
                    }
                    break;
            }
            explosion.Update(t);
            UpdateImpactEffects(t);
        }

        private void TransitionToNextState()
        {
            stateTimer = 0f;
            switch (currentState)
            {
                case RunState.NotStarted:
                    canBeKilled = false;  // 试炼开始后不可被 kill
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
                    missionTimeoutTimer = 0f;
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
                    // 任务完成，执行阶段完成动作（简化：输出文本）
                    os.write($"Phase {currentPhase.Id} completed.");
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
            charsPrinted = 0;
            textPrintAccum = 0f;
            string rawText = currentPhase.DescriptionText;
            if (File.Exists(rawText))
                currentDisplayText = Utils.readEntireFile(rawText);
            else
                currentDisplayText = rawText ?? "";
            os.write($"\n--- {currentPhase.Title} ---");
            if (!string.IsNullOrEmpty(currentPhase.Subtitle))
                os.write(currentPhase.Subtitle.Replace("\\n", "\n"));
            os.write("");
            showCustomTimer = false;
        }

        private void UpdateAssignMission(float t)
        {
            if (string.IsNullOrEmpty(currentDisplayText))
            {
                TransitionToNextState();
                return;
            }
            textPrintAccum += t;
            if (textPrintAccum >= PrintDelay && charsPrinted < currentDisplayText.Length)
            {
                textPrintAccum -= PrintDelay;
                // os.write 只接受一个字符串，不能带第二个 bool 参数，因此直接追加字符
                os.write(currentDisplayText[charsPrinted].ToString());
                charsPrinted++;
            }
            if (charsPrinted >= currentDisplayText.Length)
                TransitionToNextState();
        }

        private void LoadCurrentMission()
        {
            if (!string.IsNullOrEmpty(currentPhase.MissionFile))
            {
                string missionPath = currentPhase.MissionFile;
                if (!Path.IsPathRooted(missionPath))
                    missionPath = Path.Combine(Paths.GameRootPath, missionPath);
                currentMission = (ActiveMission)ComputerLoader.readMission(missionPath);
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
            // 失败处理：输出文本，重置当前阶段
            os.write($"Mission failed! Resetting phase {currentPhase.Id}.");
            currentState = RunState.AssignMission;
            PrepareAssignMission();
        }

        private void OnTraceTimeout()
        {
            OnMissionFailed();
        }

        // ========== 特效实现（与之前相同，但使用本地 circle 纹理） ==========
        private void UpdateFlickering(float t)
        {
            if (config.EnableFlickering && os.netMap.visibleNodes.Count > 1)
            {
                nodeRemovalTimer += t;
                if (nodeRemovalTimer >= NodeRemovalInterval)
                {
                    nodeRemovalTimer -= NodeRemovalInterval;
                    RemoveRandomNode();
                }
            }
        }

        private void RemoveRandomNode()
        {
            var visible = os.netMap.visibleNodes;
            if (visible.Count <= 1) return;
            int idx = Utils.random.Next(visible.Count);
            var comp = os.netMap.nodes[visible[idx]];
            if (comp == os.thisComputer) return;
            Vector2 screenPos = comp.getScreenSpacePosition();
            impactEffects.Add(new TraceKillExe.PointImpactEffect
            {
                location = screenPos,
                scaleModifier = 3f,
                cne = new ConnectedNodeEffect(os, true),
                timeEnabled = 0f,
                HasHighlightCircle = true
            });
            visible.RemoveAt(idx);
        }

        private void UpdateMailIconDestroy(float t)
        {
            float progress = Math.Min(1f, stateTimer / 3.82f);
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

        // ========== 绘制 ==========
        public override void Draw(float t)
        {
            base.Draw(t);
            drawTarget();
            drawOutline();

            Rectangle contentRect = new Rectangle(bounds.X + 2, bounds.Y + Module.PANEL_HEIGHT + 2, bounds.Width - 4, bounds.Height - Module.PANEL_HEIGHT - 4);

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

            // 绘制独立计时器（手动绘制进度条）
            if (showCustomTimer && customTimerRemaining > 0 && currentPhase != null)
            {
                Rectangle timerRect = new Rectangle(os.display.bounds.Width / 2 - 100, 10, 200, 30);
                float percent = customTimerRemaining / currentPhase.Timeout;
                // 背景
                RenderedRectangle.doRectangle(timerRect.X, timerRect.Y, timerRect.Width, timerRect.Height, Color.Black * 0.7f);
                // 填充
                RenderedRectangle.doRectangle(timerRect.X, timerRect.Y, (int)(timerRect.Width * percent), timerRect.Height, Color.Red);
                // 边框
                RenderedRectangle.doRectangleOutline(timerRect.X, timerRect.Y, timerRect.Width, timerRect.Height, 1, Color.White);
                // 文字
                TextItem.doCenteredFontLabel(timerRect, $"{(int)customTimerRemaining}s", GuiData.font, Color.White, false);
            }
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