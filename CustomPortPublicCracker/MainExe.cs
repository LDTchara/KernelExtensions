using Hacknet;
using Hacknet.Gui;
using HarmonyLib;
using KernelExtensions.CustomPortPublicCracker;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Pathfinder.Executable;
using Pathfinder.Port;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace KernelExtensions.CustomPortCreaker;

/// <summary>
/// CrackerToolKit —— 端口破解工具箱（简化 GUI 版）。
///
/// 状态与行为：
///   Intro    ：欢迎页，绘制 Welcome 标题（无参数启动时进入；常驻，直到手动退出或被新实例互斥关闭）
///   Listing  ：列出可破解端口的 name + useable（终端 os.write 输出 + GUI 滚动列表展示），短暂后自动退出
///   Confings ：查看指定 UPS 配置（终端 os.write 输出 + GUI 滚动列表展示），短暂后自动退出
///   Cracking ：破解进行中 —— 左上 CurrentUPS.Title，底部按时间推进的进度条，完成后开端口并退出
///
/// 多实例互斥（os.exes 中已存在另一个本程序实例时）：
///   · 旧实例处于非 Cracking 状态 → 关闭旧实例，新实例正常运行
///   · 旧实例处于 Cracking 状态 → 仅当新实例也进入 Cracking 才允许并存；否则新实例自动退出
///
/// 命令入口：
///   CrackerToolKit          → Intro（欢迎页）
///   CrackerToolKit l        → Listing（输出 + 滚动列表，自动退出）
///   CrackerToolKit c &lt;n&gt;    → Confings（输出 + 滚动列表，自动退出）
///   CrackerToolKit &lt;port&gt;   → 直接对该端口开始破解（Cracking）
/// </summary>
public class PublicCracker : GameExecutable
{
    /// <summary>Listing / Confings 在 GUI 上展示多久后自动退出（秒）</summary>
    private const float EXIT_DELAY = 2.5f;

    public Dictionary<string, UPSConfig> UPSConfigs = Configs.ConfigLoader.UPSConfigs;
    public List<string> UPSPorts = new();
    public Computer c;
    public UPSConfig CurrentUPS;
    public Status CurrentStatus { get; set; } = Status.Intro;

    public enum Status
    {
        Intro,
        Listing,
        Confings,
        Cracking
    }

    // ---------- 破解运行时状态 ----------
    private float lifetime = 0f;             // 破解倒计时（初始 = Time + 1，减到 0 完成）
    private List<string> POSH = new();       // debuff: ProxyOverloadSpeedHelper 参数（破解期间持续）
    private List<string> AOP = new();        // buff:   AutoOverloadProxy 参数（破解期间持续）
    private string crackProtocol = null;     // 正在破解的协议（完成 openPort / 结算用）

    // ---------- GUI 状态 ----------
    private string confingProtocol = null;   // Confings 屏正在查看的协议
    private float selfExitTimer = 0f;        // Listing/Confings 自动退出倒计时
    private int listingScroll = 0;           // Listing 滚动偏移（doFancyList 静态全局，需保存/恢复）
    private int confingScroll = 0;           // Confings 滚动偏移

    // 界面几何（对齐原版 ExeModule/PortHackExe：内容从 PANEL_HEIGHT 之下开始）
    private int InnerX => bounds.X + 2;
    private int InnerW => bounds.Width - 4;
    private int ContentTop => bounds.Y + PANEL_HEIGHT + 4;
    private int BottomButtonY => bounds.Y + bounds.Height - 34;

    // Button ID 偏移（同 exe 内唯一）
    private const int ID_INTRO_EXIT = 301;
    private const int ID_LIST = 400;
    private const int ID_LIST_EXIT = 402;
    private const int ID_CONFINGS = 500;
    private const int ID_CRACK_CANCEL = 600;

    public PublicCracker() : base()
    {
        this.name = "CrackerToolKit";
        this.ramCost = 200;
    }

    // ======================= 初始化：解析入口 -> 互斥 -> 进入状态 =======================

