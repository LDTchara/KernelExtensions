using BepInEx.Hacknet;
using Hacknet;
using Hacknet.Daemons.Helpers;
using Hacknet.Gui;
using KernelExtensions.AirCraft.Actions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Pathfinder.Meta.Load;
using Pathfinder.Util;

namespace KernelExtensions.AirCraft.Daemon
{
    public class FlightDaemon : Pathfinder.Daemon.BaseDaemon
    {
        public FlightDaemon(Computer computer, string serviceName, OS opSystem) : base(computer, serviceName, opSystem) { }
        public override string Identifier => this.comp.name+" SYSTEM";

        // ====== 全局字典（静态，公开只读） ======
        public static Dictionary<string, Computer> FlightIdToComputer = new();


        // ====== 私有内部状态（不序列化） ======
        private const float FlightHoursPerLengthUnit = 0.06855416f;
        private const float ReloadFirmwareTime = 6f;
        internal const string CriticalFilename = "747FlightOps.dll";
        private const float RoughTotalFallTimeSeconds = 135f;
        private const float StartingAltitude = 38000f;
        


        public double CurrentAltitude = 37900.0;
        private float currentAirspeed = 460f;
        private float rateOfClimb = 0.073f;
        private Color ThemeColor = Color.CornflowerBlue;
        private Folder MainFolder;
        private bool PilotAlerted = false;
        private bool IsReloadingFirmware = false;
        private float firmwareReloadProgress = 0f;
        private float timeFallingFor = 0f;
        private float timeSinceLastDataUpdate = 0f;
        public float H = 135f;
        private bool IsSubscribedForUpdates = false;
        public bool IsInCriticalFirmwareFailure = false;
        public bool AircraftFallStartsImmediatley = true;
        public Action CrashAction;
        private Texture2D WorldMap = OS.currentInstance.content.Load<Texture2D>("DLC/Sprites/SmallWorldMap");
        private Texture2D Circle = OS.currentInstance.content.Load<Texture2D>("Circle");
        private Texture2D StatusOKIcon = OS.currentInstance.content.Load<Texture2D>("CircleOutlineLarge");
        private Texture2D CautionIcon = OS.currentInstance.content.Load<Texture2D>("Sprites/Icons/CautionIcon");
        private Texture2D Plane = OS.currentInstance.content.Load<Texture2D>("DLC/Sprites/Airplane");
        private Texture2D CircleOutline = OS.currentInstance.content.Load<Texture2D>("CircleOutlineLarge");

        private Vector2 mapOrigin = new Vector2(0.4304f, 0.8339f);
        private Vector2 mapDestV = new Vector2(0.6672f, 0.4264f);

        private float FlightProgress=3f;
        

        // ★新增：拯救标志，防止重复触发 OnSaved
        private bool hasBeenRescued = false;

        public static Dictionary<Computer, Daemon.FlightDaemon> CompToDamons = new Dictionary<Computer, Daemon.FlightDaemon>();


        // ====== XML 序列化的公开属性（必须保留） ======




        [XMLStorage]
        public string OnFailed;

        [XMLStorage]
        public string OnSaved;












        // ====== 文件系统初始化 ======
        public override void initFiles()
        {
            base.initFiles();

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
            // 注册全局字典
            if (!FlightIdToComputer.ContainsKey(comp.idName))
                FlightIdToComputer[comp.idName] = comp;
            if (!CompToDamons.ContainsKey(comp))
                CompToDamons[comp] = this;

        }

