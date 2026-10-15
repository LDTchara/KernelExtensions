using Hacknet;
using Hacknet.Daemons.Helpers;
using Hacknet.Gui;
using KernelExtensions.Modules;
using KernelExtensions.Utilities;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Pathfinder.Util;

namespace KernelExtensions.Daemons
{
    /// <summary>
    /// 飞机 Daemon —— 原版 AircraftDaemon 的 KE 通用化版本。
    ///
    /// ── 节点 XML 用法（全部参数可选）──────────────────────────────────────────
    /// &lt;Computer&gt;
    ///     &lt;FlightDaemon OnFailed="Actions/Failed" OnSaved="Actions/Saved" CrashIPPrefix="YourPrefix"/&gt;
    /// &lt;/Computer&gt;
    /// ── 参数说明（均可选）────────────────────────────────────────────────────
    ///   OnFailed      — 坠机后加载的 Action 文件（相对扩展根目录）。默认不执行。
    ///   OnSaved       — 固件重载期间成功恢复 747FlightOps.dll 后加载的 Action 文件。
    ///                    默认不执行。
    ///   FallDuration  — 坠落总时长（秒），同时作为 AttackAircraft CrashDelay=-1
    ///                    时的默认坠落时长。默认 135。
    ///   CrashIPPrefix — 坠机后加在节点 IP 前的前缀，使旧 IP 不可达（对齐原版
    ///                    "DCLOC:" 行为）。空字符串 = 不修改 IP（节点仍可访问，
    ///                    重访显示为静态残骸，不复活不重复触发 OnFailed）。
    ///                    默认 "DCLOC:"。
    ///
    /// ── 行为特性 ─────────────────────────────────────────────────────────────
    ///   · 攻击入口: AttackAircraft Action（NodeID + CrashDelay: -1 默认 / 0 立即 /
    ///     N 秒延迟）
    ///   · 固件重载链: 删 747FlightOps.dll → StartReloadFirmware → 6s 后检测缺失 →
    ///     IsInCriticalFirmwareFailure → 坠落 → CrashAircraft
    ///   · 按需更新（对齐原版）: 巡航时物理仅在 daemon 界面绘制（draw 补 tick）或
    ///     覆盖层激活（ShowAircraftOverlay）时推进；离开界面即冻结。固件重载/坠机
    ///     过程持续更新（StartReloadFirmware 自动订阅），不依赖观看
    ///   · 坠毁终结: 坠机后 IsCrashed，物理彻底停止，重访节点不复活、不重复 OnFailed
    ///   · 覆盖层: GlobalAircraftOverlayManager 全局高度计（固定位置绘制，与界面无关）；
    ///     坠毁后持续显示残骸，仅由 HideAircraftOverlay Action 关闭（不自动关闭，
    ///     对齐原版 AircraftInfoOverlay 行为）
    ///   · Pathfinder 兼容: 坠机改 IP 后手动刷新 ComputerLookup 缓存（原版由
    ///     NodeLookup.OnAircraftDaemonChangeIP patch 自动处理，KE 自实现类需手动
    ///     RebuildLookups，否则 getComputer/connect 缓存 miss 连不上）
    ///
    /// ── 内部机制备忘 ─────────────────────────────────────────────────────────
    ///   注册表: CompToDaemons（Computer → FlightDaemon），loadInit 时注册
    ///   生命周期: initFiles（新游戏建 FlightSystems + 固件文件）/ loadInit（读档重建）
    ///   更新订阅: StartUpdating / UnsubscribeFromUpdates / UnsubscribeIfIdle（安全退订）
    /// </summary>
    public class FlightDaemon : Pathfinder.Daemon.BaseDaemon
    {
        public FlightDaemon(Computer computer, string serviceName, OS opSystem) : base(computer, serviceName, opSystem) { }
        public override string Identifier => this.comp.name+" SYSTEM";

        // ====== 私有内部状态（不序列化） ======
        private const float FlightHoursPerLengthUnit = 0.06855416f;
        private const float ReloadFirmwareTime = 6f;
        internal const string CriticalFilename = "747FlightOps.dll";
        private const float RoughTotalFallTimeSeconds = 135f;
        private const float StartingAltitude = 38000f;
        
