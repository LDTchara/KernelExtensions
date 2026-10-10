using Hacknet;
using Hacknet.Effects;
using Hacknet.Gui;
using HarmonyLib;
using KernelExtensions.Configs;
using KernelExtensions.Utilities;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Pathfinder.Executable;

namespace KernelExtensions.Executables
{
    /// <summary>
    /// 效果预览器（<c>#EFFECTS#</c>）：把项目里所有可选背景/动画效果放进一个可翻页的窗口里看。
    ///
    /// <para>
    /// 它存在的意义是**在真实 exe 环境里预览**：<c>Hacknet.Effects</c> 里的效果有的需要
    /// RenderTarget、有的需要 ContentManager、有的要 Update+Draw 配对，光看源码判断不了观感，
    /// 也不知道不同窗口尺寸下会变成什么样。这个程序用 ←/→ 逐个翻。
    /// </para>
    ///
    /// <para>
    /// 三类条目：
    /// <list type="bullet">
    ///   <item><b>2D</b>：直接画在当前 spriteBatch 上（原版大部分背景效果）</item>
    ///   <item><b>3D</b>：需要离屏 RenderTarget + 专用 SpriteBatch（<c>OS.Draw</c> 已经 Begin 过了，
    ///     中途 End 不了，所以必须离屏再贴回来）</item>
    ///   <item><b>自绘</b>：项目自己实现的几何/流场类效果</item>
    /// </list>
    /// </para>
    /// </summary>
    public class EffectsPlayerExe : GameExecutable
    {
        /// <summary>
        /// 一个可预览的效果条目。
        /// 2D 与 3D 分成两个委托，因为 3D 需要 <see cref="GraphicsDevice"/> 与离屏批次，
        /// 而 2D 只要当前批次 —— 用一个委托塞不下，会逼所有 2D 效果也走 RT（白白多一次全屏拷贝）。
        /// </summary>
        private sealed class EffectEntry
        {
            public string Name;
            /// <summary>一句话说明（显示在左下角，帮助回忆这效果是干嘛的）。</summary>
            public string Note = "";
            public float Duration = 8f;
            /// <summary>2D 绘制：画在外部传入的批次与矩形上。</summary>
            public Action<SpriteBatch, Rectangle, float> Draw2D;
            /// <summary>3D 绘制：在<b>已绑定好的</b>离屏目标与专用批次上画（坐标从 (0,0) 起）。</summary>
            public Action<GraphicsDevice, SpriteBatch, Rectangle, float> Draw3D;
            /// <summary>每帧推进（2D/3D 都可能需要）。</summary>
            public Action<float> Update;
        }

        private readonly List<EffectEntry> _effects = new();
        private int _currentIndex;
        private float _timer;
        private bool _paused;
        private string _statusText = "";

        // ---- 效果实例 ----
        private HexGridBackground _hexGrid;
        private ShiftingGridEffect _shiftingGrid;
        private MovingBarsEffect _movingBars;
        private RaindropsEffect _raindrops;
        private DepthDotGridEffect _depthDot;
        private Managers.CustomColorManager.DynColorConfig _rainbowConfig;

        // ---- 3D：离屏渲染资源（懒建）----
        private RenderTarget2D _rt;
        private SpriteBatch _rtBatch;
        private PortHackCubeSequence _cubeSeq;
        private readonly PortHackV2Sequence _polySeq = new();

        // ---- 新建的自绘效果 ----
        private FlowFieldEffect _flowField;
        /// <summary>上一帧内容区高度。Update 在 Draw 之前跑，所以流场折算用的是上一帧的尺寸。</summary>
        private int _lastContentH;

        public EffectsPlayerExe() : base()
        {
            ramCost = 300;
            IdentifierName = "EffectsPlayer";
            name = "EffectsPlayer";
        }

        public override void OnInitialize()
        {
            base.OnInitialize();
            InitEffects();
            LoadEffects();
        }

