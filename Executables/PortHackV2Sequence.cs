using Hacknet;
using KernelExtensions.Utilities;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace KernelExtensions.Executables;

/// <summary>
/// 端口破解演出里的单个柏拉图立体线框动画：只绕「过顶点的 n 重对称轴」纯转，没有线框尾迹
/// （原版靠 20 层螺旋尾迹堆体积感，这里是单立体，靠相位与角速度表达节奏）。
///
/// 节奏（三段之和恒等于调用方给的总时长）：
///   起转（浮现 + 加速，默认占总时长 <see cref="DEFAULT_SPINUP_RATIO"/>）→ 匀速 → 急刹
///   （默认 <see cref="DEFAULT_SPINDOWN_SECONDS"/> 秒，剩余不够就截断）→ 匀速慢转。
///
/// 绘制分工（与调用方约定，双方都不要越界）：
///   · 调用方管理 RenderTarget 与主 spriteBatch 的 Begin / End，并把最终结果贴回主屏；
///   · <see cref="RenderFrame"/> 只把立体画进内部 RenderTarget，再用调用方传入的 rtBatch 贴到
///     调用方当前绑定的目标上（进入时是什么目标，返回时还是什么目标）；
///   · <see cref="DrawOverlay"/> 把内部那张纹理按 opacity 贴到主 spriteBatch（完成后暗化用）。
///
/// 3D 部分自建 BasicEffect + VertexBuffer(LineList)：原版 Hacknet.Effects.Cube3D 是 internal
/// 类型且只有立方体，既不能扩展，也不该去反射内部类型。
/// </summary>
public class PortHackV2Sequence : IDisposable
{
    // ---- 时间轴（秒 / 弧度每秒） ----
    private const float STARTUP_RAMP = 0.4f;        // 浮现缩放窗口：并入起转，不再单独占一段时长
    private const float OMEGA0 = 0.3f;              // 起转角速度
    private const float OMEGA_MAX = 3.2f;           // 匀速角速度：60fps 单帧约 3.1°，不闪
    private const float OMEGA_IDLE = 0.5f;          // 急刹结束后的匀速慢转
    private const float SPINDOWN_DECAY = 5f;        // 急刹曲线强度（指数）；1.7s 的窗口里看得见减速
    private const float IDLE_STOP_DECAY = 1.5f;     // IdleSpin=false 时从 ω_idle 停住的时间常数

    /// <summary>起转时长占总时长的比例；仅当 <see cref="SpinUpSeconds"/> &lt; 0 时生效。</summary>
    public const float DEFAULT_SPINUP_RATIO = 0.1f;

    /// <summary>急刹默认时长（秒）。</summary>
    public const float DEFAULT_SPINDOWN_SECONDS = 1.7f;

    /// <summary>
    /// 单帧时长上限（秒）。低于 20fps 时演出宁可走慢一点，也不让单帧转角大到看起来是「跳帧闪烁」。
    /// </summary>
    private const float MAX_STEP = 0.05f;

    // ---- 相机与投影 ----
    private const float CAMERA_DISTANCE = 3f;       // 模型半径 1，这个距离下立体约占画面短边 85%
    private const float CAMERA_PITCH_DEG = 15f;     // 轻俯角：平视会正对赤道面，轮廓偏平
    private const float CAMERA_YAW_DEG = 25f;       // 方位角：避免正好沿某个对称轴看过去，转动才看得出来
    private const float FOV_Y_DEG = 45f;            // 纵向视场角；窄窗口按宽高比放大，保证横向也装得下

    /// <summary>相机方向（单位向量，从原点指向相机位置）。</summary>
    private static readonly Vector3 CameraDirection = BuildCameraDirection();

    // ---- 动画状态（字段默认值等价于已 Reset：角度 0、缩放 0） ----
    private float _angle;
    private float _omega;
    private float _scale;
    private float _idleTime;        // 慢转区内的停留时长（IdleSpin=false 时用来慢慢停住）
    private bool _woundDown;        // 已经收过尾（进过急刹 / 慢转），不再回到高速

    // ---- 时间轴（由 ApplyTimeline 按总时长结算） ----
    private float _totalSeconds = 10f;
    private float _spinUpTime;      // 起转（浮现 + 加速）实际时长
    private float _spinDownTime;    // 急刹实际时长（已按剩余截断）
    private float _steadyEnd;       // 匀速结束时刻 = 总时长 - 急刹时长

