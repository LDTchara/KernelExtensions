using Hacknet;
using KernelExtensions.Utilities;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace KernelExtensions.Executables;

/// <summary>
/// 端口破解演出里的单个柏拉图立体线框动画：只绕「过顶点的 n 重对称轴」纯转，没有线框尾迹
/// （原版靠 20 层螺旋尾迹堆体积感，这里是单立体，靠相位与角速度表达节奏）。
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
    private const float STARTUP_TIME = 0.4f;        // 浮现：缩放 0 → 1
    private const float SPINUP_TIME = 1f;           // 起转：ω 从 ω0 缓慢加上去
    private const float SPINDOWN_TIME = 0.3f;       // 完成：指数急刹
    private const float OMEGA0 = 0.3f;              // 起转角速度
    private const float OMEGA_SPINUP_END = 1.6f;    // 起转结束时的角速度（与 runtime 起点连续）
    private const float OMEGA_MAX = 3.2f;           // 破解全程的角速度上限：60fps 单帧约 3.1°，不闪
    private const float OMEGA_IDLE = 0.5f;          // 演出结束后的慢转
    private const float SPINDOWN_DECAY = 12f;       // 急刹强度（指数衰减系数）
    private const float IDLE_STOP_DECAY = 1.5f;     // IdleSpin=false 时从 ω_idle 停住的时间常数

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

    private enum Phase
    {
        Startup = 0,
        SpinUp = 1,
        Runtime = 2,
        SpinDown = 3,
        Idle = 4
    }

    // ---- 动画状态（字段默认值等价于已 Reset：相位 Startup、缩放 0、角度 0） ----
    private Phase _phase = Phase.Startup;
    private float _phaseTime;
    private float _angle;
    private float _omega;
    private float _scale;
    private float _spinDownFrom;
    private float _runtimeFrom;
    private bool _completeSeen;

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
    /// true = 急刹结束后保持 ω_idle（约 0.5 rad/s）慢转，供 COMPLETE 演出当背景；
    /// false = 从 ω_idle 平滑停住，演出结束后画面静止。
    /// </summary>
    public bool IdleSpin { get; set; }

    /// <summary>回到初始状态（浮现开始、角度归零、<see cref="IdleSpin"/> 复位）。新一次破解演出开始时调用。</summary>
    public void Reset()
    {
        _phase = Phase.Startup;
        _phaseTime = 0f;
        _angle = 0f;
        _omega = 0f;
        _scale = 0f;
        _spinDownFrom = 0f;
        _runtimeFrom = 0f;
        _completeSeen = false;
        IdleSpin = false;
    }

    /// <summary>
    /// 推进时间轴。<paramref name="progress"/> 为破解进度 0~1：
    /// 浮现 0.4s（不转）→ 起转 1s（ω0 起加速）→ 全程随 progress 缓入加速到 ωmax
    /// → progress 到 1 时急刹 0.3s → 慢转（是否保持由 <see cref="IdleSpin"/> 决定）。
    /// </summary>
    /// <param name="t">帧间隔（秒）。非正数 / NaN 视作 0；超过上限会被夹住。</param>
    /// <param name="progress">破解进度 0~1，超界自动夹住。</param>
    public void Update(float t, float progress)
    {
        if (_disposed) return;
        if (float.IsNaN(t) || float.IsInfinity(t) || t <= 0f) t = 0f;
        else if (t > MAX_STEP) t = MAX_STEP;
        if (float.IsNaN(progress) || float.IsInfinity(progress) || progress < 0f) progress = 0f;
        else if (progress > 1f) progress = 1f;

        // 一帧内可能跨过多个相位（卡顿时），用剩余时间循环消耗；guard 只是防御性上限
        float remaining = t;
        int guard = 0;
        while (remaining > 1e-6f && guard++ < 8)
        {
            switch (_phase)
            {
                case Phase.Startup:
                {
                    float step = Math.Min(remaining, STARTUP_TIME - _phaseTime);
                    Integrate(step);
                    remaining -= step;
                    _omega = 0f; // 浮现阶段不转：先立起来，再起转
                    _scale = Utils.QuadraticOutCurve(Clamp01(_phaseTime / STARTUP_TIME));
                    if (_phaseTime >= STARTUP_TIME - 1e-6f)
                    {
                        _phase = Phase.SpinUp;
                        _phaseTime = 0f;
                        _scale = 1f;
                    }
                    break;
                }

                case Phase.SpinUp:
                {
                    float step = Math.Min(remaining, SPINUP_TIME - _phaseTime);
                    float u = Clamp01((_phaseTime + step) / SPINUP_TIME);
                    _omega = MathHelper.Lerp(OMEGA0, OMEGA_SPINUP_END, u * u); // 起步柔和地加速
                    Integrate(step);
                    remaining -= step;
                    if (_phaseTime >= SPINUP_TIME - 1e-6f)
                    {
                        _phase = Phase.Runtime;
                        _phaseTime = 0f;
                        _runtimeFrom = progress;
                    }
                    break;
                }

                case Phase.Runtime:
                {
                    // 以「进入 runtime 时的进度」为起点缓入加速：起点处 ω 与起转结束连续，progress=1 处到 ωmax
                    float span = 1f - _runtimeFrom;
                    float u = span > 1e-4f ? Clamp01((progress - _runtimeFrom) / span) : 1f;
                    _omega = MathHelper.Lerp(OMEGA_SPINUP_END, OMEGA_MAX, u * u);
                    Integrate(remaining);
                    remaining = 0f;

                    if (progress >= 1f && !_completeSeen)
                    {
                        _completeSeen = true;
                        _spinDownFrom = _omega;
                        _phase = Phase.SpinDown;
                        _phaseTime = 0f;
                    }
                    break;
                }

                case Phase.SpinDown:
                {
                    float step = Math.Min(remaining, SPINDOWN_TIME - _phaseTime);
                    float u = Clamp01((_phaseTime + step) / SPINDOWN_TIME);
                    _omega = OMEGA_IDLE + (_spinDownFrom - OMEGA_IDLE) * (float)Math.Exp(-SPINDOWN_DECAY * u);
                    Integrate(step);
                    remaining -= step;
                    if (_phaseTime >= SPINDOWN_TIME - 1e-6f)
                    {
                        _phase = Phase.Idle;
                        _phaseTime = 0f;
                        _omega = OMEGA_IDLE;
                    }
                    break;
                }

                default: // Phase.Idle
                {
                    if (IdleSpin)
                    {
                        _omega = OMEGA_IDLE;
                    }
                    else
                    {
                        _omega = OMEGA_IDLE * (float)Math.Exp(-_phaseTime / IDLE_STOP_DECAY);
                        if (_omega < 0.005f) _omega = 0f; // 视觉上已经停住，直接归零免得一直微动
                    }
                    Integrate(remaining);
                    remaining = 0f;
                    break;
                }
            }
        }
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

    /// <summary>按当前角速度推进角度与相位计时（step 为本段时长，秒）。</summary>
    private void Integrate(float step)
    {
        _angle += _omega * step;
        _phaseTime += step;
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