        public double CurrentAltitude = 37900.0;
        internal float currentAirspeed = 460f;
        internal float rateOfClimb = 0.073f;
        private Color ThemeColor = Color.CornflowerBlue;
        private Folder MainFolder;
        internal bool PilotAlerted = false;
        internal bool IsReloadingFirmware = false;
        internal float firmwareReloadProgress = 0f;
        internal float timeFallingFor = 0f;
        internal float timeSinceLastDataUpdate = 0f;
        /// <summary>
        /// 坠落总时长（秒）。<c>&lt;= 0</c> = **跳过下降过程**：坠落被触发时直接判定落地
        /// （由 <see cref="ApplyFallDuration"/> 从 <see cref="FallDuration"/> 写入）。
        /// </summary>
        public float H = 135f;
        internal bool IsSubscribedForUpdates = false;
        public bool IsInCriticalFirmwareFailure = false;
        internal bool IsCrashed = false; // 坠毁终结标志：坠机后物理彻底停止，防止重访节点时"复活"
        /// <summary>
        /// 【原版遗留字段，保留作备用接口】**从未被验证过**。
        ///
        /// <para>
        /// 字段名与拼写都**逐字来自原版** <c>AircraftDaemon</c>（原版第 65 行同样是
        /// <c>AircraftFallStartsImmediatley</c>，含这个拼写错误）—— 故意不改名、不改拼写，
        /// 以便与本项目参考的原版源码对照，也避免与"是否该开放给作者"这件事纠缠。
        /// </para>
        ///
        /// <para>
        /// 语义：机载固件进入致命故障后，坠落是**立刻以最大速率**开始（<c>true</c>，
        /// 高度按 <c>timeFallingFor / H</c> 线性算），还是先用 15 秒二次缓出加速到最大速率
        /// （<c>false</c>）。
        /// </para>
        ///
        /// <para>
        /// ⚠️ <b>它目前恒为 <c>true</c>，而且没有任何途径改成 <c>false</c>：</b>
        /// 本类**没有给它加 <c>[XMLStorage]</c>**（原版也没有任何序列化机制，原版里它同样
        /// 只在声明与使用两处出现、无人赋值 → 原版的 <c>false</c> 分支是**死代码**）。
        /// 也就是说 **<c>false</c>（15 秒缓出）这条路从来没有被跑过**，故不开放给作者：
        /// 开放等于把一条未经验证的路径变成对外承诺。
        /// </para>
        ///
        /// <para>
        /// 需要"更快/更慢掉下去"时改 <see cref="FallDuration"/>（即 <c>H</c>），
        /// 需要"跳过坠落直接判定坠毁"时用 AttackAircraft 动作的 <c>FallDuration="0"</c>。
        /// </para>
        /// </summary>
        public bool AircraftFallStartsImmediatley = true;
        public Action CrashAction;
        private Texture2D WorldMap = OS.currentInstance.content.Load<Texture2D>("DLC/Sprites/SmallWorldMap");
        private Texture2D Circle = OS.currentInstance.content.Load<Texture2D>("Circle");
        private Texture2D StatusOKIcon = OS.currentInstance.content.Load<Texture2D>("CircleOutlineLarge");
        private Texture2D CautionIcon = OS.currentInstance.content.Load<Texture2D>("Sprites/Icons/CautionIcon");
        private Texture2D Plane = OS.currentInstance.content.Load<Texture2D>("DLC/Sprites/Airplane");
        private Texture2D CircleOutline = OS.currentInstance.content.Load<Texture2D>("CircleOutlineLarge");

        /// <summary>运行时航线起点（归一化坐标）。由 ApplyMapPoints() 从 XML 应用；Debug Action 可临时改。</summary>
        internal Vector2 mapOrigin = new(0.4304f, 0.8339f);
        /// <summary>运行时航线终点（同上）。</summary>
        internal Vector2 mapDestV = new(0.6672f, 0.4264f);