        private void InitEffects()
        {
            _hexGrid = new HexGridBackground(os.content);
            _shiftingGrid = new ShiftingGridEffect();
            _movingBars = new MovingBarsEffect();
            _raindrops = new RaindropsEffect();
            _raindrops.Init(os.content);
            _depthDot = new DepthDotGridEffect(os.content);
            _rainbowConfig = Managers.CustomColorManager.ParseColorString("LDTchara");
            _cubeSeq = new PortHackCubeSequence();
            _flowField = new FlowFieldEffect();
            // 保持原先"高度 55 → 218px/s、555 → 450px/s"的曲线，改用系数式表达
            _flowField.SpeedCoefficient = 450f / (float)Math.Pow(555f, _flowField.SpeedExponent);
        }

        private void LoadEffects()
        {
            // ============ 原版 2D 背景效果 ============

            _effects.Add(new EffectEntry
            {
                Name = "ZoomingDotGrid",
                Note = "starfield tunnel",
                Duration = 8f,
                Draw2D = (sb, b, t) => ZoomingDotGridEffect.Render(b, sb, t * 0.3f, GetRainbowColor())
            });

            _effects.Add(new EffectEntry
            {
                Name = "GridEffect",
                Note = "cross grid",
                Duration = 5f,
                Draw2D = (sb, b, t) => GridEffect.DrawGridBackground(b, sb, 12, GetRainbowColor() * 0.3f)
            });

            _effects.Add(new EffectEntry
            {
                Name = "HexGrid",
                Note = "honeycomb (vanilla KaguyaTrial)",
                Duration = 8f,
                Update = dt => _hexGrid.Update(dt),
                Draw2D = (sb, b, t) => _hexGrid.Draw(b, sb, Color.Black, GetRainbowColor() * 0.2f,
                    HexGridBackground.ColoringAlgorithm.CorrectedSinWash, 0f)
            });

            _effects.Add(new EffectEntry
            {
                Name = "ShiftingGrid",
                Note = "travelling grid",
                Duration = 8f,
                Update = dt => _shiftingGrid.Update(dt),
                Draw2D = (sb, b, t) => _shiftingGrid.RenderGrid(b, sb,
                    GetRainbowColor() * 0.3f, GetRainbowColor() * 0.15f, Color.White * 0.05f, false)
            });

            _effects.Add(new EffectEntry
            {
                Name = "MovingBars",
                Note = "audio bars",
                Duration = 8f,
                Update = dt => _movingBars.Update(dt),
                Draw2D = (sb, b, t) => _movingBars.Draw(sb, b, 5f, 6f, 4f, GetRainbowColor() * 0.3f)
            });

            _effects.Add(new EffectEntry
            {
                Name = "Raindrops",
                Note = "matrix rain",
                Duration = 10f,
                Update = dt => _raindrops.Update(dt, 8f),
                Draw2D = (sb, b, t) => _raindrops.Render(b, sb, GetRainbowColor() * 0.5f, 15f, 20f)
            });

            _effects.Add(new EffectEntry
            {
                Name = "DepthDotGrid",
                Note = "depth dots (vanilla SignalScramble)",
                Duration = 8f,
                Draw2D = (sb, b, t) => _depthDot.DrawGrid(b, Vector2.Zero, sb,
                    30f, 10, GetRainbowColor() * 0.2f, 60f, 10f, 0f, t * 0.2f, 1f - MathHelper.Clamp(t / 6f, 0f, 1f))
            });

            // ============ 项目自绘 ============

            _effects.Add(new EffectEntry
            {
                Name = "FlowField",
                Note = "data torrent (load-driven)",
                Duration = 0f,   // 0 = 不限时（它是无缝循环的）
                Update = dt => _flowField.Update(dt, LoadRamp(), _lastContentH),
                Draw2D = (sb, b, t) => _flowField.Draw(sb, b, GetRainbowColor(), LoadRamp())
            });

            // ============ 5 个柏拉图立体（3D，走离屏）============

            foreach (KPlatonicShape shape in new[]
                     {
                         KPlatonicShape.Tetrahedron, KPlatonicShape.Cube, KPlatonicShape.Octahedron,
                         KPlatonicShape.Dodecahedron, KPlatonicShape.Icosahedron
                     })
            {
                KPlatonicShape captured = shape;
                _effects.Add(new EffectEntry
                {
                    Name = "Poly-" + captured,
                    Note = "platonic solid (PortHackV2 animation; spins up, then brakes)",
                    Duration = 0f,
                    Update = dt => UpdatePolyPreview(captured, dt),
                    Draw3D = (gd, sb, b, t) =>
                    {
                        _polySeq.Shape = captured;
                        _polySeq.RenderFrame(gd, sb, b, GetRainbowColor());
                    }
                });
            }

            // ============ 原版 PortHack 立方体序列（3D，走离屏）============

            // ⚠️ 原版序列内部自己累加时间（DrawSequence 里的 elapsedTime += t），
            // 而且它的旋转是 rotTime * 1.5 直接当弧度用 —— 每秒约 86°、20 层还各自错开，
            // 实际观感是"糊成一片"。这里用一个缩放后的时间喂它，让它慢下来能看清层。
            _effects.Add(new EffectEntry
            {
                Name = "PortHackCubeSequence",
                Note = "vanilla PortHack 20-layer cube (slowed down)",
                Duration = 0f,
                Update = dt => _cubeTime += dt * CubeTimeScale,
                Draw3D = (gd, sb, b, t) => _cubeSeq.DrawSequence(b, _cubeTime, 10f)
            });

            if (_effects.Count > 0)
                _statusText = StatusFor(0);
        }