    // ---- 渲染资源（惰性创建；设备或尺寸变化时重建） ----
    private GraphicsDevice _device;
    private RenderTarget2D _wireframeTexture;
    private BasicEffect _effect;
    private VertexBuffer _vertexBuffer;
    private RasterizerState _rasterizer;
    private KPlatonicShape _vertexBufferShape = (KPlatonicShape)(-1);
    private KPlatonicShape _axisShape = (KPlatonicShape)(-1);
    private Vector3 _axis = Vector3.UnitY;
    private int _edgeCount;
    private Color _lastColor = Color.White;
    private bool _renderFailed;
    private bool _disposed;

    private KPlatonicShape _shape = KPlatonicShape.Cube;

    /// <summary>当前立体（默认立方体）。切换后下一帧重建顶点缓冲与旋转轴。</summary>
    public KPlatonicShape Shape
    {
        get => _shape;
        set => _shape = value;
    }

    /// <summary>
    /// true = 急刹结束后保持 ω_idle（约 0.5 rad/s）匀速慢转，供成功演出当背景；
    /// false = 从 ω_idle 平滑停住（中断 / 失败收尾时画面归于静止）。
    /// </summary>
    public bool IdleSpin { get; set; }

    /// <summary>
    /// 起转（浮现 + 加速）时长（秒）。<c>&lt; 0</c> = 用 <see cref="SpinUpRatio"/> × 总时长。
    /// 配置项留空即此情形。
    /// </summary>
    public float SpinUpSeconds { get; set; } = -1f;

    /// <summary>起转时长占总时长的比例；仅当 <see cref="SpinUpSeconds"/> &lt; 0 时生效，超界夹到 0~1。</summary>
    public float SpinUpRatio { get; set; } = DEFAULT_SPINUP_RATIO;

    /// <summary>急刹（减速）时长（秒）。剩余时间不够时自动截断（见 <see cref="ApplyTimeline"/>）。</summary>
    public float SpinDownSeconds { get; set; } = DEFAULT_SPINDOWN_SECONDS;

    /// <summary>
    /// 按总时长结算时间轴（每帧调用，所以运行时改配置也能立刻生效）：
    /// 起转 → 匀速 → 急刹，三段之和恒等于总时长（都是 0 的退化情形也不会超时）。
    ///
    /// 预算优先级：**起转优先、急刹取剩余**（用户约定「急刹剩余不够就截断」）。
    /// 这么定的理由：起转是比例预算（默认总时长的 10%），天然随总时长缩放，不会把急刹挤没；
    /// 而两者真的冲突时（总时长 ≲ 2s，或作者显式写了很大的起转绝对值），截急刹只是「刹得更干脆」，
    /// 截起转却会让「加速」这段视觉叙事整个消失 —— 观众直接看到一个满速立体，急刹也就没了对照。
    /// </summary>
    private void ApplyTimeline(float totalSeconds)
    {
        if (float.IsNaN(totalSeconds) || float.IsInfinity(totalSeconds) || totalSeconds < 0f) totalSeconds = 0f;
        _totalSeconds = totalSeconds;

        float ratio = SpinUpRatio;
        if (float.IsNaN(ratio) || ratio < 0f) ratio = DEFAULT_SPINUP_RATIO;
        if (ratio > 1f) ratio = 1f;
        float requested = SpinUpSeconds >= 0f ? SpinUpSeconds : _totalSeconds * ratio;
        _spinUpTime = Math.Min(requested, _totalSeconds);

        float spinDown = SpinDownSeconds;
        if (float.IsNaN(spinDown) || spinDown < 0f) spinDown = DEFAULT_SPINDOWN_SECONDS;
        _spinDownTime = Math.Min(spinDown, Math.Max(0f, _totalSeconds - _spinUpTime));

        _steadyEnd = _totalSeconds - _spinDownTime;
    }

    /// <summary>回到初始状态（角度归零、缩放归零、<see cref="IdleSpin"/> 复位）。新一次破解演出开始时调用。</summary>
    public void Reset()
    {
        _angle = 0f;
        _omega = 0f;
        _scale = 0f;
        _idleTime = 0f;
        _woundDown = false;
        IdleSpin = false;
    }