    public override void OnInitialize()
    {
        base.OnInitialize();
        c = OS.currentInstance.connectedComp ?? OS.currentInstance.thisComputer;
        UPSPorts = new List<string>(UPSConfigs.Keys);
        CurrentUPS = null;

        // 1) 解析本次目标动作
        bool willCrack = false;            // 本次是否进入破解（互斥判定用）
        string confingProto = null;        // c <index> 解析出的协议
        bool doListing = false;            // l 子命令
        List<string> commandErrors = null; // 参数错误/破解失败提示（写终端后退出）

        if (Args.Length >= 2)
        {
            switch (Args[1].ToLower())
            {
                case "l":
                    doListing = true;
                    break;

                case "c":
                    if (Args.Length >= 3 && int.TryParse(Args[2], out int idx)
                        && idx >= 1 && idx <= UPSPorts.Count)
                    {
                        confingProto = UPSPorts[idx - 1];
                    }
                    else
                    {
                        commandErrors = new List<string> { "Args ERROR!!!", "Use c <index> to show the configs" };
                    }
                    break;

                default:
                    if (Args.Length == 2 && int.TryParse(Args[1], out int port)
                        && c.GetAllPortStates().Any(s => s.PortNumber == port))
                    {
                        if (StartCrack(port, out string err))
                        {
                            willCrack = true;
                        }
                        else
                        {
                            commandErrors = new List<string> { err };
                        }
                    }
                    else
                    {
                        commandErrors = new List<string>
                        {
                            "Args ERROR!!!",
                            "Use l to list useableports",
                            "Use c <index> to show the configs",
                            "Use <portnum> to crack"
                        };
                    }
                    break;
            }
        }

        // 2) 多实例互斥
        ApplyInstanceMutex(willCrack);
        if (isExiting) return; // 互斥判定要求本次退出（此时尚未输出任何内容）

        // 3) 执行进入动作
        if (commandErrors != null)
        {
            foreach (string e in commandErrors) os.write(e);
            isExiting = true;
            return;
        }
        if (willCrack)
        {
            CurrentStatus = Status.Cracking;
            return;
        }
        if (confingProto != null)
        {
            OpenConfings(confingProto);
            return;
        }
        if (doListing)
        {
            OpenListing();
            return;
        }

        // 无参数：进入欢迎页
        CurrentStatus = Status.Intro;
    }

    /// <summary>
    /// 多实例互斥：遍历 os.exes 中已存在的本程序实例。
    /// 旧实例非 Cracking → 关闭旧实例让新的继续；旧实例 Cracking → 仅当本次也进入 Cracking 才放行，否则退出本次。
    /// </summary>
    private void ApplyInstanceMutex(bool willEnterCracking)
    {
        try
        {
            // os.exes 的元素类型（ExeModule）不可直接引用，反射遍历（同 CustomTrialExe 的做法）
            var exes = AccessTools.Field(typeof(OS), "exes")?.GetValue(os) as IList;
            if (exes == null) return;

            foreach (object e in exes)
            {
                if (e is not PublicCracker other || ReferenceEquals(other, this)) continue;

                if (other.CurrentStatus == Status.Cracking)
                {
                    // 旧的正在破解：仅允许两个 Cracking 并存
                    if (!willEnterCracking)
                    {
                        os.write("CrackerToolKit is already cracking a port.");
                        isExiting = true;
                    }
                }
                else
                {
                    // 旧实例处于非破解状态：关闭它，让新实例正常运行
                    other.isExiting = true;
                }
                return; // 只处理遇到的第一个旧实例
            }
        }
        catch { /* 反射失败时不做互斥，保证不崩 */ }
    }

    // ======================= 状态切换 =======================

    /// <summary>Listing：终端输出 + GUI 滚动列表，短暂展示后自动退出</summary>
    private void OpenListing()
    {
        BuildPortListData(out string[] items, out _);
        CurrentUPS = null;
        confingProtocol = null;

        // 终端输出（退出后可见），GUI 同步展示
        os.write("================================");
        if (items.Length == 0)
            os.write("    -- No Crackable Ports --    ");
        else
            foreach (string s in items) os.write(s);
        os.write("================================");

        listingScroll = 0;
        selfExitTimer = EXIT_DELAY;
        CurrentStatus = Status.Listing;
    }