        internal float FlightProgress=3f;
        
        // ★新增：拯救标志，防止重复触发 OnSaved
        internal bool hasBeenRescued = false;

        public static Dictionary<Computer, FlightDaemon> CompToDaemons = new();

        // ====== XML 序列化的公开属性（必须保留） ======

        [XMLStorage]
        public string OnFailed;

        [XMLStorage]
        public string OnSaved;

        [XMLStorage]
        public float FallDuration = 135f; // 默认坠落时长

        [XMLStorage]
        public string CrashIPPrefix = "DCLOC:"; // 坠机后加在 IP 前的前缀（空 = 不修改 IP，节点仍可访问）

        // ====== 航线图起终点======
        // 归一化坐标（0~1，相对世界地图矩形），默认值即原硬编码位置。
        // 换算与贴图尺寸无关（按矩形比例算），但换贴图后“起终点落在图哪里”就得重调，故开放可配。
        [XMLStorage] public float MapOriginX = 0.4304f;
        [XMLStorage] public float MapOriginY = 0.8339f;
        [XMLStorage] public float MapDestX = 0.6672f;
        [XMLStorage] public float MapDestY = 0.4264f;


        /// <summary>坠落时长不可用时的兜底值（= XML 字段的默认值，对齐原版 RoughTotalFallTimeSeconds）。</summary>
        private const float DefaultFallDuration = 135f;

        /// <summary>
        /// 把作者配置的 <c>FallDuration</c> 应用到运行时高度 <c>H</c>。
        ///
        /// <para>
        /// ⚠️ <c>H</c> 会**作为除数**（Update 里 <c>fallFraction = timeFallingFor / H</c>），
        /// 所以必须 &gt; 0 —— 0 会让 <c>fallFraction</c> 先变 NaN（0/0）再变 +Inf，
        /// <c>CurrentAltitude</c> 随之变 NaN / -Inf，而坠落终结判定是
        /// <c>CurrentAltitude &lt;= 0</c>（NaN 下**不成立**），于是**坠落流程永久卡死**。
        /// </para>
        ///
        /// <para>
        /// 两档处理（<b>0 被刻意放行</b>）：
        /// <list type="bullet">
        ///   <item>负数 / NaN / Infinity → 用默认，**不警告**（AGENTS.md 9.55「负数 = 默认；
        ///     NaN / Infinity 视同无效值」，作者是在明确表达"用默认"。
        ///     注意 <c>&lt;AttackAircraft FallDuration="-1"&gt;</c> 也被文档写作"沿用 daemon 的值"，
        ///     所以负数**不能**当作"立即坠毁"）。</item>
        ///   <item><b>0 → 原样放行</b>（这里不替换），由坠落分支解释为
        ///     「坠落被触发时跳过下降过程、直接坠毁」—— 见 <c>Update</c> 里对 <c>H &lt;= 0</c> 的处理。</item>
        ///   <item>正值 → 直接用</item>
        /// </list>
        /// </para>
        /// </summary>
        private void ApplyFallDuration()
        {
            float d = FallDuration;

            if (float.IsNaN(d) || float.IsInfinity(d) || d < 0f)
            {
                H = DefaultFallDuration;
                return;
            }

            // 0 原样保留 —— 它的含义在坠落分支里，不在这里
            H = d;
        }

        // ====== 文件系统初始化 ======
        public override void initFiles()
        {
            base.initFiles();

            ApplyFallDuration(); // 将 XML 配置的坠落时长应用到运行时变量 H（三档处理见方法注释）
            ApplyMapPoints();

            MainFolder = comp.files.root.searchForFolder("FlightSystems");
            if (MainFolder == null)
            {
                MainFolder = new Folder("FlightSystems");
                comp.files.root.folders.Add(MainFolder);
            }
            MainFolder.files.Add(new FileEntry(PortExploits.ValidAircraftOperatingDLL, "747FlightOps.dll"));
            MainFolder.files.Add(new FileEntry(Computer.generateBinaryString(200), "InFlightWifiRouter.dll"));
            MainFolder.files.Add(new FileEntry(Computer.generateBinaryString(200), "Scheduler.dll"));
            MainFolder.files.Add(new FileEntry(Computer.generateBinaryString(200), "EntertainmentServices.dll"));
            MainFolder.files.Add(new FileEntry(Computer.generateBinaryString(200), "AnnouncementsSys.dll"));

            // 注册到查找表（与 loadInit 对称）。
            //   此前只在 loadInit 注册，新游戏路径下 CompToDaemons 恒为空，
            //   要靠 AttackAircraft 顺手补全 —— 于是“离开 daemon 查状态”类工具在新游戏里什么都看不到。
            if (!CompToDaemons.ContainsKey(comp))
                CompToDaemons[comp] = this;
        }