    /// <summary>
    /// 推进时间轴。<paramref name="elapsed"/> 是 Cracking 已进行时长、<paramref name="totalSeconds"/>
    /// 是根级总时长。
    ///
    /// 角速度按 <paramref name="elapsed"/> 直接求值（纯函数，不看历史），角度再按帧间隔积分。
    /// 这样做有两个好处：把总时长改小时不会出现「刹住又加速」（elapsed 一过新的总时长就是慢转）；
    /// 调用方在成功演出期间让 elapsed 停住时，慢转仍能靠 dt 继续走。
    /// </summary>
    /// <param name="t">帧间隔（秒）。非正数 / NaN 视作 0；超过上限会被夹住。</param>
    /// <param name="elapsed">Cracking 已进行时长（秒），由调用方累计。</param>
    /// <param name="totalSeconds">根级总时长（秒）。&lt;= 0 视作「立刻完成」。</param>
    public void Update(float t, float elapsed, float totalSeconds)
    {
        if (_disposed) return;
        if (float.IsNaN(t) || float.IsInfinity(t) || t <= 0f) t = 0f;
        else if (t > MAX_STEP) t = MAX_STEP;
        if (float.IsNaN(elapsed) || float.IsInfinity(elapsed) || elapsed < 0f) elapsed = 0f;

        ApplyTimeline(totalSeconds);

        // 慢转区停留时长：IdleSpin=false 时用它把 ω 从 ω_idle 平滑衰减到 0
        _idleTime = elapsed >= _totalSeconds ? _idleTime + t : 0f;

        bool woundDown = elapsed >= _steadyEnd; // 已进急刹区间（或更晚）
        float omega = EvaluateOmega(elapsed);

        // 已经收过尾就不再回到高速：总时长被改大时 elapsed 会重新落回匀速区，
        // 但演出已经收过尾，退回去会看到「刹住又加速」。一次性闩锁，Reset 时清掉。
        if (woundDown) _woundDown = true;
        else if (_woundDown) omega = Math.Min(omega, OMEGA_IDLE);

        _omega = omega;
        _angle += _omega * t;
        _scale = EvaluateScale(elapsed);
    }

    /// <summary>按 elapsed 求角速度：起转线性加速 → 匀速 ωmax → 急刹指数落到 ω_idle → 匀速慢转。</summary>
    private float EvaluateOmega(float elapsed)
    {
        if (_totalSeconds <= 0f) return IdleOmega(); // 总时长 0：直接进慢转

        if (elapsed < _spinUpTime)
        {
            float u = _spinUpTime <= 0f ? 1f : Clamp01(elapsed / _spinUpTime);
            return MathHelper.Lerp(OMEGA0, OMEGA_MAX, u);
        }
        if (elapsed < _steadyEnd) return OMEGA_MAX;

        if (elapsed >= _totalSeconds || _spinDownTime <= 0f) return IdleOmega();
        float brake = Clamp01((elapsed - _steadyEnd) / _spinDownTime);
        return OMEGA_IDLE + (OMEGA_MAX - OMEGA_IDLE) * (float)Math.Exp(-SPINDOWN_DECAY * brake);
    }

    /// <summary>急刹结束后的角速度：保持 ω_idle 慢转，或按 <see cref="IdleSpin"/> 平滑停住。</summary>
    private float IdleOmega()
    {
        if (IdleSpin) return OMEGA_IDLE;
        float omega = OMEGA_IDLE * (float)Math.Exp(-_idleTime / IDLE_STOP_DECAY);
        return omega < 0.005f ? 0f : omega; // 视觉上已经停住，直接归零免得一直微动
    }

    /// <summary>
    /// 浮现缩放：并入起转 —— 两者从 0 时刻同时开始，浮现不再单独占 0.4s 时长。
    /// 窗口取 min(0.4s, 起转时长)：长破解（总时长 30s → 起转 3s）若把浮现拉满整个起转窗口，
    /// 会变成「慢动作长大」；短破解则保证浮现一定在起转窗口内完成。
    /// </summary>
    private float EvaluateScale(float elapsed)
    {
        float ramp = Math.Min(STARTUP_RAMP, _spinUpTime);
        if (ramp <= 0f) return 1f; // 没有起转预算（总时长≈0 或作者把起转配成 0）：不拖浮现
        return Utils.QuadraticOutCurve(Clamp01(elapsed / ramp));
    }

