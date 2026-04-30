using Hacknet;
using Hacknet.Extensions;
using Hacknet.Gui;
using Hacknet.Localization;
using Hacknet.UIUtils;
using KernelExtensions.Config;
using KernelExtensions.Utility;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Pathfinder.Replacements;
using Pathfinder.Util.XML;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace KernelExtensions.Modules
{
    /// <summary>
    /// VM 攻击恢复界面模块。
    /// 系统日志 → 引导文本（逐字，0.2 秒/字，行末 % 停顿，%% 长停）→ 交互按钮。
    /// </summary>
    public class FakeRecoveryModule : Module
    {
        private VMAttackConfig config;
        private enum Phase { SystemLog, GuideText, Interaction }
        private Phase currentPhase = Phase.SystemLog;
        private float stateTimer;
        private bool passwordSuccess;        // 密码验证通过，等待重启
        private float successTimer;          // 倒计时
        private const float SUCCESS_DELAY = 3f;

        // 所有已完成输出行（含系统日志和引导文本）
        private readonly List<(string text, bool monospace)> outputLines = new();

        // 系统日志
        private string[] systemLogLines;
        private int systemLogLineIndex;
        private float systemLogLineTimer;
        private const float SYS_LOG_INTERVAL = 0.08f;

        // 引导文本
        private List<string> guideLines;
        private int guideLineIndex;
        private int guideCharCount;
        private float guideCharTimer;
        private float guidePauseTimer;
        private const float GUIDE_CHAR_DELAY = 0.12f;
        private float currentDynamicSpeed; // 当前动态速度，初始值为 GUIDE_CHAR_DELAY
        // 新增两个缓存字段
        private string currentFullDisplayText = "";  // 当前行最终完整显示文本（已去除指令）
        private string currentPartialText = "";      // 当前行已打出的部分文本

        // 交互
        private bool interactionActivated;
        private string passwordInput = "";
        private KeyboardState prevKs;
        private bool showPasswordError = false;

        public FakeRecoveryModule(Rectangle bounds, OS os, VMAttackConfig config) : base(bounds, os)
        {
            this.config = config;
            this.bounds = bounds;
            this.visible = true;
            LoadLines();
            currentDynamicSpeed = GUIDE_CHAR_DELAY;
        }

        private enum TokenType { Char, Pause, Speed }

        private struct LineToken
        {
            public TokenType Type;
            public float Value;         // 停顿秒数 或 速度值
            public char Char;           // 普通字符
            public bool IsResetSpeed;   // 是否为恢复默认速度指令 ||SR||
        }

        /// <summary>
        /// 解析一行文本，返回令牌列表。识别 ||P0.5||、||S0.2||、||SR||
        /// </summary>
        private List<LineToken> ParseLineTokens(string line)
        {
            var tokens = new List<LineToken>();
            int i = 0;

            while (i < line.Length)
            {
                // 检查是否为指令开始标记 "||"
                if (i + 1 < line.Length && line[i] == '|' && line[i + 1] == '|')
                {
                    // 查找结束标记 "||"
                    int endIdx = line.IndexOf("||", i + 2);
                    if (endIdx > i + 2)
                    {
                        string cmd = line.Substring(i + 2, endIdx - i - 2);

                        if (cmd == "SR")
                        {
                            tokens.Add(new LineToken { Type = TokenType.Speed, IsResetSpeed = true });
                        }
                        else if (cmd.StartsWith("P") && float.TryParse(cmd.Substring(1), out float pauseVal))
                        {
                            tokens.Add(new LineToken { Type = TokenType.Pause, Value = pauseVal });
                        }
                        else if (cmd.StartsWith("S") && float.TryParse(cmd.Substring(1), out float speedVal))
                        {
                            tokens.Add(new LineToken { Type = TokenType.Speed, Value = speedVal });
                        }

                        i = endIdx + 2; // 跳过整个指令
                        continue;
                    }
                }

                // 普通字符
                tokens.Add(new LineToken { Type = TokenType.Char, Char = line[i] });
                i++;
            }

            return tokens;
        }

        /// <summary>
        /// 完全去除行内所有 ||xxx|| 标记，仅保留普通文本
        /// </summary>
        private string StripAllMarkers(string line)
        {
            var sb = new System.Text.StringBuilder();
            int i = 0;
            while (i < line.Length)
            {
                if (i + 1 < line.Length && line[i] == '|' && line[i + 1] == '|')
                {
                    int endIdx = line.IndexOf("||", i + 2);
                    if (endIdx > i + 2)
                    {
                        i = endIdx + 2;
                        continue;
                    }
                }
                sb.Append(line[i]);
                i++;
            }
            return sb.ToString();
        }

        /// <summary>
        /// 从令牌列表构建显示文本
        /// </summary>
        private string BuildDisplayText(List<LineToken> tokens)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var token in tokens)
            {
                if (token.Type == TokenType.Char)
                    sb.Append(token.Char);
            }
            return sb.ToString();
        }

        private static string GetLocalizedPasswordMatch()
        {
            string lang = Settings.ActiveLocale?.ToLowerInvariant() ?? "en-us";
            if (lang.StartsWith("zh")) return "检测结果：完全匹配。3秒后重启...";
            if (lang.StartsWith("ja")) return "照合結果：完全一致。3秒後に再起動します...";
            if (lang.StartsWith("ko")) return "결과: 완전히 일치합니다. 3초 후 다시 시작...";
            if (lang.StartsWith("ru")) return "РЕЗУЛЬТАТ: ПОЛНОЕ СОВПАДЕНИЕ. Перезапуск через 3 сек...";
            if (lang.StartsWith("de")) return "ERGEBNIS: VOLLE ÜBEREINSTIMMUNG. Neustart in 3 Sek...";
            if (lang.StartsWith("fr")) return "RÉSULTAT : CORRESPONDANCE PARFAITE. Redémarrage dans 3 s...";
            if (lang.StartsWith("es")) return "RESULTADO: COINCIDENCIA EXACTA. Reiniciando en 3 seg...";
            if (lang.StartsWith("tr")) return "SONUÇ: TAM EŞLEŞME. 3 saniye içinde yeniden başlatılıyor...";
            if (lang.StartsWith("nl")) return "RESULTAAT: VOLLEDIGE MATCH. Herstart over 3 seconden...";
            return "MATCH: FULL. Restarting in 3s...";
        }

        private static string GetLocalizedPasswordMismatch()
        {
            string lang = Settings.ActiveLocale?.ToLowerInvariant() ?? "en-us";
            if (lang.StartsWith("zh")) return "检测结果：不匹配";
            if (lang.StartsWith("ja")) return "照合結果：不一致";
            if (lang.StartsWith("ko")) return "결과: 일치하지 않음";
            if (lang.StartsWith("ru")) return "РЕЗУЛЬТАТ: НЕ СОВПАДАЕТ";
            if (lang.StartsWith("de")) return "ERGEBNIS: KEINE ÜBEREINSTIMMUNG";
            if (lang.StartsWith("fr")) return "RÉSULTAT : NON CORRESPONDANCE";
            if (lang.StartsWith("es")) return "RESULTADO: NO COINCIDE";
            if (lang.StartsWith("tr")) return "SONUÇ: EŞLEŞMİYOR";
            if (lang.StartsWith("nl")) return "RESULTAAT: GEEN MATCH";
            return "MATCH: MISMATCH";
        }

        private static string GetLocalizedHelpButton()
        {
            string lang = Settings.ActiveLocale?.ToLowerInvariant() ?? "en-us";
            if (lang.StartsWith("zh")) return "帮助";
            if (lang.StartsWith("ja")) return "ヘルプ";
            if (lang.StartsWith("ko")) return "도움말";
            if (lang.StartsWith("ru")) return "Помощь";
            if (lang.StartsWith("de")) return "Hilfe";
            if (lang.StartsWith("fr")) return "Aide";
            if (lang.StartsWith("es")) return "Ayuda";
            if (lang.StartsWith("tr")) return "Yardım";
            if (lang.StartsWith("nl")) return "Help";
            return "HELP";
        }

        private void LoadLines()
        {
            // ---------- 系统日志 ----------
            var sys = new List<string>();
            if (config.SystemLogFiles != null && config.SystemLogFiles.Count > 0)
            {
                string root = ExtensionLoader.ActiveExtensionInfo.FolderPath;
                for (int i = 0; i < config.SystemLogFiles.Count; i++)
                {
                    string path = Path.Combine(root, config.SystemLogFiles[i]);
                    if (File.Exists(path))
                    {
                        var lines = File.ReadAllText(path).Split(
                            new[] { "\r\n", "\n" }, StringSplitOptions.None);
                        sys.AddRange(lines);
                        if (i < config.SystemLogFiles.Count - 1)
                            sys.Add(null);      // 文件间停顿
                    }
                }
            }
            if (sys.Count > 0)
            {
                // 最后一个文件输出完毕后也停顿 SystemLogPauseBetween 秒
                sys.Add(null);   // null 行会在 UpdateSystemLog 中触发等待
                systemLogLines = sys.ToArray();
            }
            else
            {
                systemLogLines = sys.ToArray();   // 空数组，直接跳到引导文本（与之前一致）
                currentPhase = Phase.GuideText;
            }

            // ---------- 引导文本 ----------
            guideLines = config.GuideText != null
                ? new List<string>(config.GuideText)
                : new List<string>();
        }

        public override void LoadContent() => prevKs = Keyboard.GetState();

        public override void Update(float t)
        {
            stateTimer += t;
            switch (currentPhase)
            {
                case Phase.SystemLog: UpdateSystemLog(t); break;
                case Phase.GuideText: UpdateGuideText(t); break;
                case Phase.Interaction: UpdateInteraction(t); break;
            }
        }

        private void UpdateSystemLog(float t)
        {
            if (systemLogLineIndex >= systemLogLines.Length)
            {
                currentPhase = Phase.GuideText;
                stateTimer = 0f;
                return;
            }

            string line = systemLogLines[systemLogLineIndex];
            if (line == null)   // 文件间停顿
            {
                systemLogLineTimer += t;
                if (systemLogLineTimer >= config.SystemLogPauseBetween)
                {
                    systemLogLineTimer = 0f;
                    systemLogLineIndex++;
                }
                return;
            }

            systemLogLineTimer += t;
            if (systemLogLineTimer >= SYS_LOG_INTERVAL)
            {
                systemLogLineTimer = 0f;
                outputLines.Add((line, true));
                systemLogLineIndex++;
            }
        }

        private void UpdateGuideText(float t)
        {
            // 停顿中
            if (guidePauseTimer > 0f)
            {
                guidePauseTimer -= t;
                return;
            }

            // 在 UpdateGuideText 方法中，已读跳过之前插入这段
            if (guideLineIndex == 0 && guideCharCount == 0 &&
                !string.IsNullOrEmpty(config.ActionOnGuideTextStart) &&
                !os.Flags.HasFlag("Kernel_VMGuideActionDone_" + config.ConfigName))
            {
                os.Flags.AddFlag("Kernel_VMGuideActionDone_" + config.ConfigName);
                string actionPath = config.ActionOnGuideTextStart;  // 例如 "Actions/ActionOnGuideTextStart.xml"
                string extensionRoot = ExtensionLoader.ActiveExtensionInfo?.FolderPath;

                if (KernelExtensions.Debug)
                    Console.WriteLine("[FakeRecoveryModule] Executing guide start action via ActionHelper...");

                ActionHelper.ExecuteActionFile(os, actionPath, extensionRoot);
            }

            // 已读跳过：一次性输出所有引导文本（去除指令）
            if (config.EnableGuideReadFlag && os.Flags.HasFlag("Kernel_VMGuideRead_" + config.ConfigName))
            {
                foreach (string raw2 in guideLines)
                {
                    string clean = StripAllMarkers(raw2);
                    if (!string.IsNullOrEmpty(clean))
                        outputLines.Add(("> " + clean, false));
                }
                currentPhase = Phase.Interaction;
                return;
            }

            // 所有引导文本已输出
            if (guideLineIndex >= guideLines.Count)
            {
                if (config.EnableGuideReadFlag && !os.Flags.HasFlag("Kernel_VMGuideRead_" + config.ConfigName))
                {
                    os.Flags.AddFlag("Kernel_VMGuideRead_" + config.ConfigName);
                    os.threadedSaveExecute(true);
                }
                currentPhase = Phase.Interaction;
                return;
            }

            // ---- 新行开始：解析令牌并确定本行最终显示文本 ----
            if (guideCharCount == 0 && string.IsNullOrEmpty(currentPartialText))
            {
                string rawLine = guideLines[guideLineIndex];
                currentFullDisplayText = StripAllMarkers(rawLine); // 整行纯文本
                currentPartialText = "";
            }

            var tokens = ParseLineTokens(guideLines[guideLineIndex]);

            // 逐令牌处理
            if (guideCharCount < tokens.Count)
            {
                var token = tokens[guideCharCount];

                switch (token.Type)
                {
                    case TokenType.Pause:
                        guidePauseTimer = token.Value;
                        guideCharCount++; // 消耗此令牌
                        return;

                    case TokenType.Speed:
                        if (token.IsResetSpeed)
                            currentDynamicSpeed = GUIDE_CHAR_DELAY;
                        else
                            currentDynamicSpeed = token.Value;
                        guideCharCount++; // 消耗此令牌
                        return;

                    case TokenType.Char:
                        // 按当前动态速度逐字输出
                        guideCharTimer += t;
                        while (guideCharTimer >= currentDynamicSpeed)
                        {
                            guideCharTimer -= currentDynamicSpeed;
                            if (guideCharCount < tokens.Count)
                            {
                                currentPartialText += tokens[guideCharCount].Char;
                                guideCharCount++;
                            }
                            else break;
                        }
                        break;
                }
            }

            // 当前行所有令牌处理完毕，加入已完成列表
            if (guideCharCount >= tokens.Count)
            {
                if (!string.IsNullOrEmpty(currentFullDisplayText))
                    outputLines.Add(("> " + currentFullDisplayText, false));

                guideLineIndex++;
                guideCharCount = 0;
                guideCharTimer = 0f;
                currentDynamicSpeed = GUIDE_CHAR_DELAY;
                currentPartialText = "";
                currentFullDisplayText = "";
            }
        }

        private void UpdateInteraction(float t)
        {
            if (!interactionActivated)
            {
                interactionActivated = true;
                Game1.getSingleton().IsMouseVisible = true;
            }

            // 密码正确倒计时
            if (passwordSuccess)
            {
                successTimer -= t;
                if (successTimer <= 0f)
                {
                    if (!string.IsNullOrEmpty(config.SuccessMusic))
                    {
                        string extRoot = ExtensionLoader.ActiveExtensionInfo?.FolderPath?.Replace('\\', '/');
                        string resolved = MusicPathResolver.ResolveMusicPath(config.SuccessMusic, extRoot);
                        MusicManager.loadAsCurrentSong(resolved);
                    }
                    VMInfectionManager.Recover(os);
                    os.rebootThisComputer();
                }
                return;
            }

            // 自制键盘监听，支持所有按键
            if (config.Mode == RecoveryMode.Password)
            {
                KeyboardState ks = Keyboard.GetState();
                foreach (Keys key in ks.GetPressedKeys())
                {
                    if (prevKs.IsKeyUp(key))
                    {
                        if (key == Keys.Back && passwordInput.Length > 0)
                            passwordInput = passwordInput.Substring(0, passwordInput.Length - 1);
                        else if (key == Keys.Enter)
                        {
                            // 回车提交
                            if (passwordInput == config.Password)
                            {
                                passwordSuccess = true;
                                successTimer = SUCCESS_DELAY;
                            }
                            else
                            {
                                passwordInput = "";
                                showPasswordError = true;
                            }
                        }
                        else
                        {
                            // 可打印字符处理
                            string keyStr = TextBox.ConvertKeyToChar(key, ks.IsKeyDown(Keys.LeftShift) || ks.IsKeyDown(Keys.RightShift));
                            if (!string.IsNullOrEmpty(keyStr))
                                passwordInput += keyStr;
                        }
                    }
                }
                prevKs = ks;
            }
        }

        public override void Draw(float t)
        {
            spriteBatch.Draw(Utils.white, bounds, os.darkBackgroundColor);

            Color textColor = Color.Lerp(Utils.AddativeWhite, Utils.makeColor(204, 255, 249, 0), 0.5f);
            float monoCharWidth = LocaleActivator.ActiveLocaleIsCJK() ? 13f : 10f;
            SpriteFont font = GuiData.smallfont;
            float lineHeight = GuiData.ActiveFontConfig.tinyFontCharHeight + 6f;

            // 1. 固定光标位置（永远在屏幕底部的原始位置）
            float cursorY = bounds.Y + bounds.Height - (GuiData.ActiveFontConfig.tinyFontCharHeight + 16f);
            Rectangle cursorRect = new(bounds.X + 10, (int)cursorY, 10, 14);
            spriteBatch.Draw(Utils.white, cursorRect,
                (os.timer % 0.3f < 0.15f) ? Color.White : (Color.White * 0.05f));

            // 2. 构建要绘制的文本行列表（已完成行 + 当前正在打字的部分行）
            var drawLines = new List<(string text, bool monospace)>(outputLines);
            if (currentPhase == Phase.GuideText && guideLineIndex < guideLines.Count)
            {
                // 直接使用 Update 中拼好的部分文本
                string displayText = string.IsNullOrEmpty(currentPartialText) ? "" : currentPartialText;
                drawLines.Add(("> " + displayText, false));
            }

            // 3. 确定文本区域底部 Y 坐标（避免与按钮/光标重叠）
            float textBottomY;
            if (currentPhase == Phase.Interaction)
            {
                // 交互阶段：按钮放在光标上方，文本再放在按钮上方
                float btnTopY = cursorY - 40f;               // 按钮顶部 Y（离光标有一定间隔）
                textBottomY = btnTopY - 10f;                 // 文本底部离按钮 10 像素
            }
            else
            {
                // 非交互阶段：文本底部直接放在光标上方，留 5 像素间距
                textBottomY = cursorY - 5f;
            }

            float totalTextHeight = drawLines.Count * lineHeight;
            float textStartY = textBottomY - totalTextHeight; // 文本第一行顶部 Y

            // 4. 从上到下绘制文本
            float currentY = textStartY;
            for (int i = 0; i < drawLines.Count; i++)
            {
                var (text, mono) = drawLines[i];
                Vector2 pos = new(bounds.X + 10, currentY);
                if (mono)
                    DrawMonospace(text, font, pos, textColor, monoCharWidth);
                else
                    spriteBatch.DrawString(font, text, pos, textColor);
                currentY += lineHeight;
            }

            // 5. 绘制交互按钮（固定在文本下方、光标上方）
            if (currentPhase == Phase.Interaction)
            {
                float btnTopY = cursorY - 40f;               // 与上面保持一致
                // 密码模式交互
                if (config.Mode == RecoveryMode.Password)
                {
                    if (passwordSuccess)
                    {
                        Vector2 msgPos = new(bounds.X + 20, cursorY - 40f);
                        spriteBatch.DrawString(GuiData.smallfont, GetLocalizedPasswordMatch(), msgPos, Color.White);
                        return;
                    }

                    int textBoxX = bounds.X + 20;
                    int textBoxY = (int)(cursorY - 40f);

                    // 简单背景框
                    Rectangle inputRect = new(textBoxX, textBoxY, 200, 20);
                    spriteBatch.Draw(Utils.white, inputRect, Color.Gray);

                    // 密码内容（带光标闪烁）
                    string display = passwordInput;
                    if (stateTimer % 0.5f < 0.25f) display += "_";
                    spriteBatch.DrawString(GuiData.smallfont, display, new Vector2(inputRect.X + 2, inputRect.Y + 2), Color.White);

                    // 提交按钮
                    Rectangle submitBtn = new(inputRect.X + 210, inputRect.Y, 100, 20);
                    if (Button.doButton(1002, submitBtn.X, submitBtn.Y, submitBtn.Width, submitBtn.Height, config.ButtonText, Color.White))
                    {
                        if (passwordInput == config.Password)
                        {
                            passwordSuccess = true;
                            successTimer = SUCCESS_DELAY;
                        }
                        else
                        {
                            passwordInput = "";
                            showPasswordError = true;
                        }
                    }

                    // 帮助按钮（如果启用）
                    if (config.EnableHelpButton)
                    {
                        Rectangle helpBtn = new(submitBtn.X, submitBtn.Y + 25, submitBtn.Width, submitBtn.Height);
                        if (Button.doButton(1003, helpBtn.X, helpBtn.Y, helpBtn.Width, helpBtn.Height, GetLocalizedHelpButton(), Color.White))
                        {
                            if (!string.IsNullOrEmpty(config.HelpFile))
                            {
                                string helpSrc = Path.Combine(ExtensionLoader.ActiveExtensionInfo.FolderPath, config.HelpFile);
                                string fileName = Path.GetFileName(config.HelpFile);
                                string dest = Path.Combine(HostileHackerBreakinSequence.GetBaseDirectory(), fileName);
                                if (File.Exists(helpSrc))
                                {
                                    File.Copy(helpSrc, dest, true);
                                    if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                                        System.Diagnostics.Process.Start("notepad.exe", dest);
                                }
                            }
                            HostileHackerBreakinSequence.OpenTerminal();
                        }
                    }

                    // 错误提示
                    if (showPasswordError)
                    {
                        Vector2 errorPos = new(inputRect.X, inputRect.Y + 25);
                        spriteBatch.DrawString(GuiData.smallfont, GetLocalizedPasswordMismatch(), errorPos, Color.Red);
                    }
                }
                else
                {
                    Rectangle btn = new(bounds.X + 20, (int)btnTopY, 220, 30);
                    if (Button.doButton(1002, btn.X, btn.Y, btn.Width, btn.Height, config.ButtonText, Color.White))
                    {
                        if (!string.IsNullOrEmpty(config.HelpFile))
                        {
                            string helpSrc = Path.Combine(ExtensionLoader.ActiveExtensionInfo.FolderPath, config.HelpFile);
                            string fileName = Path.GetFileName(config.HelpFile);
                            string dest = Path.Combine(HostileHackerBreakinSequence.GetBaseDirectory(), fileName);
                            if (File.Exists(helpSrc))
                            {
                                File.Copy(helpSrc, dest, true);
                                if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                                    System.Diagnostics.Process.Start("notepad.exe", dest);
                            }
                        }
                        HostileHackerBreakinSequence.OpenTerminal();
                        HostileHackerBreakinSequence.CrashProgram();
                    }
                }
            }
        }

        private void DrawMonospace(string text, SpriteFont font, Vector2 pos, Color c, float charWidth)
        {
            for (int i = 0; i < text.Length; i++)
            {
                spriteBatch.DrawString(font, text[i].ToString(), Utils.ClipVec2ForTextRendering(pos), c);
                pos.X += charWidth;
            }
        }
    }
}