        /// <summary>
        /// 把 XML 配置的航线起终点应用到运行时坐标，clamp 到 [0,1]。
        /// 新游戏走 initFiles、读档走 loadInit，两处都要调（对齐 H = FallDuration 的做法）。
        /// </summary>
        private void ApplyMapPoints()
        {
            mapOrigin = new Vector2(MathHelper.Clamp(MapOriginX, 0f, 1f), MathHelper.Clamp(MapOriginY, 0f, 1f));
            mapDestV = new Vector2(MathHelper.Clamp(MapDestX, 0f, 1f), MathHelper.Clamp(MapDestY, 0f, 1f));
        }

        public override void loadInit()
        {
            base.loadInit();

            OS os = OS.currentInstance;
            WorldMap = os.content.Load<Texture2D>("DLC/Sprites/SmallWorldMap");
            Circle = os.content.Load<Texture2D>("Circle");
            StatusOKIcon = os.content.Load<Texture2D>("CircleOutlineLarge");
            CircleOutline = os.content.Load<Texture2D>("CircleOutlineLarge");
            CautionIcon = os.content.Load<Texture2D>("Sprites/Icons/CautionIcon");
            Plane = os.content.Load<Texture2D>("DLC/Sprites/Airplane");

            FlightProgress = 3f;   // 使用 XML 属性 Progress

            ThemeColor = os.highlightColor;
            MainFolder = comp.files.root.searchForFolder("FlightSystems");
            ApplyFallDuration(); // 读档后同步 H（XMLStorage 反序列化在 loadInit 前完成）
            ApplyMapPoints(); // 同上：读档后同步航线起终点
            if (!CompToDaemons.ContainsKey(comp))
                CompToDaemons[comp] = this;

        }

        public override void navigatedTo()
        {
            base.navigatedTo();
            // 按需更新（对齐原版）：不在此订阅。物理仅在 daemon 界面绘制时（draw 补 tick）、
            // 固件重载中（StartReloadFirmware）或覆盖层激活时（ShowAircraftOverlay）推进
        }

        // ====== 更新订阅管理 ======
        public void StartUpdating()
        {
            if (!IsSubscribedForUpdates)
            {
                os.UpdateSubscriptions += Update;
                IsSubscribedForUpdates = true;
            }
        }

        private void UnsubscribeFromUpdates()
        {
            if (IsSubscribedForUpdates)
            {
                os.UpdateSubscriptions -= Update;
                IsSubscribedForUpdates = false;
            }
        }

        /// <summary>
        /// 安全退订：仅当不在固件重载/坠落中且未坠毁时退订（供覆盖层关闭等场景调用）。
        /// 坠机或重载进行中退订会冻结坠落过程，必须避免。
        /// </summary>
        public void UnsubscribeIfIdle()
        {
            if (IsCrashed || IsReloadingFirmware || IsInCriticalFirmwareFailure)
                return;
            UnsubscribeFromUpdates();
        }