        public override void navigatedTo()
        {
            base.navigatedTo();
            StartUpdating();
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

        // ====== 核心更新逻辑 ======
        private void Update(float t)
        {
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
                    if (AircraftFallStartsImmediatley)
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
        private void CrashAircraft()
        {
            // ★ 如果当前全局覆盖层显示的是本飞机的数据，立即关闭
            if (GlobalAircraftOverlayManager.CurrentFlightDaemon == this)
            {
                GlobalAircraftOverlayManager.IsOverlayActive = false;
                GlobalAircraftOverlayManager.CurrentFlightDaemon = null;
            }

            if (os.connectedComp == comp)
            {
                os.execute("disconnect");
                os.display.command = "dc";
            }
            CrashAction?.Invoke();
            os.netMap.visibleNodes.Remove(os.netMap.nodes.IndexOf(comp));

            if (!string.IsNullOrEmpty(comp.idName) && FlightIdToComputer.ContainsKey(comp.idName))
                FlightIdToComputer.Remove(comp.idName);

            if (!string.IsNullOrEmpty(OnFailed))
            {
                RunnableConditionalActions.LoadIntoOS(OnFailed, OS.currentInstance);
            }

            UnsubscribeFromUpdates();
        }


        // ====== 固件重载 ======
        public void StartReloadFirmware()
        {
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
                if (!string.IsNullOrEmpty(OnSaved))
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
            Rectangle bounds2 = new Rectangle(bounds.X, bounds.Y,
                (int)((double)bounds.Width * 0.666), (int)((double)bounds.Height * 0.666));
            DrawHeadings(bounds2, sb);
            // ★ 修改点：仅当全局覆盖层未激活或指向其他 daemon 时才绘制高度计
            if (!(GlobalAircraftOverlayManager.IsOverlayActive &&
                  GlobalAircraftOverlayManager.CurrentFlightDaemon == this))
            {
                AircraftAltitudeIndicator.RenderAltitudeIndicator(dest, sb,
                    (int)(CurrentAltitude + 0.5),
                    IsInCriticalDescent(),
                    AircraftAltitudeIndicator.GetFlashRateFromTimer(OS.currentInstance.timer));
            }
        }

        private void DrawHeadings(Rectangle bounds, SpriteBatch sb)
        {
            Rectangle rectangle = new Rectangle(bounds.X, bounds.Y + 4, bounds.Width, 40);
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
            string text = (IsReloadingFirmware ? LocaleTerms.Loc("RELOADING FIRMWARE") :
                (IsInCriticalFirmwareFailure ? LocaleTerms.Loc("CRITICAL FIRMWARE FAILURE") :
                (PilotAlerted ? LocaleTerms.Loc("PILOT ALERTED") : LocaleTerms.Loc("FLIGHT IN PROGRESS"))));
            TextItem.doCenteredFontLabel(dest2, text, GuiData.font, Color.White);
            Rectangle rectangle2 = new Rectangle(dest2.X, dest2.Y + dest2.Height + 8, dest2.Width, 24);
            int num = 4;
            int num2 = (rectangle2.Width - num * 3) / 3;
            if (Button.doButton(632877701, rectangle2.X, rectangle2.Y, num2 - 20, rectangle2.Height,
                LocaleTerms.Loc("Exit.."), os.lockedColor))
            {
                os.runCommand($"connect {comp.ip}");
            }
            if (Button.doButton(632877703, rectangle2.X + num + num2 - 20, rectangle2.Y, num2 + 10 + num,
                rectangle2.Height, LocaleTerms.Loc("Pilot Alert"), ThemeColor))
            {
                PilotAlerted = true;
            }

            if (Button.doButton(632877706, rectangle2.X + num * 3 + num2 * 2 - 10, rectangle2.Y, num2 + 10 + num,
                rectangle2.Height, LocaleTerms.Loc("Reload Firmware"), os.lockedColor))
            {
                StartReloadFirmware();
            }

            Rectangle dest3 = new Rectangle(rectangle2.X + 6, rectangle2.Y + rectangle2.Height + 20,
                rectangle2.Width - 75, 70);
            byte status = (byte)((!(currentAirspeed <= 500f)) ? ((currentAirspeed < 600f) ? 1u : 2u) : 0u);
            DrawFieldDisplay(dest3, sb, LocaleTerms.Loc("Air Speed (kn)"), currentAirspeed.ToString("0.0"), status);
            dest3.Y += dest3.Height + 6;
            byte status2 = (byte)((!(rateOfClimb > -0.2f)) ? ((rateOfClimb > -1f) ? 1u : 2u) : 0u);
            DrawFieldDisplay(dest3, sb, LocaleTerms.Loc("Rate of Climb (f/s)"), rateOfClimb.ToString("0.000"), status2);
            dest3.Y += dest3.Height + 6;
            DrawFieldDisplay(dest3, sb, LocaleTerms.Loc("Heading (deg)"), string.Concat(67.228f), 0);
            dest3.Y += dest3.Height + 6;
        }

        private void DrawFieldDisplay(Rectangle dest, SpriteBatch sb, string title, string value, byte status)
        {
            Rectangle rectangle = new Rectangle(dest.X, dest.Y, dest.Height, dest.Height);
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
            Rectangle dest2 = new Rectangle(rectangle.X + rectangle.Width + 6, dest.Y,
                dest.Width - rectangle.Width, dest.Height / 3 - 1);
            TextItem.doFontLabelToSize(dest2, title, GuiData.font, color, doNotOversize: true, offsetToTopLeft: true);
            Rectangle destinationRectangle2 = new Rectangle(dest2.X - 8, dest2.Y + dest2.Height,
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
            Vector2 vector = new Vector2((float)rectangle.X + (float)rectangle.Width * mapOrigin.X,
                (float)rectangle.Y + (float)rectangle.Height * mapOrigin.Y);
            Vector2 vector2 = new Vector2((float)rectangle.X + (float)rectangle.Width * mapDestV.X,
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
            Vector2 scale = new Vector2(num2 / (float)Plane.Width);
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