    /// <summary>Confings：终端输出 + GUI 滚动列表，短暂展示后自动退出</summary>
    private void OpenConfings(string protocol)
    {
        if (protocol == null || !UPSConfigs.TryGetValue(protocol, out UPSConfig cfg))
        {
            os.write($"No UPS config found for protocol '{protocol}'.");
            isExiting = true;
            return;
        }
        CurrentUPS = cfg;
        confingProtocol = protocol;

        // 终端输出（退出后可见），GUI 同步展示
        os.write("================================");
        foreach (string s in BuildConfingLines(protocol)) os.write(s);
        os.write("================================");

        confingScroll = 0;
        selfExitTimer = EXIT_DELAY;
        CurrentStatus = Status.Confings;
    }

    /// <summary>该协议当前是否可用（配置允许 且 未被破解过）</summary>
    private bool IsProtocolUseable(string protocol)
    {
        if (protocol == null || !UPSConfigs.TryGetValue(protocol, out UPSConfig cfg)) return false;
        return cfg.Useable && !os.Flags.HasFlag(protocol);
    }

    /// <summary>把协议列表格式化为可读的端口名；未注册/无效协议保留原文并标注，便于排查配置拼写错误。</summary>
    private static string FormatProtocolNames(List<string> protocols)
    {
        if (protocols == null || protocols.Count == 0) return string.Empty;
        List<string> names = new();
        foreach (string p in protocols)
        {
            try
            {
                if (PortManager.IsPortRegistered(p))
                    names.Add(PortManager.GetPortRecordFromProtocol(p).DefaultDisplayName);
                else
                    names.Add($"{p} <unknown>");
            }
            catch { names.Add(p); }
        }
        return string.Join(", ", names);
    }

    // ======================= 破解入口（校验 + 收集持续效果） =======================

    /// <summary>
    /// 对指定显示端口开始破解：前置校验通过后初始化倒计时并收集“破解期间持续生效”的代理辅助参数。
    /// 一次性 debuff/buff 效果不在此执行，统一在破解完成后结算（SettleEffects）。
    /// </summary>
    private bool StartCrack(int displayPort, out string error)
    {
        error = string.Empty;

        // 端口必须存在于目标机：以 PortState 表为准（Pathfinder 下 Computer.ports 不再承载端口）
        var state = c.GetAllPortStates().FirstOrDefault(s => s.PortNumber == displayPort);
        if (state == null)
        {
            error = "This Port is Not on this computer.";
            return false;
        }

        string protocol = state.Record.Protocol;
        if (!UPSConfigs.TryGetValue(protocol, out UPSConfig cfg))
        {
            error = $"No UPS config for port {displayPort}.";
            return false;
        }
        CurrentUPS = cfg;

        // 可用性校验
        if (cfg.Useable == false || os.Flags.HasFlag(protocol))
        {
            error = "This Port is Not Useable now.";
            return false;
        }

        // 前置开放端口校验
        int count = 0;
        for (int i = 0; i < cfg.RequireOpenPortsBefore.Count; i++)
        {
            if (c.isPortOpen(cfg.RequireOpenPortsBefore[i])) count++;
        }
        if (count != cfg.RequireOpenPortsBefore.Count)
        {
            error = $"Needs Open {FormatProtocolNames(cfg.RequireOpenPortsBefore)} before";
            return false;
        }

        // 前置代理校验：仅当目标机装有代理且代理仍在运行（proxyActive=true，即未被过载）时才要求先过载
        if (cfg.RequireProxyOverload && c.hasProxy && c.proxyActive)
        {
            error = "Needs Overload Proxy First";
            return false;
        }

        // 前置防火墙校验：仅当目标机装有防火墙且尚未解时要求先解
        if (cfg.RequireSolveFirewall && c.firewall != null && !c.firewall.solved)
        {
            error = "Needs Solve Firewall First";
            return false;
        }

        // 进入破解：初始化计时
        lifetime = cfg.Time + 1f;
        ramCost = cfg.RamCost;
        crackProtocol = protocol;
        POSH = new List<string>();
        AOP = new List<string>();

        // 仅收集破解期间逐帧持续的代理辅助参数；一次性效果留待破解完成后结算
        CollectProxyHelpers(cfg.Debuffs);
        CollectProxyHelpers(cfg.Buffs);

        return true;
    }