        /// <summary>
        /// 多边体预览的时间线：跑一轮完整的"起转 → 匀速 → 收尾急刹 → 慢转"，然后重来。
        ///
        /// <para>
        /// 为什么要自己驱动时间线：<see cref="PortHackV2Sequence.CrackingFinished"/> 置位前，
        /// 内部时钟被夹在总时长以内、永远停在匀速全速段（那是给 Cracking 用的：
        /// 破解途中不该自己慢下来）。预览要看的是**整个动画的性格**，
        /// 所以这里到点就置位让它进急刹窗口，循环展示。
        /// </para>
        /// </summary>
        private void UpdatePolyPreview(KPlatonicShape shape, float dt)
        {
            const float total = 6f;          // 匀速段时长（够看清转速）
            const float cycle = total + 3.5f; // 匀速 + 收尾窗口，之后重来
            _polySeq.Shape = shape;

            if (_timer >= cycle)
            {
                _polySeq.Reset();
                _timer = 0f;
            }
            _polySeq.CrackingFinished = _timer >= total;
            _polySeq.Update(dt, _timer, total);
        }

        /// <summary>
        /// 预览用的"负荷"曲线：0 →（4 秒内升到满）→ 满档保持 3 秒 → 回落，循环。
        /// 保持 3 秒是为了看清"满负荷"是什么样 —— 纯三角波只有一瞬到达峰值。
        /// </summary>
        private float LoadRamp()
        {
            const float up = 4f, hold = 3f, down = 4f;
            const float period = up + hold + down;
            float t = _timer % period;
            if (t < up) return t / up;
            if (t < up + hold) return 1f;
            return 1f - (t - up - hold) / down;
        }

        private string StatusFor(int i)
            => $"{_effects[i].Name}  -  {i + 1}/{_effects.Count}";

        public override void OnUpdate(float dt)
        {
            base.OnUpdate(dt);
            if (_effects.Count == 0) return;

            if (!_paused) _timer += dt;

            // Update 一直跑（暂停时也传染 dt=0，让效果停住而不是累积跳变）
            float use = _paused ? 0f : dt;
            _effects[_currentIndex].Update?.Invoke(use);

            EffectEntry cur = _effects[_currentIndex];
            if (cur.Duration > 0f && _timer >= cur.Duration) NextEffect();
        }

        private void NextEffect() => Goto((_currentIndex + 1) % _effects.Count);
        private void PrevEffect() => Goto((_currentIndex - 1 + _effects.Count) % _effects.Count);

        private void Goto(int index)
        {
            // 换条目时重置一次性状态，否则上一个效果的残留会让观感串味
            _polySeq.Reset();
            _cubeSeq.Reset();
            _cubeTime = 0f;
            _flowField.Reset();
            _timer = 0f;
            _currentIndex = index;
            _statusText = StatusFor(index);
        }