    /// <summary>
    /// 把立体渲染到调用方当前已绑定的 RenderTarget 上：先在内部 RenderTarget 里画 3D 线框，
    /// 再用调用方已经 Begin 的 <paramref name="rtBatch"/> 贴到 <paramref name="dest"/>。
    /// 不碰调用方的 RenderTarget 归属 —— 返回时 GraphicsDevice 的 RenderTarget 与进入前一致。
    /// </summary>
    /// <param name="gd">当前图形设备。</param>
    /// <param name="rtBatch">调用方持有且已 Begin 的 SpriteBatch（这里不会 Begin / End 任何批次）。</param>
    /// <param name="dest">
    /// 目标矩形，用**当前已绑定 RenderTarget 的坐标系**（离屏图是面板大小就传 0,0,w,h；
    /// 离屏图与屏幕同尺寸就直接传内容区的屏幕矩形）。它同时决定内部 RenderTarget 尺寸与投影宽高比。
    /// </param>
    /// <param name="color">线框基色（通常 Color.White，由调用方在贴回时统一着色）。</param>
    public void RenderFrame(GraphicsDevice gd, SpriteBatch rtBatch, Rectangle dest, Color color)
    {
        if (_disposed || _renderFailed || gd == null || rtBatch == null) return;
        if (dest.Width <= 0 || dest.Height <= 0) return;

        try
        {
            EnsureResources(gd, dest);
            RenderToTexture(gd, dest);
            _lastColor = color;
            rtBatch.Draw(_wireframeTexture, dest, color);
        }
        catch (Exception ex)
        {
            // 只报一次并关闭渲染：驱动不支持时不该每帧刷日志，更不该把 exe 拖崩
            _renderFailed = true;
            KELog.Error($"[PortHackV2Sequence] wireframe rendering disabled after failure: {ex.Message}");
        }
    }

    /// <summary>
    /// 把最近一次渲染好的线框按 <paramref name="opacity"/> 贴回主 spriteBatch（调用方已 Begin）。
    /// 与 <see cref="RenderFrame"/> 二选一：同一帧两边都调用会画两遍。
    /// </summary>
    /// <param name="sb">主 spriteBatch（调用方已 Begin）。</param>
    /// <param name="dest">目标矩形，坐标含义同 <see cref="RenderFrame"/>。</param>
    /// <param name="opacity">不透明度 0~1，用于完成后的暗化。</param>
    public void DrawOverlay(SpriteBatch sb, Rectangle dest, float opacity)
    {
        if (_disposed || _renderFailed || sb == null || _wireframeTexture == null) return;
        if (float.IsNaN(opacity) || opacity <= 0f) return;
        if (opacity > 1f) opacity = 1f;
        sb.Draw(_wireframeTexture, dest, _lastColor * opacity);
    }