        // ====== 核心更新逻辑 ======
        private void Update(float t)
        {
            if (IsCrashed)
                return; // 坠毁终结：物理不再推进（防重访节点时复活/重复 OnFailed）

            if (IsReloadingFirmware)
            {
                firmwareReloadProgress += t;
                if (firmwareReloadProgress > 6f)
                    FinishReloadingFirmware();
            }

            timeSinceLastDataUpdate += t;
            if (timeSinceLastDataUpdate > 0.1f)
            {
                timeSinceLastDataUpdate -= 0.1f;
                t = 0.1f;

                if (IsInCriticalFirmwareFailure)
                {
                    timeFallingFor += t;
                    float maxRate = -876.9231f;
                    if (H <= 0f)
                    {
                        // H = 0（即作者把 FallDuration 写成 0）⇒ 跳过下降过程，直接判定落地。
                        // 收尾由本段末尾已有的 `CurrentAltitude <= 0 → CrashAircraft()` 完成，
                        // 与 AttackAircraft 的 FallDuration="0" 是同一条路。
                        //
                        // ⚠️ 必须显式置 0，不能指望除法：timeFallingFor / 0 会先得 NaN（0/0）、
                        // 之后 +Inf，而 **NaN <= 0.0 是 false** ⇒ 那样永远不会坠毁，
                        // 飞机会悬在半空且高度显示错乱。
                        rateOfClimb = maxRate;
                        CurrentAltitude = 0.0;
                    }
                    else if (AircraftFallStartsImmediatley)
                    {
                        rateOfClimb = maxRate;
                        float fallFraction = timeFallingFor / H;
                        CurrentAltitude = 38000f * (1f - fallFraction);
                    }
                    else
                    {
                        float delay = 15f;
                        rateOfClimb = Math.Max(maxRate,
                            0f - Utils.QuadraticOutCurve(Math.Min(delay, timeFallingFor) / delay) * (-1f * maxRate));
                        CurrentAltitude += rateOfClimb * t;
                    }
                    if (Utils.FloatEquals(rateOfClimb, maxRate))
                        rateOfClimb += (1f - Utils.rand(2f)) * t;

                    float minAirspeed = -1600f;
                    if (currentAirspeed < minAirspeed)
                        currentAirspeed -= (1f - Utils.rand(2f)) * rateOfClimb * t;
                    else
                        currentAirspeed -= rateOfClimb * t;
                }
                else
                {
                    double targetAlt = 38000.0;
                    double targetSpeed = 500.0;
                    if ((double)currentAirspeed > targetSpeed * 1.25)
                        currentAirspeed -= t * rateOfClimb * 3f;
                    else
                        currentAirspeed += (float)((5 - Utils.random.Next(11)) * t *
                            ((currentAirspeed > targetSpeed) ? (-2.0) : 1.0));

                    if (CurrentAltitude > targetAlt + 500.0)
                        CurrentAltitude -= rateOfClimb * t + (5 - Utils.random.Next(11)) * t * 2.0 *
                            ((CurrentAltitude > targetAlt) ? (-1f) : 1f);
                    else
                        CurrentAltitude += rateOfClimb * t + (5 - Utils.random.Next(11)) * t * 2.0 *
                            ((CurrentAltitude > targetAlt) ? (-1f) : 1f);

                    if (rateOfClimb < -0.1f || CurrentAltitude < 37500.0)
                    {
                        if (rateOfClimb < -1f)
                            rateOfClimb += -1f * rateOfClimb / 2f * t;
                        else
                            rateOfClimb += 5f * t;
                    }
                    else if (rateOfClimb > 0.1f)
                        rateOfClimb -= 1.6666666f * t;
                    else
                        rateOfClimb += (Utils.rand(0.1f) - Utils.rand(0.06f)) * t;
                }

                if (CurrentAltitude <= 0.0)
                    CrashAircraft();
            }
        }