        /// <summary>
        /// 原版 PortHack 立方体序列的时间缩放。
        ///
        /// <para>
        /// 原版 <c>PortHackExe</c> 的参数是 <c>CRACK_TIME = 6f</c>，
        /// 调用 <c>DrawSequence(dest, t, CRACK_TIME)</c> 且 <c>t</c> 是**真实帧间隔** ——
        /// 也就是 6 秒里转约 5.3 弧度（**平均约 50°/秒**，内层还更快），
        /// 它本来就是设计成一闪而过的破解动画。预览里必须大幅减速才看得清层级。
        /// </para>
        ///
        /// <para>
        /// 0.04 = 1/25 速度，一圈约 22 秒；0.16（上一版）实测仍偏快。
        /// 想更慢就继续降这个数。
        /// </para>
        /// </summary>
        private const float CubeTimeScale = 0.04f;

        private float _cubeTime;

        public override void Draw(float t)
        {
            base.Draw(t);
            drawOutline();
            drawTarget();

            // 内容区**紧跟标题栏**（bounds 去掉面板高），与 ProxyKiller / PortHackV2 一致。
            // 原先这里多减了 14（标题栏高度），等于内容区凭空下移一条标题栏 ——
            // 效果绘制区整体偏低。
            // ⚠️ 按钮条画在内容区**内部底部**：放到内容区外面会盖到相邻的 exe 窗口。
            const int BUTTON_ROW_H = 30;
            Rectangle bg = new(bounds.X + 2, bounds.Y + PANEL_HEIGHT,
                               bounds.Width - 4, bounds.Height - PANEL_HEIGHT - BUTTON_ROW_H);
            if (bg.Width <= 8 || bg.Height <= 8) return;
            _lastContentH = bg.Height;
            spriteBatch.Draw(Utils.white, bg, new Color(10, 10, 10));

            if (_effects.Count == 0) return;
            EffectEntry cur = _effects[_currentIndex];

            // ⚠️ 不要给内容乘 fade。原版的退场机制是"窗口高度收缩到 0"（ExeModule.Update 里
            // bounds.Height = ramCost = baseRamCost * fade），内容跟着被裁掉才是原版观感；
            // 再乘一层 fade 会让它过早变暗、看着像"没在动"。退出期间照常按原亮度绘制即可。
            _flowField.Opacity = 1f;

            if (cur.Draw3D != null) DrawOffscreen(cur.Draw3D, bg);
            else cur.Draw2D?.Invoke(spriteBatch, bg, _timer);

            // 顶部：序号 + 名称；左下角：一句话说明
            TextItem.doFontLabel(new Vector2(bg.X + 6, bg.Y + 4), _statusText, GuiData.font, GetRainbowColor());
            if (!string.IsNullOrEmpty(cur.Note))
                TextItem.doFontLabel(new Vector2(bg.X + 6, bg.Y + bg.Height - 20), cur.Note,
                    GuiData.smallfont, Color.White * 0.6f);

            // 负荷指示：只有自绘流场在乎它，但统一显示便于对照
            string load = "load " + (LoadRamp() * 100f).ToString("000") + "%";
            // 流场额外报一段参数 —— 排查"分层""粒子消失""速度不够"时看它最直接
            if (cur.Draw2D != null && cur.Name == "FlowField")
                load += $"  speed x{_flowField.MinSpeedFactor:0.00}~x{_flowField.MaxSpeedFactor:0.00}"
                     + $" /{_flowField.SpeedTiers}tier  life x{_flowField.LifeGain:0.00}"
                     + $"  trail {_flowField.TrailPixels:0}px x{_flowField.LoadTrailGain:0.0}"
                     + $"  n={_flowField.EffectiveCount(bg.Height)}"
                     + $" = {_flowField.DensityCoefficient:0.###}xh^{_flowField.DensityExponent:0.###}"
                     + $"  spd {_flowField.SpeedPxPerSecond(bg.Height):0}px/s";
            TextItem.doFontLabel(new Vector2(bg.X + bg.Width - 90, bg.Y + 4), load,
                GuiData.smallfont, Color.White * 0.5f);

            // 按钮条贴内容区底部内侧
            int btnY = bg.Y + bg.Height - 26;
            if (Button.doButton(9001 + PID, bg.X + 6, btnY, 50, 22, "Prev", os.highlightColor))
                PrevEffect();
            if (Button.doButton(9002 + PID, bg.X + 62, btnY, 46, 22, _paused ? "Play" : "Pause", os.lockedColor))
                _paused = !_paused;
            if (Button.doButton(9003 + PID, bg.X + 114, btnY, 50, 22, "Next", os.highlightColor))
                NextEffect();
            // 缩放试验：切几个窗口高度，验证效果在"默认小窗口"与"自定义大窗口"下构图是否一致
            if (Button.doButton(9005 + PID, bg.X + 172, btnY, 58, 22, "Size", os.highlightColor))
                CycleSize();
            if (Button.doButton(9004 + PID, bounds.X + bounds.Width - 34, btnY, 28, 22, "X", os.brightLockedColor))
                isExiting = true;
        }