    /// <summary>
    /// 破解成功后统一结算 UPS 的 debuff / buff 一次性效果（先 debuffs 后 buffs）。
    /// ProxyOverloadSpeedHelper / AutoOverloadProxy 是破解期间逐帧持续的代理辅助，不在此结算。
    /// </summary>
    private void SettleEffects(UPSConfig cfg)
    {
        if (cfg == null) return;

        // ---- debuffs：破解完成后对目标机施加的负面效果 ----
        foreach (var k in cfg.Debuffs)
        {
            string key = k.Key;
            List<string> kvp = k.Value;
            if (key == "closeports" && kvp.Count > 0 && kvp[0] != "")
                Methods.ClosePortsFromProtocol(kvp, os, c.idName);
            if (key == "addfirewall" && kvp.Count > 0 && kvp[0] != "")
                Methods.addFirewall(os, c.idName, Methods.GetValue<int>(kvp, "lenth"), Methods.GetValue<float>(kvp, "additiontime"), Methods.GetValue<string>(kvp, "solution"));
            if (key == "addproxy" && kvp.Count > 0 && kvp[0] != "")
                Methods.addProxy(os, c.idName, int.Parse(kvp[0]));
            if (key == "ReRandomFirewall" && kvp.Count > 0 && kvp[0] != "")
            {
                int lenth = Methods.GetValue<int>(kvp, "lenth");
                float additiontime = Methods.GetValue<float>(kvp, "additiontime");
                bool reset = string.IsNullOrEmpty(Methods.GetValue(kvp, "reset")) ? true : Methods.GetValue<bool>(kvp, "reset");
                Methods.ReRandomFirewallSolution(os, c.idName, lenth, additiontime, reset);
            }
            if (key == "ResetFirewall" && kvp.Count > 0 && kvp[0] != "")
                Methods.ResetFirewall(os, c.idName);
            if (key == "ResetProxy" && kvp.Count > 0 && kvp[0] != "")
                Methods.ResetProxy(os, c.idName);
            if (key == "SetFirewallConfig" && kvp.Count > 0 && kvp[0] != "")
            {
                bool reset = string.IsNullOrEmpty(Methods.GetValue(kvp, "reset")) ? true : Methods.GetValue<bool>(kvp, "reset");
                Methods.SetFirewallConfig(os, c.idName, Methods.GetValue<int>(kvp, "lenth"), Methods.GetValue<float>(kvp, "additiontime"), Methods.GetValue<string>(kvp, "solution"), reset);
            }
        }

        // ---- buffs：破解完成后获得/结算的正面效果 ----
        foreach (var k in cfg.Buffs)
        {
            string key = k.Key;
            List<string> kvp = k.Value;
            if (key == "openports" && kvp.Count > 0 && kvp[0] != "")
                Methods.OpenPortsFromProtocol(kvp, os, c.idName);
            if (key == "solvefirewall" && kvp.Count > 0 && kvp[0] != "")
                Methods.solveFirewall(os, c.idName, float.Parse(kvp[0]));
            if (key == "solveproxy" && kvp.Count > 0 && kvp[0] != "")
                Methods.solveProxy(os, c.idName, float.Parse(kvp[0]));
            if (key == "SetFirewallConfig" && kvp.Count > 0 && kvp[0] != "")
            {
                bool reset = string.IsNullOrEmpty(Methods.GetValue(kvp, "reset")) ? true : Methods.GetValue<bool>(kvp, "reset");
                Methods.SetFirewallConfig(os, c.idName, Methods.GetValue<int>(kvp, "lenth"), Methods.GetValue<float>(kvp, "additiontime"), Methods.GetValue<string>(kvp, "solution"), reset);
            }
        }
    }

    /// <summary>扫描 debuff/buff 字典，收集破解期间逐帧持续的代理辅助参数（ProxyOverloadSpeedHelper / AutoOverloadProxy）。</summary>
    private void CollectProxyHelpers(Dictionary<string, List<string>> effects)
    {
        if (effects == null) return;
        foreach (var k in effects)
        {
            string key = k.Key;
            List<string> kvp = k.Value;
            if (key == "ProxyOverloadSpeedHelper" && kvp != null && kvp.Count > 0 && kvp[0] != "")
                POSH = kvp;
            if (key == "AutoOverloadProxy" && kvp != null && kvp.Count > 0 && kvp[0] != "")
                AOP = kvp;
        }
    }

    // ======================= Update =======================