        // ★修改：CrashAircraft 中调用 OnFailed 条件动作
        internal void CrashAircraft()
        {
            IsCrashed = true; // 坠毁终结：物理停止，防重访节点时复活/重复 OnFailed

            if (os.connectedComp == comp)
            {
                os.execute("disconnect");
                os.display.command = "dc";
            }
            CrashAction?.Invoke();
            os.netMap.visibleNodes.Remove(os.netMap.nodes.IndexOf(comp));

            // 坠机后 IP 加前缀使其不可达（对齐原版 DCLOC: 行为；CrashIPPrefix 为空/NONE 则不修改）
            if (!ConfigValue.IsNone(CrashIPPrefix))
            {
                comp.ip = CrashIPPrefix + comp.ip;
                // Pathfinder 的 getComputer/connect 走 ComputerLookup 缓存（NodeLookup patch），
                // 改 IP 后必须刷新缓存——原版由 NodeLookup.OnAircraftDaemonChangeIP 自动处理，
                // 但该 patch 绑定原版 AircraftDaemon，KE 自实现类需手动调用
                Pathfinder.Util.ComputerLookup.RebuildLookups();
            }

            if (!ConfigValue.IsNone(OnFailed))
            {
                RunnableConditionalActions.LoadIntoOS(OnFailed, OS.currentInstance);
            }

            UnsubscribeFromUpdates();
        }


        // ====== 固件重载 ======
        public void StartReloadFirmware()
        {
            // 已在重载中则直接返回：此前无条件重置进度，导致连点「重载固件」会让进度从头再来
            if (IsReloadingFirmware) return;

            StartUpdating();
            IsReloadingFirmware = true;
            firmwareReloadProgress = 0f;
        }

        // ★修改：FinishReloadingFirmware 中检测拯救并调用 OnSaved
        private void FinishReloadingFirmware()
        {
            bool wasCritical = IsInCriticalFirmwareFailure;   // 记录之前状态
            IsReloadingFirmware = false;
            FileEntry fileEntry = MainFolder.searchForFile("747FlightOps.dll");
            if (fileEntry == null || fileEntry.data != PortExploits.ValidAircraftOperatingDLL)
            {
                IsInCriticalFirmwareFailure = true;
            }
            else
            {
                IsInCriticalFirmwareFailure = false;
            }

            // ★新增：如果从关键故障恢复到正常状态，且尚未拯救，则触发拯救动作
            if (wasCritical && !IsInCriticalFirmwareFailure && !hasBeenRescued)
            {
                hasBeenRescued = true;
                if (!ConfigValue.IsNone(OnSaved))
                {
                    RunnableConditionalActions.LoadIntoOS(OnSaved, OS.currentInstance);
                }
            }
        }

        public bool IsInCriticalDescent()
        {
            return rateOfClimb < -0.8f || CurrentAltitude < 800.0;
        }

        // ====== 界面绘制 ======

        public override void draw(Rectangle bounds, SpriteBatch sb)
        {
            base.draw(bounds, sb);
            if (!IsSubscribedForUpdates)
            {
                Update((float)os.lastGameTime.ElapsedGameTime.TotalSeconds);
            }
            Rectangle dest = Utils.InsetRectangle(bounds, 1);
            DrawMap(dest, sb);
            Rectangle bounds2 = new(bounds.X, bounds.Y,
                (int)((double)bounds.Width * 0.666), (int)((double)bounds.Height * 0.666));
            DrawHeadings(bounds2, sb);
            KEAltitudeIndicator.RenderAltitudeIndicator(dest, sb,
                (int)(CurrentAltitude + 0.5),
                IsInCriticalDescent(),
                AircraftAltitudeIndicator.GetFlashRateFromTimer(OS.currentInstance.timer));
        }