    /// <summary>释放 VertexBuffer / BasicEffect / 内部 RenderTarget。可重复调用。</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        ReleaseResources();
        GC.SuppressFinalize(this);
    }

    // ======================= 内部：渲染 =======================

    private void EnsureResources(GraphicsDevice gd, Rectangle dest)
    {
        if (!ReferenceEquals(_device, gd))
        {
            ReleaseResources();
            _device = gd;
        }

        if (_effect == null)
        {
            _effect = new BasicEffect(gd)
            {
                TextureEnabled = false, // 画线不需要贴图
                LightingEnabled = false,
                VertexColorEnabled = true,
                DiffuseColor = Vector3.One
            };
        }
        if (_rasterizer == null)
        {
            // 只画线，剔除模式其实不起作用；MultiSampleAntiAlias 配合 MSAA 让 1px 线别太硬
            _rasterizer = new RasterizerState
            {
                CullMode = CullMode.None,
                FillMode = FillMode.Solid,
                MultiSampleAntiAlias = true
            };
        }
        if (_wireframeTexture == null || _wireframeTexture.Width != dest.Width || _wireframeTexture.Height != dest.Height)
        {
            _wireframeTexture?.Dispose();
            _wireframeTexture = CreateRenderTarget(gd, dest.Width, dest.Height);
        }

        EnsureVertexBuffer(gd);
        if (_axisShape != Shape)
        {
            _axis = KPolyhedron.SpinAxis(Shape);
            _axisShape = Shape;
        }
    }

    /// <summary>
    /// 内部 RenderTarget：优先 4x MSAA（FNA 改不了线宽，1px 线靠 MSAA 别太硬）；
    /// PreserveContents 让「这一帧没重画」时 <see cref="DrawOverlay"/> 还能贴上一帧的结果。
    /// 驱动不支持 MSAA 就退回无 MSAA；两种都失败则抛出，由 RenderFrame 统一兜底。
    /// </summary>
    private static RenderTarget2D CreateRenderTarget(GraphicsDevice gd, int width, int height)
    {
        try
        {
            return new RenderTarget2D(gd, width, height, false, SurfaceFormat.Color,
                DepthFormat.None, 4, RenderTargetUsage.PreserveContents);
        }
        catch (Exception)
        {
            return new RenderTarget2D(gd, width, height, false, SurfaceFormat.Color,
                DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        }
    }

    private void EnsureVertexBuffer(GraphicsDevice gd)
    {
        if (_vertexBuffer != null && _vertexBufferShape == Shape) return;

        Vector3[] verts = KPolyhedron.Vertices(Shape);
        (int A, int B)[] edges = KPolyhedron.Edges(Shape);
        var data = new VertexPositionColor[edges.Length * 2];
        for (int i = 0; i < edges.Length; i++)
        {
            data[i * 2] = new VertexPositionColor(verts[edges[i].A], Color.White);
            data[i * 2 + 1] = new VertexPositionColor(verts[edges[i].B], Color.White);
        }

        _vertexBuffer?.Dispose();
        _vertexBuffer = new VertexBuffer(gd, VertexPositionColor.VertexDeclaration, data.Length, BufferUsage.WriteOnly);
        _vertexBuffer.SetData(data);
        _edgeCount = edges.Length;
        _vertexBufferShape = Shape;
    }

    private void RenderToTexture(GraphicsDevice gd, Rectangle dest)
    {
        RenderTargetBinding[] previousTargets = gd.GetRenderTargets();
        Viewport previousViewport = gd.Viewport;
        RasterizerState previousRasterizer = gd.RasterizerState;
        DepthStencilState previousDepth = gd.DepthStencilState;
        BlendState previousBlend = gd.BlendState;

        try
        {
            gd.SetRenderTarget(_wireframeTexture);
            gd.Clear(Color.Transparent);

            // 投影每帧按 dest 宽高比重设（不像原版那样启动时固化一次）。
            // 竖长窗口要按宽高比放大纵向视场角，否则立体横向会被裁掉。
            float aspect = dest.Width / (float)dest.Height;
            float halfFov = MathHelper.ToRadians(FOV_Y_DEG) * 0.5f;
            if (aspect < 1f) halfFov = (float)Math.Atan(Math.Tan(halfFov) / aspect);
            _effect.Projection = Matrix.CreatePerspectiveFieldOfView(halfFov * 2f, aspect, 0.1f, CAMERA_DISTANCE * 2f);
            _effect.View = Matrix.CreateLookAt(CameraDirection * CAMERA_DISTANCE, Vector3.Zero, Vector3.Up);
            _effect.World = Matrix.CreateScale(_scale) * Matrix.CreateFromAxisAngle(_axis, _angle);

            gd.RasterizerState = _rasterizer;
            gd.DepthStencilState = DepthStencilState.None; // 不挂深度缓冲：线框本来就该看见背面那几条棱
            gd.BlendState = BlendState.Opaque;
            gd.SetVertexBuffer(_vertexBuffer);

            if (_scale > 0.001f)
            {
                foreach (EffectPass pass in _effect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    gd.DrawPrimitives(PrimitiveType.LineList, 0, _edgeCount);
                }
            }
        }
        finally
        {
            // 调用方可能正持有一个已 Begin 的 SpriteBatch：动过的设备状态全部还原，
            // 尤其是 RenderTarget —— 调用方的批次是在它自己 End 时才真正落盘的。
            // （顶点缓冲不用管：SpriteBatch 在 flush 时会绑自己的）
            gd.RasterizerState = previousRasterizer;
            gd.DepthStencilState = previousDepth;
            gd.BlendState = previousBlend;
            if (previousTargets.Length == 0) gd.SetRenderTarget(null);
            else gd.SetRenderTargets(previousTargets);
            gd.Viewport = previousViewport; // SetRenderTarget 会重置视口，所以放在最后
        }
    }

    private void ReleaseResources()
    {
        _wireframeTexture?.Dispose();
        _wireframeTexture = null;
        _vertexBuffer?.Dispose();
        _vertexBuffer = null;
        _effect?.Dispose();
        _effect = null;
        _rasterizer?.Dispose();
        _rasterizer = null;
        _edgeCount = 0;
        _vertexBufferShape = (KPlatonicShape)(-1);
        _axisShape = (KPlatonicShape)(-1);
    }

    private static float Clamp01(float x) => x <= 0f ? 0f : (x >= 1f ? 1f : x);

    private static Vector3 BuildCameraDirection()
    {
        float pitch = MathHelper.ToRadians(CAMERA_PITCH_DEG);
        float yaw = MathHelper.ToRadians(CAMERA_YAW_DEG);
        return new Vector3(
            (float)(Math.Sin(yaw) * Math.Cos(pitch)),
            (float)Math.Sin(pitch),
            (float)(Math.Cos(yaw) * Math.Cos(pitch)));
    }
}