    public override void Update(float t)
    {
        base.Update(t);
        if (isExiting) return;

        // Listing / Confings：GUI 展示片刻后自动退出（终端输出内容保留可回看）
        if (CurrentStatus == Status.Listing || CurrentStatus == Status.Confings)
        {
            selfExitTimer -= t;
            if (selfExitTimer <= 0f) isExiting = true;
            return;
        }

        if (CurrentStatus == Status.Intro)
        {
            CurrentUPS = null;
            return;
        }
        if (CurrentStatus != Status.Cracking) return;

        // ---- Cracking：倒计时 + 持续辅助 ----
        lifetime -= t;
        if (POSH != null && POSH.Count != 0)
            Methods.ProxyOverloadTicksHelper(os, c.idName, int.Parse(POSH[0].Replace("x", "")), t);
        if (AOP != null && AOP.Count != 0)
            Methods.AutoOverloadProxy(os, c.idName, float.Parse(AOP[0]), t);

        if (lifetime <= 0f)
        {
            // 破解完成：打开目标端口并退出
            // 注意：POSH/AOP 保存的是对 UPS 配置列表的引用，置 null 而非 Clear，避免清空配置
            POSH = null;
            AOP = null;
            lifetime = 0f;
            try
            {
                // 直接按协议打开目标端口（PortState 表语义；null 防御）
                string proto = crackProtocol ?? CurrentUPS.Protocol;
                if (c.GetPortState(proto) != null)
                    c.openPort(proto, os.thisComputer.ip);
            }
            catch { /* 端口不在目标机等极端情况：静默 */ }

            // 端口已打开，统一结算该 UPS 的 debuff / buff 一次性效果（先 debuffs 后 buffs）
            SettleEffects(CurrentUPS);

            CurrentStatus = Status.Intro;
            isExiting = true;
        }
    }

    // ======================= Draw =======================

    public override void Draw(float t)
    {
        base.Draw(t);
        drawOutline();
        drawTarget();
        if (isExiting) return;

        switch (CurrentStatus)
        {
            case Status.Intro:
                DrawIntro();
                break;
            case Status.Listing:
                DrawListing();
                break;
            case Status.Confings:
                DrawConfings();
                break;
            case Status.Cracking:
                DrawCracking();
                break;
        }
    }

    // ---- Intro：Welcome 标题 ----