        private void DrawHeadings(Rectangle bounds, SpriteBatch sb)
        {
            Rectangle rectangle = new(bounds.X, bounds.Y + 4, bounds.Width, 40);
            Rectangle dest = rectangle;
            dest.X += 8;
            dest.Width -= 8;
            TextItem.doFontLabelToSize(dest, comp.name, GuiData.font, Color.White, doNotOversize: true, offsetToTopLeft: true);
            rectangle.Y += rectangle.Height - 1;
            rectangle.Height = 1;
            sb.Draw(Utils.white, rectangle, Color.White);
            Color themeColor = ThemeColor;
            rectangle.Y += 2;
            rectangle.Height = 20;
            Color patternColor = (IsInCriticalFirmwareFailure ? Color.DarkRed : (themeColor * 0.28f));
            if (!IsInCriticalFirmwareFailure && PilotAlerted)
            {
                patternColor = os.warningColor * 0.5f;
            }
            PatternDrawer.draw(rectangle, 1f, themeColor * 0.1f, patternColor, sb, PatternDrawer.warningStripe);
            if (IsReloadingFirmware)
            {
                Rectangle destinationRectangle = rectangle;
                destinationRectangle.Width = (int)((float)destinationRectangle.Width * Utils.QuadraticOutCurve(firmwareReloadProgress / 6f));
                sb.Draw(Utils.white, destinationRectangle, Utils.AddativeWhite * 0.4f);
            }
            Rectangle dest2 = Utils.InsetRectangle(rectangle, 1);
            string text = (IsReloadingFirmware ? KELoc.Loc("FLIGHT_RELOADING_FIRMWARE", "RELOADING FIRMWARE") :
                (IsInCriticalFirmwareFailure ? KELoc.Loc("FLIGHT_CRITICAL_FIRMWARE_FAILURE", "CRITICAL FIRMWARE FAILURE") :
                (PilotAlerted ? KELoc.Loc("FLIGHT_PILOT_ALERTED", "PILOT ALERTED") : KELoc.Loc("FLIGHT_IN_PROGRESS", "FLIGHT IN PROGRESS"))));
            TextItem.doCenteredFontLabel(dest2, text, GuiData.font, Color.White);
            Rectangle rectangle2 = new(dest2.X, dest2.Y + dest2.Height + 8, dest2.Width, 24);
            int num = 4;
            int num2 = (rectangle2.Width - num * 3) / 3;
            if (Button.doButton(632877701, rectangle2.X, rectangle2.Y, num2 - 20, rectangle2.Height,
            KELoc.Loc("FLIGHT_DISCONNECT", "Disconnect"), os.lockedColor))
            {
                os.runCommand("disconnect");
            }
            if (Button.doButton(632877703, rectangle2.X + num + num2 - 20, rectangle2.Y, num2 + 10 + num,
                rectangle2.Height, KELoc.Loc("FLIGHT_PILOT_ALERT", "Pilot Alert"), ThemeColor))
            {
                PilotAlerted = true;
                string flag = "PilotAlerted_" + comp.idName;
                if (!os.Flags.HasFlag(flag))
                {
                    os.Flags.AddFlag(flag);
                    os.threadedSaveExecute(true);
                }
            }

            if (Button.doButton(632877706, rectangle2.X + num * 3 + num2 * 2 - 10, rectangle2.Y, num2 + 10 + num,
                rectangle2.Height, KELoc.Loc("FLIGHT_RELOAD_FIRMWARE", "Reload Firmware"), os.lockedColor))
            {
                StartReloadFirmware();
            }

            Rectangle dest3 = new(rectangle2.X + 6, rectangle2.Y + rectangle2.Height + 20,
                rectangle2.Width - 75, 70);
            byte status = (byte)((!(currentAirspeed <= 500f)) ? ((currentAirspeed < 600f) ? 1u : 2u) : 0u);
            DrawFieldDisplay(dest3, sb, KELoc.Loc("FLIGHT_AIR_SPEED", "Air Speed (kn)"), currentAirspeed.ToString("0.0"), status);
            dest3.Y += dest3.Height + 6;
            byte status2 = (byte)((!(rateOfClimb > -0.2f)) ? ((rateOfClimb > -1f) ? 1u : 2u) : 0u);
            DrawFieldDisplay(dest3, sb, KELoc.Loc("FLIGHT_RATE_OF_CLIMB", "Rate of Climb (f/s)"), rateOfClimb.ToString("0.000"), status2);
            dest3.Y += dest3.Height + 6;
            DrawFieldDisplay(dest3, sb, KELoc.Loc("FLIGHT_HEADING", "Heading (deg)"), string.Concat(67.228f), 0);
            dest3.Y += dest3.Height + 6;
        }