        /// <summary>
        /// 循环切换窗口高度（100 / 200 / 400 / 600）。这是选型时最关键的一条验证：
        /// 效果在默认小窗口与自定义大窗口下是否同一构图，光看代码判断不了。
        /// </summary>
        private void CycleSize()
        {
            int[] heights = { 100, 200, 400, 600 };
            _sizeIndex = (_sizeIndex + 1) % heights.Length;
            int h = heights[_sizeIndex];
            int minH = PANEL_HEIGHT + 30 + 20;    // 面板高 + 按钮条 + 内容区最少 20
            bounds.Height = Math.Max(minH, h);
            // ⚠️ 原版 ExeModule.Update 每帧执行 bounds.Height = ramCost，所以光改 bounds 会被覆盖回去
            ramCost = bounds.Height;
            KELog.Info($"[EffectsPlayer] window height -> {bounds.Height}px.");
        }

        private int _sizeIndex;

        /// <summary>
        /// 走离屏渲染再贴回来。
        ///
        /// <para>
        /// 为什么必须离屏：<c>OS.Draw</c> 在所有 exe 的 <c>Draw</c> 之前已经 <c>spriteBatch.Begin()</c>
        /// 且中途不会 <c>End</c>，所以这里既不能直接开始第二个批次（会抛
        /// "Begin cannot be called again until End has been successfully called"），
        /// 也不能带着活动批次切 RenderTarget。做法与 PortHackV2 动画、CustomHeartDaemon 一致：
        /// 切到离屏目标 → 专用批次 Begin/Draw/End → 切回来 → 用主批次贴图。
        /// </para>
        /// </summary>
        private void DrawOffscreen(Action<GraphicsDevice, SpriteBatch, Rectangle, float> draw, Rectangle dest)
        {
            GraphicsDevice gd = spriteBatch.GraphicsDevice;
            try
            {
                if (_rt == null || _rt.Width != dest.Width || _rt.Height != dest.Height)
                {
                    _rtBatch ??= new SpriteBatch(gd);
                    _rt?.Dispose();
                    _rt = new RenderTarget2D(gd, dest.Width, dest.Height);
                }

                RenderTarget2D prev = Utils.GetCurrentRenderTarget();
                gd.SetRenderTarget(_rt);
                gd.Clear(Color.Transparent);
                _rtBatch.Begin();
                // 离屏坐标从 (0,0) 起 —— 效果自己不知道它最终会被贴到哪
                draw(gd, _rtBatch, new Rectangle(0, 0, dest.Width, dest.Height), _timer);
                _rtBatch.End();
                gd.SetRenderTarget(prev);

                // AddativeWhite 让线条叠加发光（原版 PortHack 也是这么贴它的立方体的）
                spriteBatch.Draw(_rt, dest, Utils.AddativeWhite);
            }
            catch (Exception ex)
            {
                if (!_rtWarned)
                {
                    _rtWarned = true;
                    KELog.Warn($"[EffectsPlayer] offscreen draw failed: {ex.Message}");
                }
                // 失败时至少让面板可见，而不是静默全黑
                spriteBatch.Draw(Utils.white, dest, Color.Black * 0.6f);
            }
        }

        private bool _rtWarned;

        /// <summary>返回当前帧的彩虹色（来自 CustomColor 系统），供特效 Draw 使用。</summary>
        private Color GetRainbowColor()
        {
            if (_rainbowConfig != null)
                return Managers.CustomColorManager.CalcColor(_rainbowConfig, OS.currentElapsedTime);
            return Color.Cyan;
        }
    }
}