    private void DrawIntro()
    {
        int cx = bounds.X + bounds.Width / 2;
        int top = ContentTop;
        Color accent = os.highlightColor;

        // 大标题 Welcome
        string title = "Welcome";
        Vector2 titleSize = GuiData.font.MeasureString(title);
        const float scale = 1.8f;
        spriteBatch.DrawString(GuiData.font, title,
            new Vector2(cx - titleSize.X * scale / 2f, top + 8),
            accent, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

        // 副标题
        string sub = "CrackerToolKit - Public Port Cracking Suite";
        Vector2 subSize = GuiData.font.MeasureString(sub);
        spriteBatch.DrawString(GuiData.font, sub,
            new Vector2(cx - subSize.X / 2f, top + 62),
            Color.White * 0.85f, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);

        // 操作提示
        string[] hints =
        {
            "l          list crackable ports",
            "c <n>      view port config",
            "<port>     start cracking directly"
        };
        for (int i = 0; i < hints.Length; i++)
        {
            Vector2 hs = GuiData.smallfont.MeasureString(hints[i]);
            spriteBatch.DrawString(GuiData.smallfont, hints[i],
                new Vector2(cx - hs.X / 2f, top + 88 + i * 18),
                Color.White * 0.6f, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);
        }

        // Exit
        if (Button.doButton(PID + ID_INTRO_EXIT, cx - 45, BottomButtonY, 90, 26, "Exit", os.lockedColor))
            isExiting = true;
    }

    // ---- Listing：端口列表（name + useable），滚动条 + 终端输出后自动退出 ----

    private void BuildPortListData(out string[] items, out int[] portIndexMap)
    {
        List<string> ss = new();
        List<int> map = new();
        for (int i = 0; i < UPSPorts.Count; i++)
        {
            string p = UPSPorts[i];
            if (!PortManager.IsPortRegistered(p)) continue;
            if (!UPSConfigs.TryGetValue(p, out UPSConfig cfg)) continue;
            string displayName = PortManager.GetPortRecordFromProtocol(p).DefaultDisplayName;
            bool useable = cfg.Useable && !os.Flags.HasFlag(p);
            ss.Add($"{i + 1}:{p}:{displayName}:{(useable ? "Useable" : "Locked")}");
            map.Add(i);
        }
        items = ss.ToArray();
        portIndexMap = map.ToArray();
    }

    private void DrawListing()
    {
        Color theme = Color.Lerp(os.topBarColor, Utils.AddativeWhite, 0.2f);
        int x = InnerX;
        int w = InnerW;

        // 标题
        TextItem.doFontLabel(new Vector2(x, ContentTop), "Useable Cracker Ports", GuiData.smallfont, os.highlightColor);

        BuildPortListData(out string[] items, out _);

        // 列表区（标题下到窗口底）
        int listY = ContentTop + 24;
        int listH = bounds.Y + bounds.Height - 6 - listY;

        if (items.Length == 0)
        {
            spriteBatch.Draw(Utils.white, new Rectangle(x, listY, w, listH), Utils.VeryDarkGray);
            TextItem.doFontLabelToSize(new Rectangle(x, listY, w, listH), "    -- No Crackable Ports --    ",
                GuiData.smallfont, Utils.AddativeWhite, true, true);
        }
        else
        {
            SelectableTextList.scrollOffset = listingScroll;
            SelectableTextList.doFancyList(PID + ID_LIST,
                x, listY, w, listH,
                items, -1, theme, HasDraggableScrollbar: true);
            listingScroll = SelectableTextList.scrollOffset;
        }

        // 自动退出提示 + 提前退出按钮
        TextItem.doFontLabel(new Vector2(x, bounds.Y + bounds.Height - 24), $"Closing in {(int)Math.Ceiling(selfExitTimer)}s",
            GuiData.detailfont, Color.White * 0.5f);
        if (Button.doButton(PID + ID_LIST_EXIT, x + w - 60, bounds.Y + bounds.Height - 28, 50, 22, "X", os.lockedColor))
            isExiting = true;
    }

    // ---- Confings：UPS 配置（滚动条 + 终端输出后自动退出） ----

    private string[] BuildConfingLines(string protocol)
    {
        UPSConfig cfg = CurrentUPS ?? UPSConfigs[protocol];
        string displayName = protocol;
        try
        {
            if (PortManager.IsPortRegistered(protocol))
                displayName = PortManager.GetPortRecordFromProtocol(protocol).DefaultDisplayName;
        }
        catch { }

        string sss = FormatProtocolNames(cfg.RequireOpenPortsBefore);
        bool useable = IsProtocolUseable(protocol);

        List<string> lines = new()
        {
            $"Protocol               : {protocol}",
            $"Display Name           : {displayName}",
            $"TimeCost               : {cfg.Time}s",
            $"RamCost                : {cfg.RamCost}MB",
            $"Title                  : {cfg.Title}",
        };

        // Debuff(s) / Buff(s)：每个效果单独成行，避免全部挤在一行被缩小字号；无效果时显示 None
        AppendEffectLines(lines, cfg.Debuffs, "Debuff(s)", false);
        AppendEffectLines(lines, cfg.Buffs, "Buff(s)", true);

        lines.Add($"NeedsPreOpenPort(s)    : {sss}");
        lines.Add($"NeedsPreSolveFirewall  : {cfg.RequireSolveFirewall}");
        lines.Add($"NeedsPreSolveProxy     : {cfg.RequireProxyOverload}");
        lines.Add($"Useable                : {(useable ? "True" : "False")}");
        return lines.ToArray();
    }

    /// <summary>把一类 debuff/buff 效果按“每效果一行”追加到 lines（含缩进），无效果则显示 None。</summary>
    private static void AppendEffectLines(List<string> lines, Dictionary<string, List<string>> effects, string label, bool isBuff)
    {
        var effectLines = UPSConfig.OutputAnybuffConfingsLines(effects, isBuff);
        if (effectLines == null || effectLines.Count == 0)
        {
            lines.Add($"{label,-22}: None");
            return;
        }
        lines.Add($"{label,-22}:");
        foreach (string el in effectLines)
            lines.Add("   - " + el);
    }

    private void DrawConfings()
    {
        int x = InnerX;
        int w = InnerW;
        string protocol = confingProtocol;
        if (protocol == null || !UPSConfigs.ContainsKey(protocol))
        {
            // 防御：异常情况下退回 Listing
            OpenListing();
            return;
        }

        UPSConfig cfg = UPSConfigs[protocol];
        string displayName = protocol;
        try
        {
            if (PortManager.IsPortRegistered(protocol))
                displayName = PortManager.GetPortRecordFromProtocol(protocol).DefaultDisplayName;
        }
        catch { }

        // 标题
        TextItem.doFontLabel(new Vector2(x, ContentTop),
            $"Configs - {displayName} [{protocol}]", GuiData.smallfont, os.highlightColor);

        // 配置文本行（可滚动）
        string[] lines = BuildConfingLines(protocol);
        int listY = ContentTop + 24;
        int listH = bounds.Y + bounds.Height - 6 - listY;

        SelectableTextList.scrollOffset = confingScroll;
        SelectableTextList.doFancyList(PID + ID_CONFINGS,
            x, listY, w, listH,
            lines, -1, Color.Lerp(os.topBarColor, Utils.AddativeWhite, 0.2f), HasDraggableScrollbar: true);
        confingScroll = SelectableTextList.scrollOffset;

        // 自动退出提示 + 提前退出按钮
        TextItem.doFontLabel(new Vector2(x, bounds.Y + bounds.Height - 24), $"Closing in {(int)Math.Ceiling(selfExitTimer)}s",
            GuiData.detailfont, Color.White * 0.5f);
        if (Button.doButton(PID + ID_LIST_EXIT + 1, x + w - 60, bounds.Y + bounds.Height - 28, 50, 22, "X", os.lockedColor))
            isExiting = true;
    }

    // ---- Cracking：左上 CurrentUPS.Title + 底部按时间推进的进度条 ----

    private void DrawCracking()
    {
        if (CurrentUPS == null) return;
        int x = InnerX;
        int w = InnerW;

        // 左上角标题（Title 为空时回退协议名）
        string title = string.IsNullOrEmpty(CurrentUPS.Title)
            ? $"Cracking {crackProtocol ?? confingProtocol ?? "port"}..."
            : CurrentUPS.Title;
        TextItem.doFontLabel(new Vector2(x, ContentTop), title, GuiData.font, os.highlightColor);

        // 中部状态说明
        string status = "UPLOADING CRACK ...";
        Vector2 ss = GuiData.font.MeasureString(status);
        spriteBatch.DrawString(GuiData.font, status,
            new Vector2(bounds.X + bounds.Width / 2f - ss.X / 2f, bounds.Y + bounds.Height / 2f - 30f),
            Color.White * 0.7f, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);

        // 底部进度条：黑色衬底 + 随进度增长的亮条（窗口外框由基类 drawOutline()/drawTarget() 负责）
        float progress = CurrentUPS.Time <= 0f
            ? 1f
            : MathHelper.Clamp(1f - (lifetime - 1f) / CurrentUPS.Time, 0f, 1f);

        Rectangle bar = new Rectangle(bounds.X + 2, bounds.Y + bounds.Height - 18, w, 16);
        // 黑色衬底
        RenderedRectangle.doRectangle(bar.X, bar.Y, bar.Width, bar.Height, Color.Black * 0.75f);
        // 进度亮条
        RenderedRectangle.doRectangle(bar.X, bar.Y, (int)(bar.Width * progress), bar.Height, os.highlightColor);

        // 进度文字（右侧：剩余秒数；中间：百分比）
        int remaining = Math.Max(0, (int)(lifetime - 1f));
        string pct = $"{(int)(progress * 100f)}%";
        Vector2 pctSize = GuiData.smallfont.MeasureString(pct);
        Vector2 pctPos = new Vector2(bounds.X + bounds.Width / 2f - pctSize.X / 2f, bar.Y + (bar.Height - pctSize.Y) / 2f);
        spriteBatch.DrawString(GuiData.smallfont, pct, pctPos, Color.White, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);

        string timeText = remaining + "s";
        Vector2 ttSize = GuiData.smallfont.MeasureString(timeText);
        spriteBatch.DrawString(GuiData.smallfont, timeText,
            new Vector2(bar.X + bar.Width - ttSize.X - 4, bar.Y + (bar.Height - ttSize.Y) / 2f),
            Color.White * 0.85f, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);

        // 右上角取消按钮（进度条上方）
        if (Button.doButton(PID + ID_CRACK_CANCEL, bounds.X + bounds.Width - 72, bounds.Y + bounds.Height - 46, 60, 24, "Cancel", os.lockedColor))
            isExiting = true;
    }
}