        private void DrawFieldDisplay(Rectangle dest, SpriteBatch sb, string title, string value, byte status)
        {
            Rectangle rectangle = new(dest.X, dest.Y, dest.Height, dest.Height);
            Texture2D texture = ((status == 0) ? StatusOKIcon : CautionIcon);
            Color color = status switch
            {
                1 => Color.Orange,
                0 => ThemeColor,
                _ => Color.Red,
            };
            sb.Draw(CircleOutline, rectangle, color);
            if (status < 2 || !(os.timer % 0.4f < 0.2f))
            {
                bool flag = status != 0;
                Rectangle destinationRectangle = Utils.InsetRectangle(rectangle, rectangle.Width / 5);
                if (flag)
                {
                    destinationRectangle.Y -= 2;
                }
                sb.Draw(texture, destinationRectangle, color);
            }
            Rectangle dest2 = new(rectangle.X + rectangle.Width + 6, dest.Y,
                dest.Width - rectangle.Width, dest.Height / 3 - 1);
            TextItem.doFontLabelToSize(dest2, title, GuiData.font, color, doNotOversize: true, offsetToTopLeft: true);
            Rectangle destinationRectangle2 = new(dest2.X - 8, dest2.Y + dest2.Height,
                dest2.Width + 8, 1);
            dest2.Y += dest2.Height + 1;
            sb.Draw(Utils.white, destinationRectangle2, color);
            dest2.Height = (int)((float)dest.Height / 3f * 2f) - 1;
            TextItem.doFontLabelToSize(dest2, value, GuiData.font,
                (status == 0) ? (Color.White * 0.9f) : color, doNotOversize: true, offsetToTopLeft: true);
        }

        private void DrawMap(Rectangle dest, SpriteBatch sb)
        {
            Rectangle rectangle = Utils.DrawSpriteAspectCorrect(dest, sb, WorldMap, Color.Gray, ForceToBottom: true);
            float num = 10f;
            Vector2 vector = new((float)rectangle.X + (float)rectangle.Width * mapOrigin.X,
                (float)rectangle.Y + (float)rectangle.Height * mapOrigin.Y);
            Vector2 vector2 = new((float)rectangle.X + (float)rectangle.Width * mapDestV.X,
                (float)rectangle.Y + (float)rectangle.Height * mapDestV.Y);
            sb.Draw(Circle, vector, null, Color.Black, 0f, Circle.GetCentreOrigin(),
                new Vector2((num + 3f) / (float)Circle.Width), SpriteEffects.None, 0.4f);
            sb.Draw(Circle, vector, null, Utils.AddativeWhite, 0f, Circle.GetCentreOrigin(),
                new Vector2(num / (float)Circle.Width), SpriteEffects.None, 0.4f);
            sb.Draw(Circle, vector2, null, Color.Black, 0f, Circle.GetCentreOrigin(),
                new Vector2((num + 3f) / (float)Circle.Width), SpriteEffects.None, 0.4f);
            sb.Draw(Circle, vector2, null, ThemeColor, 0f, Circle.GetCentreOrigin(),
                new Vector2(num / (float)Circle.Width), SpriteEffects.None, 0.4f);
            Utils.drawLine(sb, vector, vector2, Vector2.Zero, ThemeColor * 0.5f, 0.3f);
            Vector2 vector3 = Vector2.Lerp(vector, vector2, FlightProgress / 6f);
            float num2 = 55f;
            Vector2 scale = new(num2 / (float)Plane.Width);
            Vector2 vector4 = vector2 - vector3;
            float rotation = (float)(Math.Atan2(vector4.Y, vector4.X) + Math.PI / 2.0);
            sb.Draw(Plane, vector3, null, Color.Black, rotation, Plane.GetCentreOrigin(),
                scale, SpriteEffects.None, 0.4f);
            num2 = 53f;
            sb.Draw(scale: new Vector2(num2 / (float)Plane.Width), texture: Plane, position: vector3,
                sourceRectangle: null, color: IsInCriticalFirmwareFailure ? Color.Red : ThemeColor,
                rotation: rotation, origin: Plane.GetCentreOrigin(), effects: SpriteEffects.None,
                layerDepth: 0.4f);
        }

        // ====== 生命周期 ======
        // (已通过 navigatedTo 和 UnsubscribeFromUpdates 管理更新)
    }
}
