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
            //
            // ⚠️ 缩放 t 是**控制不住**这个序列的：它内部把累计时间切成
            // startup(0.6) → spinup(1.0) → runtime(到 totalTime-1.7) → spindown → idle 五段推进，
            // 缩放 t 等于把**整条时间线**一起拉长 —— 前 1.6 内部秒会被拉成 64 真实秒，
            // 期间它停在"浮现/起转"，而这几段里驱动画面变化的是各层缩放（num8 的幂次），
            // 观感上就不是"慢慢转"而是别的运动。
            //
            // 所以这里**不让它自己管时间**：我们按真实时间喂一个"恰好走完前两段"的推进量，
            // 之后让 elapsedTime 停在 runtime 起点（ShouldCentralSpinInfinitley 保证不进急刹），
            // 再接一个**固定角速度**的时钟。于是转速完全由 DRIVE_RATE 决定，可控。
            _cubeSeq.ShouldCentralSpinInfinitley = true;
            _effects.Add(new EffectEntry
            {
                Name = "PortHackCubeSequence",
                Note = "vanilla PortHack 20-layer cube (own rotate rate)",
                Duration = 0f,
                Update = dt => UpdateCubePreview(dt),
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
            _lastDt = dt;   // Draw 里的诊断要用它累计真实时间

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
            _cubeRealTime = 0f;
            _cubeFrames = 0;
            _flowField.Reset();
            _timer = 0f;
            _currentIndex = index;
            _statusText = StatusFor(index);
        }

        /// <summary>
        /// 立方体预览的驱动。
        ///
        /// <para>
        /// ⚠️ <c>DrawSequence(dest, t, totalTime)</c> 的 <c>t</c> 是**帧增量**（原版传的就是
        /// <c>OS.Update</c> 的帧间隔），**不是累计时钟**。早先的实现把"累计到 1.6 秒的时钟"
        /// 当成 t 每帧传进去，于是序列每帧都按 1.6 内部秒推进 ——
        /// <c>rotTime += 1.6</c> 每帧 ≈ 137 度/帧，60fps 下约 22 圈/秒，正是实测到的那种狂转。
        /// </para>
        ///
        /// <para>
        /// 正确做法：<c>t</c> 恒为"本帧要推进的内部秒数"，只调节它的大小来控速：
        /// <list type="bullet">
        ///   <item>前 <see cref="STARTUP_S"/>+<see cref="SPINUP_S"/> 内部秒：按 <c>dt × 1</c>
        ///     推进 —— 1.6 真实秒走完浮现与起转（与原版同速）。</item>
        ///   <item>之后：按 <c>dt × 目标速率 / 原生速率</c> 推进，于是匀速段转速 =
        ///     <see cref="CUBE_RATE_DEG_PER_SEC"/>。配合
        ///     <c>ShouldCentralSpinInfinitley = true</c>（runtime 段永不结束）就不会进急刹。</item>
        /// </list>
        /// </para>
        /// </summary>
        private void UpdateCubePreview(float dt)
        {
            const float RampTarget = STARTUP_S + SPINUP_S;

            // 原生速率：内部秒 → 度数（rotTime 每 1 内部秒让旋转角走 1.5 弧度）
            const float NativeDegPerInternalSec = 1.5f * 180f / MathHelper.Pi;   // ≈ 85.9

            float advance;
            if (_cubeTime < RampTarget)
                advance = dt;                                                    // 前两段：原生速度
            else
                advance = dt * (CUBE_RATE_DEG_PER_SEC / NativeDegPerInternalSec); // 匀速段：按目标角速度

            _cubeTime += advance;
            _cubeFrameAdvance = advance;    // 面板显示用
        }

        /// <summary>立方体匀速段的角速度（度/真实秒）—— 想调快慢只改这一个数（一圈秒数 = 360 / 它）。</summary>
        private const float CUBE_RATE_DEG_PER_SEC = 12f;

        /// <summary>最近一帧实际喂给序列的内部秒数（面板显示用）。</summary>
        private float _cubeFrameAdvance;

        /// <summary>立方体预览的时间线常量（原版分段：浮现 0.6s、起转 1.0s）。</summary>
        private const float STARTUP_S = 0.6f;
        private const float SPINUP_S = 1f;
        private const float RUNTIME_END_S = 10f - 1.7f;

        private float _cubeTime;

        /// <summary>底部按钮条高度（内容区之外、窗口之内）。</summary>
        private const int BUTTON_ROW_H = 30;
        /// <summary>底部信息条高度（两行文字：说明 + 参数）。</summary>
        private const int INFO_H = 32;

        /// <summary>最近一帧的 dt。诊断用：Draw 里累计真实时间来核对 cubeT 的增长速率。</summary>
        private float _lastDt;
        /// <summary>立方体条目激活以来的真实累计秒数与帧数（切条目时清）。</summary>
        private float _cubeRealTime;
        private int _cubeFrames;

        public override void Draw(float t)
        {
            base.Draw(t);
            drawOutline();
            drawTarget();

            // 内容区**紧跟标题栏**（bounds 去掉面板高），与 ProxyKiller / PortHackV2 一致。
            // 原先这里多减了 14（标题栏高度），等于内容区凭空下移一条标题栏 ——
            // 效果绘制区整体偏低。
            // ⚠️ 按钮条与**信息条**都画在内容区**内部底部**：放到内容区外面会盖到相邻的 exe 窗口。
            // 信息条单独留出来（原先诊断文字挤在右上角、还靠 doFontLabel 硬画，长了就溢出看不清）。
            Rectangle bg = new(bounds.X + 2, bounds.Y + PANEL_HEIGHT,
                               bounds.Width - 4, bounds.Height - PANEL_HEIGHT - BUTTON_ROW_H - INFO_H);
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

            // 顶部只留序号 + 名称（醒目，用彩色字体）
            TextItem.doFontLabel(new Vector2(bg.X + 6, bg.Y + 2), _statusText, GuiData.font, GetRainbowColor());

            // ---- 底部信息条：名称/说明 + 负荷 + 各效果的关键参数，**左对齐**、有底衬 ----
            // 原先挤在右上角、还靠 doFontLabel 硬画 —— 文本一长就溢出到看不见，也读不清。
            Rectangle info = new(bg.X, bg.Bottom, bg.Width, INFO_H);
            spriteBatch.Draw(Utils.white, info, Color.Black * 0.85f);

            // 第一行：一句话说明
            if (!string.IsNullOrEmpty(cur.Note))
                TextItem.doFontLabel(new Vector2(info.X + 6, info.Y + 2), cur.Note,
                    GuiData.smallfont, Color.White * 0.75f);

            // 第二行：负荷 + 各效果的关键参数
            string load = "load " + (LoadRamp() * 100f).ToString("000") + "%";
            if (cur.Draw2D != null && cur.Name == "FlowField")
                load += $"  speed x{_flowField.MinSpeedFactor:0.00}~x{_flowField.MaxSpeedFactor:0.00}"
                     + $" /{_flowField.SpeedTiers}tier  life x{_flowField.LifeGain:0.00}"
                     + $"  trail {_flowField.TrailPixels:0}px x{_flowField.LoadTrailGain:0.0}"
                     + $"  n={_flowField.EffectiveCount(bg.Height)}"
                     + $"  spd {_flowField.SpeedPxPerSecond(bg.Height):0}px/s";
            // 立方体序列报驱动状态：相位、目标角速度、累计旋转角，以及**每帧喂进去的内部秒数**。
            // t/frame 是关键：原生速度应约 0.016（=一帧）；若显示成 1.6 左右，
            // 就说明又把累计时钟当帧增量传了（那正是之前"一秒几十圈"的原因）。
            if (cur.Name == "PortHackCubeSequence")
            {
                _cubeRealTime += _lastDt;
                _cubeFrames++;

                string phase = _cubeTime < STARTUP_S ? "startup" :
                               _cubeTime < STARTUP_S + SPINUP_S ? "spinup" : "runtime";
                float spinDeg = MathHelper.ToDegrees(Math.Max(0f, _cubeTime - STARTUP_S - SPINUP_S) * 1.5f) % 360f;
                float fps = _cubeRealTime > 0.01f ? _cubeFrames / _cubeRealTime : 0f;

                load += $"  {phase}  target {CUBE_RATE_DEG_PER_SEC:0.#}deg/s  spin={spinDeg:0.0}deg"
                     + $"  t/frame={_cubeFrameAdvance:0.0000}s  inner={_cubeTime:0.0}s  fps={fps:0}";
            }
            TextItem.doFontLabel(new Vector2(info.X + 6, info.Y + 16), load,
                GuiData.smallfont, Color.White * 0.7f);

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
            int minH = PANEL_HEIGHT + BUTTON_ROW_H + INFO_H + 20;   // 面板高 + 按钮条 + 信息条 + 内容区最少 20
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
