using Hacknet;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace KernelExtensions.Utilities;

/// <summary>
/// **多边体演出序列**：单个柏拉图立体的线框/实心动画 —— 只绕「过顶点的 n 重对称轴」纯转，
/// 没有线框尾迹（原版靠 20 层螺旋尾迹堆体积感，这里是单立体，靠相位与角速度表达节奏）。
///
/// <para>
/// 几何来自 <see cref="KEPolyhedron"/>、默认转速来自 <see cref="KEPolyhedronAnimation"/>；
/// 本类只负责**怎么动、怎么画**。目前有两个消费者：端口破解程序（PortHackV2）的破解演出，
/// 以及效果播放器对 5 个立体的预览 —— 所以它虽然脱胎于前者，**并不专属**于它。
/// </para>
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
public class KEPolyhedronSequence : IDisposable
{
    // ---- 时间轴（秒 / 弧度每秒） ----
    private const float STARTUP_RAMP = 0.4f;        // 浮现缩放窗口：并入起转，不再单独占一段时长

    /// <summary>
    /// 匀速（Cracking 期间）的默认目标角速度（弧度/秒）。真值在
    /// <see cref="Utilities.KEPolyhedronAnimation.DefaultSpinSpeed"/>（动画层持有；配置层也引用它）。
    /// </summary>
    public const float DEFAULT_SPIN_SPEED = Utilities.KEPolyhedronAnimation.DefaultSpinSpeed;

    /// <summary>起转角速度相对目标转速的比例（3.2 时即 0.3）。</summary>
    private const float OMEGA0_RATIO = 0.3f / DEFAULT_SPIN_SPEED;

    /// <summary>急刹后的慢转角速度相对目标转速的比例（3.2 时即 0.5）。</summary>
    private const float OMEGA_IDLE_RATIO = 0.5f / DEFAULT_SPIN_SPEED;

    private const float SPINDOWN_DECAY = 5f;        // 急刹曲线强度（指数）；1.7s 的窗口里看得见减速
    private const float IDLE_STOP_DECAY = 1.5f;     // IdleSpin=false 时从 ω_idle 停住的时间常数

    /// <summary>起转时长占总时长的比例；仅当 <see cref="SpinUpSeconds"/> &lt; 0 时生效。</summary>
    public const float DEFAULT_SPINUP_RATIO = Utilities.KEPolyhedronAnimation.DefaultSpinUpRatio;

    /// <summary>急刹默认时长（秒）。</summary>
    public const float DEFAULT_SPINDOWN_SECONDS = Utilities.KEPolyhedronAnimation.DefaultSpinDownSeconds;

    /// <summary>
    /// 匀速（Cracking 期间）的**目标角速度**（弧度/秒）。默认 <see cref="DEFAULT_SPIN_SPEED"/>（3.2）。
    ///
    /// <para>
    /// 起转角速度与急刹后的慢转角速度按**固定比例**跟着它缩放（分别是 <c>0.094</c> 与 <c>0.156</c> 倍），
    /// 以保持"起转慢 → 匀速快 → 急刹回慢"这条曲线不变形 —— 这是调转速时通常想要的效果。
    /// 想单独控制慢转请看 <see cref="IdleSpinSpeed"/>。
    /// </para>
    ///
    /// <para>
    /// ⚠️ 数值越高，单帧转角越大：60fps 下每帧约 <c>SpinSpeed × 0.955</c> 度。
    /// 3.2 → 约 3.1°（原默认，稳）；6.4 → 约 6.1°（已能看出跳）；12 → 约 11.5°（明显跳帧）。
    /// 低于 20fps 时倍数更大。所以快是好，但别指望能无限快。
    /// </para>
    /// </summary>
    public float SpinSpeed { get; set; } = DEFAULT_SPIN_SPEED;

    /// <summary>
    /// 急刹结束后**慢转**的角速度（弧度/秒）。
    /// <c>&lt; 0</c>（默认）= 按 <see cref="SpinSpeed"/> 等比缩放（3.2 时即 0.5）。
    ///
    /// <para>
    /// 只在 <see cref="IdleSpin"/> 为 true 时有意义 —— 那是 Cracking 结束后的成功演出，
    /// 立体作为背景继续慢转；<c>IdleSpin</c> 为 false 时它平滑停住、用不到这个值。
    /// </para>
    /// </summary>
    public float IdleSpinSpeed { get; set; } = Utilities.KEPolyhedronAnimation.DefaultIdleSpinSpeed;

    /// <summary>起转角速度 = 目标转速 × 固定比例。</summary>
    private float OmegaStart => SpinSpeed * OMEGA0_RATIO;

    /// <summary>慢转角速度：单独配了就用配置值，否则按目标转速等比缩放。</summary>
    private float OmegaIdle => IdleSpinSpeed >= 0f ? IdleSpinSpeed : SpinSpeed * OMEGA_IDLE_RATIO;

    /// <summary>
    /// 单帧时长上限（秒）。低于 20fps 时演出宁可走慢一点，也不让单帧转角大到看起来是「跳帧闪烁」。
    ///
    /// <para>
    /// ⚠️ 它只夹**时长**，不夹**角速度** —— 所以把 <see cref="SpinSpeed"/> 调得很高时，
    /// 单帧转角会随之变大。那个"跳帧"由作者自己权衡（换算见 <see cref="SpinSpeed"/>）。
    /// </para>
    /// </summary>
    private const float MAX_STEP = 0.05f;

    // ---- 相机与投影 ----
    private const float CAMERA_DISTANCE = 3f;       // 模型半径 1，这个距离下立体约占画面短边 85%
    private const float TRAIL_CAMERA_DISTANCE = 7f; // 轨迹模式固定相机：不随层数/尺寸参数变化
    private const float CAMERA_PITCH_DEG = 15f;         // 轻俯角：相机略高于物体往下看（这个观感已确认合适）
    private const float CAMERA_YAW_DEG = 0f;            // 正对 Z 轴；水平自转的"从左向右"要正对着看才正

    /// <summary>
    /// 自转方向。角度增大时**前面的面向屏幕右方移动** —— 正面看就是"从左往右"，
    /// 用户确认这个是**逆时针**（我先前按俯视判断成顺时针，判错了）。
    /// </summary>
    private const float SPIN_DIRECTION_SIGN = 1f;
    private const float FOV_Y_DEG = 45f;            // 纵向视场角；窄窗口按宽高比放大，保证横向也装得下

    /// <summary>相机方向（单位向量，从原点指向相机位置）。</summary>
    private static readonly Vector3 CameraDirection = BuildCameraDirection();

    // ---- 动画状态（字段默认值等价于已 Reset：角度 0、缩放 0） ----
    private float _angle;
    private float _omega;
    private float _scale;
    private float _idleTime;        // 慢转区内的停留时长（IdleSpin=false 时用来慢慢停住）
    private float _postCrackTime;   // Cracking 结束后又过了多久（驱动急刹窗口）

    // 多层尾迹的 spindown 因子（对应原版 DrawSequence 的 num8 / num9 / num7）：
    // runtime 都是 1；急刹时分别收缩尺寸、收掉层间旋转偏移、降低转速；idle 保持末值。
    private float _layerScaleFactor = 1f;
    private float _layerTorsionFactor = 1f;
    private float _layerSpinFactor = 1f;

    // ---- 时间轴（由 ApplyTimeline 按总时长结算） ----
    private float _totalSeconds = 10f;
    private float _spinUpTime;      // 起转（浮现 + 加速）实际时长
    private float _spinDownTime;    // 急刹实际时长（已按剩余截断）

    // ---- 渲染资源（惰性创建；设备或尺寸变化时重建） ----
    private GraphicsDevice _device;
    private RenderTarget2D _wireframeTexture;
    private BasicEffect _effect;
    private VertexBuffer _vertexBuffer;

    /// <summary>实心面的顶点缓冲（三角形列表）。<see cref="KEPolyhedronStyle.Wireframe"/> 时不建。</summary>
    private VertexBuffer _faceBuffer;

    private RasterizerState _rasterizer;

    /// <summary>画实心面用的光栅状态：开启背面剔除 + 深度测试（线框那个是 <c>CullNone</c>）。</summary>
    private RasterizerState _faceRasterizer;

    private KEPlatonicShape _vertexBufferShape = (KEPlatonicShape)(-1);
    private KEPlatonicShape _axisShape = (KEPlatonicShape)(-1);
    private Vector3 _axis = Vector3.UnitY;
    /// <summary>把自转轴摆到竖直的固定姿态矩阵（见 <see cref="KEPolyhedron.OrientationMatrix"/>）。</summary>
    private Matrix _orientation = Matrix.Identity;
    private int _edgeCount;
    private Color _lastColor = Color.White;
    private bool _renderFailed;
    private bool _disposed;

    private KEPlatonicShape _shape = KEPlatonicShape.Cube;

    private KEPolyhedronStyle _presentation = KEPolyhedronStyle.Wireframe;

    /// <summary>呈现方式（见 <see cref="KEPolyhedronStyle"/>）。改动在下一帧生效（面缓冲按需重建）。</summary>
    public KEPolyhedronStyle Mode
    {
        get => _presentation;
        set => _presentation = value;
    }

    /// <summary>是否需要实心面（<see cref="KEPolyhedronStyle.Wireframe"/> 之外都要）。</summary>
    private bool WantsFaces => _presentation != KEPolyhedronStyle.Wireframe;

    /// <summary>是否需要线框（<see cref="KEPolyhedronStyle.Solid"/> 不要）。</summary>
    private bool WantsWire => _presentation != KEPolyhedronStyle.Solid;

    /// <summary>
    /// 线框是否**过深度测试**（即被实心面挡住的棱不可见）。
    ///
    /// <para>
    /// 只在"实心 + 线框"时为 true：那时棱的作用是**勾出实心体的边**，所以被挡住的必须藏起来；
    /// 纯线框模式下则相反 —— 看见背面那几条棱正是线框该有的样子。
    /// </para>
    /// </summary>
    private bool WireDepthTested => WantsFaces && WantsWire;

    /// <summary>
    /// 逐面明暗的**光向**（**纹素空间**，已归一）。
    ///
    /// <para>
    /// 为什么在纹素空间而不是世界空间：这个立体是个**单色剪影** —— 顶点色只是明暗，
    /// 真正的颜色由 <see cref="RenderFrame"/> 贴图时乘 <c>_lastColor</c> 给。
    /// 若在世界空间着色，明暗会随自转一起转（同一面转到背光侧才变暗），
    /// 看起来像"光跟着立体转"；在纹素空间着色则**光固定**，立体转过去面才变暗 ——
    /// 那才像实体。（代价：每帧按当前旋转重算顶点色，面数 4~20，可忽略。）
    /// </para>
    ///
    /// <para>
    /// ⚠️ <b>它必须与相机方向拉开角度。</b> 相机在 <c>(0, 0.259, 0.966)</c>（轻俯角 15°）。
    /// 早先取的是 <c>(-0.45, 0.8, 0.55)</c>，归一化后与相机方向的点积高达 <b>0.69</b> ——
    /// 等于"光几乎从相机这边照过去"，于是**所有朝向相机的面亮度都差不多**，立体看起来是平的
    /// （实机反馈即此）。现改为偏竖直、明显偏左：与相机方向点积降到 <b>0.34</b>，
    /// 竖直面之间才拉得开明暗。
    /// </para>
    /// </summary>
    private static readonly Vector3 FaceLightDirection = Vector3.Normalize(new Vector3(-0.55f, 0.88f, 0.25f));

    /// <summary>
    /// 最暗面的亮度下限。
    ///
    /// <para>
    /// ⚠️ 别调高。它抬高会**压掉对比度** —— 早先取 0.30，于是 0.30~1.0 这段里
    /// 好几个面挤在低端、还齐刷刷落在 0.30 的地板上，糊成一块。
    /// 取 0.22 是"背光面不被压死"与"明暗拉得开"之间的折中。
    /// </para>
    /// </summary>
    private const float FaceAmbient = 0.22f;

    /// <summary>
    /// 面顶点缓冲要画的**三角形个数**（各面扇化的三角形之和）。
    /// 用来告诉 <c>DrawPrimitives</c> 画多少。
    /// </summary>
    private int _faceTriangleCount;

    /// <summary>面缓冲所属的立体（换了要重建）。</summary>
    private KEPlatonicShape _faceBufferShape = (KEPlatonicShape)(-1);

    /// <summary>当前立体（默认立方体）。切换后下一帧重建顶点缓冲与旋转轴。</summary>
    public KEPlatonicShape Shape
    {
        get => _shape;
        set => _shape = value;
    }

    /// <summary>
    /// true = **Cracking 已结束（根级 <c>&lt;Time&gt;</c> 跑满）**，急刹从这个时刻才开始。
    ///
    /// 为什么需要这个标志：急刹在语义上是"破解完毕、收工"的表达，
    /// 而根级 <c>&lt;Time&gt;</c> 跑满才是那个时刻 —— 若按"匀速区间用完"就刹，急刹会提前到
    /// Cracking 还没结束时就发生，观众会看到立体先慢下来、进度条却还在走。
    /// 调用方在进 Complete 状态时置 true。
    /// </summary>
    public bool CrackingFinished { get; set; }

    /// <summary>
    /// true = 急刹结束后保持 ω_idle（约 0.5 rad/s）匀速慢转，供成功演出当背景；
    /// false = 从 ω_idle 平滑停住（中断 / 失败收尾时画面归于静止）。
    /// </summary>
    public bool IdleSpin { get; set; }

    /// <summary>
    /// 多层尾迹层数。1 = 当前单立体（默认，保持 PortHackV2 既有观感）；
    /// &gt;1 时同一个立体画 N 遍，附加逐层的尺寸/旋转差，并在 spindown 时向内收缩。
    /// 实心（<see cref="KEPolyhedronStyle.Solid"/>）模式暂不参与尾迹（强制按 1 层画，避免多层实心 z-fighting）。
    /// </summary>
    public int TrailLayers { get; set; } = 1;

    /// <summary>
    /// 是否为独立的"轨迹模式"。最终会作为作者唯一可见的开关（具体参数由内部预设控制）；
    /// 当前调参期仍保留 Trail* 细项，TrailLayers &gt; 1 也会自动按轨迹模式渲染。
    /// </summary>
    public bool TrailMode { get; set; }

    /// <summary>每层额外尺寸（相对模型半径）。0 = 所有层同尺寸；默认 0.10（20 层最外层约 2.9×，接近原版 2.6→7.35 的比例）。</summary>
    public float TrailSizeStep { get; set; } = 0.10f;

    /// <summary>每层绕自转轴的额外旋转偏移（弧度）。0 = 所有层同角度；默认 0.10。</summary>
    public float TrailRotationStep { get; set; } = 0.10f;

    /// <summary>
    /// 逐层转速脉冲强度（对齐原版 num12）。0 = 所有层同角速度；默认 0.05。
    /// 只在 TrailLayers &gt; 1 时生效。
    /// </summary>
    public float TrailPulse { get; set; } = 0.05f;

    /// <summary>
    /// spindown 是否收缩：true（默认）= 按原版 spindown 的有限程度收缩（scaleFactor 约 0.868、torsion 约 0.565，**不会合并成一个**）；
    /// false = 尾迹保持 runtime 的展开形态。
    /// </summary>
    public bool TrailContraction { get; set; } = true;

    /// <summary>
    /// 尾迹模式的相机俯角（度）。单层时仍用原来的 15°；多层时默认 30°，更容易看出层叠纵深。
    /// </summary>
    public float TrailCameraPitchDeg { get; set; } = 30f;

    /// <summary>
    /// 尾迹模式的相机水平角（度）。单层时仍为 0°；多层时默认 15°，让螺旋尾迹有方向感。
    /// </summary>
    public float TrailCameraYawDeg { get; set; } = 15f;

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
        _spinDownTime = spinDown;
    }

    /// <summary>回到初始状态（角度归零、缩放归零、<see cref="IdleSpin"/> / <see cref="CrackingFinished"/> 复位）。新一次破解演出开始时调用。</summary>
    public void Reset()
    {
        _angle = 0f;
        _omega = 0f;
        _scale = 0f;
        _idleTime = 0f;
        _postCrackTime = 0f;
        CrackingFinished = false;
        IdleSpin = false;
    }

    /// <summary>
    /// 推进时间轴。<paramref name="elapsed"/> 是 Cracking 已进行时长、<paramref name="totalSeconds"/>
    /// 是根级总时长。
    ///
    /// 角速度按 <paramref name="elapsed"/> 直接求值（纯函数，不看历史），角度再按帧间隔积分。
    /// 这样做有两个好处：把总时长改小时不会出现「刹住又加速」（elapsed 一过新的总时长就是慢转）；
    /// 调用方在成功演出期间让 elapsed 停住时，慢转仍能靠 dt 继续走。
    ///
    /// ⚠️ 急刹**只在 <see cref="CrackingFinished"/> 之后**发生：Cracking 期间 elapsed 被夹在
    /// <paramref name="totalSeconds"/> 以内，所以那段时间永远处于"最忙"的匀速全速状态；
    /// 总时长到了、调用方置位后，内部时钟才继续往前走并进入急刹窗口。
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

        // Cracking 结束后的真实时钟：从总时长那一刻起继续计，急刹窗口由这段时间驱动
        if (CrackingFinished) _postCrackTime += t;
        float crackClock = Math.Min(elapsed, _totalSeconds);   // Cracking 期间夹在总时长以内
        float clock = crackClock + (CrackingFinished ? _postCrackTime : 0f);

        // 慢转区停留时长：IdleSpin=false 时用它把 ω 从 ω_idle 平滑衰减到 0
        _idleTime = clock >= _totalSeconds + _spinDownTime ? _idleTime + t : 0f;

        _omega = EvaluateOmega(clock);
        _angle += _omega * t;
        _scale = EvaluateScale(clock);
        UpdateTrailFactors(clock);
    }

    /// <summary>
    /// 计算多层尾迹的 spindown 因子（对齐原版 DrawSequence）：
    /// num8 = 0.3 + 0.7*QuadraticOutCurve(1-p)（尺寸），num9 = 1-p（旋转偏移），num7 = 0.1+0.9*(1-p)（转速）。
    /// Cracking 期间/未开启收缩时全部为 1。
    /// </summary>
    private void UpdateTrailFactors(float clock)
    {
        if (!CrackingFinished || !TrailContraction || _spinDownTime <= 0f)
        {
            _layerScaleFactor = 1f;
            _layerTorsionFactor = 1f;
            _layerSpinFactor = 1f;
            return;
        }

        // ⚠️ 对齐原版 DrawSequence 的 spindown：原版 spindown 分支永远不会结束
        // （num4 = float.MaxValue），num6 = min(1, spindown)/2.3 最多到 1/2.3 ≈ 0.435。
        // 所以它只收缩到 scaleFactor ≈ 0.868 / torsion ≈ 0.565 就停住 —— 层不会完全合并。
        // 这里用归一化 brake 映射到同样的终值，保留原版的"收缩但不合并"观感。
        const float OriginalSpindownCap = 1f / 2.3f;

        float brake = Clamp01((clock - _totalSeconds) / _spinDownTime);
        float num6 = OriginalSpindownCap * brake;
        _layerScaleFactor = 0.3f + 0.7f * Utils.QuadraticOutCurve(1f - num6);
        _layerTorsionFactor = 1f - num6;
        _layerSpinFactor = num6 >= 0.4f ? 0.2f : 0.1f + 0.9f * (1f - num6);
    }

    /// <summary>按内部时钟求角速度：起转线性加速 → 匀速 <see cref="SpinSpeed"/> → 急刹指数落到
    /// <see cref="OmegaIdle"/> → 匀速慢转。</summary>
    private float EvaluateOmega(float clock)
    {
        if (_totalSeconds <= 0f && !CrackingFinished) return OmegaStart; // 总时长 0 且还没收尾：直接从起转开始
        if (clock < _spinUpTime)
        {
            float u = _spinUpTime <= 0f ? 1f : Clamp01(clock / _spinUpTime);
            return MathHelper.Lerp(OmegaStart, SpinSpeed, u);
        }
        // 匀速一直持续到 Cracking 结束（总时长跑满）
        if (!CrackingFinished || clock < _totalSeconds) return SpinSpeed;

        if (_spinDownTime <= 0f) return IdleOmega();
        float brake = Clamp01((clock - _totalSeconds) / _spinDownTime);
        return OmegaIdle + (SpinSpeed - OmegaIdle) * (float)Math.Exp(-SPINDOWN_DECAY * brake);
    }

    /// <summary>急刹结束后的角速度：保持 ω_idle 慢转，或按 <see cref="IdleSpin"/> 平滑停住。</summary>
    private float IdleOmega()
    {
        if (IdleSpin) return OmegaIdle;
        float omega = OmegaIdle * (float)Math.Exp(-_idleTime / IDLE_STOP_DECAY);
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
            KELog.Error($"[KEPolyhedronSequence] wireframe rendering disabled after failure: {ex.Message}");
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
            // 只画线，剔除模式其实不起作用；MultiSampleAntiAlias 配合 MSAA 让 1px 线别太硬。
            // DepthBias 是**负值** = 把线往相机方向拉一点：棱就落在面的边上，
            // 不做这个移位的话两者共面，深度比较时胜负随机 → 棱会一段一段地闪（z-fighting）。
            _rasterizer = new RasterizerState
            {
                CullMode = CullMode.None,
                FillMode = FillMode.Solid,
                MultiSampleAntiAlias = true,
                DepthBias = -100000
            };
        }
        if (_faceRasterizer == null)
        {
            // 画实心面：**开背面剔除**。
            //
            // ⚠️ 用 CullClockwiseFace 而不是默认的 CullCounterClockwiseFace —— 实测确认过。
            // KEPolyhedron 的面环在**模型空间**是"从外面看逆时针"（BuildFaces 统一外法线 +
            // OrderRing 按绕法线角度升序），但 `CreatePerspectiveFieldOfView` / `CreateLookAt`
            // 会把**手性翻过来**，投影到屏幕后就变成**顺时针**了。
            // 用默认值的结果是"保留背面、剔掉正面"（就是看到内壁和地板、正对相机的面消失），
            // 而**深度测试是好的**（没有 z-fighting）—— 所以那是纯粹的剔除方向错，不是深度问题。
            _faceRasterizer = new RasterizerState
            {
                CullMode = CullMode.CullClockwiseFace,
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
        if (WantsFaces) EnsureFaceBuffer(gd);
        if (_axisShape != Shape)
        {
            _axis = KEPolyhedron.SpinAxis(Shape);
            // 摆正姿态：把这根轴转到竖直，之后自转就绕世界 Y 轴 —— 正四面体于是绕
            // "顶点向底面作的垂线"转、二十面体绕"上下两个相对顶点连成的线"转，
            // 而不是绕一根歪轴晃。立方体/八面体/十二面体的轴本来就是竖直的，
            // 这个矩阵对它们是单位矩阵。
            _orientation = KEPolyhedron.OrientationMatrix(_axis);
            _axisShape = Shape;
        }
    }

    /// <summary>
    /// 内部 RenderTarget：优先 4x MSAA（FNA 改不了线宽，1px 线靠 MSAA 别太硬）；
    /// PreserveContents 让「这一帧没重画」时 <see cref="DrawOverlay"/> 还能贴上一帧的结果。
    /// 驱动不支持 MSAA 就退回无 MSAA；两种都失败则抛出，由 RenderFrame 统一兜底。
    ///
    /// <para>
    /// ⚠️ <b>必须带深度缓冲</b>。只画线时确实用不到（线框本来就该看见背面那几条棱，
    /// 深度状态也一直是 <c>None</c>）；但画实心面时没有深度测试就会**背面盖住正面**，
    /// 表现为一团乱 —— 且不会报任何错，只是难看。所以这里统一申请深度，
    /// 由绘制路径按是否需要实心面来决定用不用它。
    /// </para>
    /// </summary>
    private static RenderTarget2D CreateRenderTarget(GraphicsDevice gd, int width, int height)
    {
        try
        {
            return new RenderTarget2D(gd, width, height, false, SurfaceFormat.Color,
                DepthFormat.Depth24, 4, RenderTargetUsage.PreserveContents);
        }
        catch (Exception)
        {
            return new RenderTarget2D(gd, width, height, false, SurfaceFormat.Color,
                DepthFormat.Depth24, 0, RenderTargetUsage.PreserveContents);
        }
    }

    private void EnsureVertexBuffer(GraphicsDevice gd)
    {
        if (_vertexBuffer != null && _vertexBufferShape == Shape) return;

        Vector3[] verts = KEPolyhedron.Vertices(Shape);
        (int A, int B)[] edges = KEPolyhedron.Edges(Shape);
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

    /// <summary>
    /// 建面顶点缓冲：把每个凸多边形面**扇化成三角形**。
    ///
    /// <para>
    /// 绕序直接沿用 <see cref="KEPolyhedron.Faces"/> 给的（从外面看逆时针），
    /// 所以硬件背面剔除与 FNA 默认的 <c>CullCounterClockwiseFace</c> 直接对得上 ——
    /// 不必自己判断朝向。
    /// </para>
    ///
    /// <para>
    /// 顶点色全部写白：明暗由 <see cref="EnsureFaceShading"/> 每帧按当前旋转重算。
    /// 之所以不在这里就算好：本方法只在换立体时跑一次，而立体每帧都在转。
    /// </para>
    /// </summary>
    private void EnsureFaceBuffer(GraphicsDevice gd)
    {
        if (_faceBuffer != null && _faceBufferShape == Shape) return;

        Vector3[] verts = KEPolyhedron.Vertices(Shape);
        int[][] faces = KEPolyhedron.Faces(Shape);

        // 先算总数（每个 n 边形扇化出 n-2 个三角形）
        int tris = 0;
        foreach (int[] f in faces) if (f.Length >= 3) tris += f.Length - 2;
        var data = new VertexPositionColor[tris * 3];

        int w = 0;
        foreach (int[] f in faces)
        {
            if (f.Length < 3) continue;
            // 扇化：(v0, v_i, v_i+1) —— 对**凸**多边形总是正确且不越界
            for (int i = 1; i + 1 < f.Length; i++)
            {
                data[w++] = new VertexPositionColor(verts[f[0]], Color.White);
                data[w++] = new VertexPositionColor(verts[f[i]], Color.White);
                data[w++] = new VertexPositionColor(verts[f[i + 1]], Color.White);
            }
        }

        _faceBuffer?.Dispose();
        _faceBuffer = new VertexBuffer(gd, VertexPositionColor.VertexDeclaration, data.Length, BufferUsage.WriteOnly);
        _faceBuffer.SetData(data);
        _faceTriangleCount = tris;
        _faceBufferShape = Shape;
    }

    /// <summary>
    /// 按当前旋转重算逐面明暗，写回面缓冲的顶点色。
    ///
    /// <para>
    /// 在**纹素空间**做：法线乘 <c>_orientation</c> 与自转角，光向固定不动
    /// （见 <see cref="FaceLightDirection"/>）。这样一个面转到背光侧才变暗，观感是"实体在转"。
    /// </para>
    ///
    /// <para>
    /// 每个面**整片一个亮度**（平面着色），这正是低多边形"看得出体积"的来源；
    /// 逐顶点插值会把棱角磨平，反而糊。
    /// </para>
    /// </summary>
    private void EnsureFaceShading()
    {
        if (_faceBuffer == null || !WantsFaces) return;

        Vector3[] verts = KEPolyhedron.Vertices(Shape);
        int[][] faces = KEPolyhedron.Faces(Shape);

        // 与 RenderToTexture 里的 World 保持一致（等比缩放不影响方向，故只取旋转两段）
        Matrix rot = _orientation * Matrix.CreateFromAxisAngle(Vector3.UnitY, _angle * SPIN_DIRECTION_SIGN);

        var shades = new Color[faces.Length];
        for (int fi = 0; fi < faces.Length; fi++)
        {
            int[] f = faces[fi];
            if (f.Length < 3) { shades[fi] = Color.White; continue; }

            // 绕序在**模型空间**是"从外看逆时针"，故 (v1-v0) × (v2-v0) 即**外法线**。
            // ⚠️ 这里在模型空间算，与投影手性**无关** —— 别因为"剔除要反过来"就动这里：
            //    投影只翻转**屏幕空间**的绕序（影响 CullMode），不改变模型空间的叉积结果。
            Vector3 n = Vector3.Cross(verts[f[1]] - verts[f[0]], verts[f[2]] - verts[f[0]]);
            if (n.LengthSquared() < 1e-12f) { shades[fi] = Color.White; continue; }
            n.Normalize();
            n = Vector3.TransformNormal(n, rot);
            n.Normalize();

            // 点积 [-1,1] 先重映射到 [0,1] 再平方：
            // ⚠️ 不要写 max(0, dot) —— 那样凡是背对光的面**全都压在同一个地板值**上，
            //    八面体这种"多面同时背光"的形状会糊成一块死平的色块（实机反馈即此）。
            //    重映射后最亮面仍是 1.0，而背光面之间仍有**有序的**明暗差（暗者更暗）。
            float dot = Vector3.Dot(n, FaceLightDirection);
            float lambert = (dot + 1f) * 0.5f;
            lambert *= lambert;
            float lum = FaceAmbient + (1f - FaceAmbient) * lambert;
            shades[fi] = new Color(lum, lum, lum, 1f);
        }

        // 重建顶点色数组。面数 4~20、顶点数 ≤ 100 量级 —— 每帧重算与重传都可忽略，
        // 换来的是不必维护"面 → 顶点区间"的映射。
        int tris = 0;
        foreach (int[] f in faces) if (f.Length >= 3) tris += f.Length - 2;
        var data = new VertexPositionColor[tris * 3];

        int w = 0;
        for (int fi = 0; fi < faces.Length; fi++)
        {
            int[] f = faces[fi];
            if (f.Length < 3) continue;
            Color c = shades[fi];
            for (int i = 1; i + 1 < f.Length; i++)
            {
                data[w++] = new VertexPositionColor(verts[f[0]], c);
                data[w++] = new VertexPositionColor(verts[f[i]], c);
                data[w++] = new VertexPositionColor(verts[f[i + 1]], c);
            }
        }

        if (w > 0) _faceBuffer.SetData(data);
    }

    private static float SafeNonNegative(float value)
        => float.IsNaN(value) || float.IsInfinity(value) ? 0f : Math.Max(0f, value);

    private static float SafeFinite(float value, float fallback)
        => float.IsNaN(value) || float.IsInfinity(value) ? fallback : value;

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

            // 多层尾迹：同一个立体画 N 遍。
            // 尺寸/旋转偏移/转速脉冲三组因子在 spindown 时按原版 num8/num9/num7 的曲线收缩：
            // 外层（i 大）按 pow(scaleFactor, i) 指数收拢，层间角偏移按 torsionFactor 归零。
            // 实心模式暂不参与尾迹（多层实心会有 z-fighting/overdraw），强制按 1 层画。
            int layers = TrailMode ? Math.Max(2, TrailLayers) : Math.Max(1, TrailLayers);
            if (WantsFaces) layers = 1;
            if (layers > 64) layers = 64;

            float sizeStep = SafeNonNegative(TrailSizeStep);
            float rotStep = SafeNonNegative(TrailRotationStep);
            float pulseStrength = SafeNonNegative(TrailPulse);

            // ⚠️ 尾迹相机必须**固定**，不能按 TrailSizeStep 自动拉远：
            // 否则调大尺寸步长时相机同步后退，外层投影尺寸几乎不变（"怎么调都一样大"），
            // 而 spindown 收缩到最后只剩基础层，固定层在不同相机距离下投影尺寸差异很大。
            // 单层保持原 CAMERA_DISTANCE；轨迹模式用固定 TRAIL_CAMERA_DISTANCE。
            float cameraDistance = layers > 1 ? TRAIL_CAMERA_DISTANCE : CAMERA_DISTANCE;

            // 投影每帧按 dest 宽高比重设（不像原版那样启动时固化一次）。
            // 竖长窗口要按宽高比放大纵向视场角，否则立体横向会被裁掉。
            float aspect = dest.Width / (float)dest.Height;
            float halfFov = MathHelper.ToRadians(FOV_Y_DEG) * 0.5f;
            if (aspect < 1f) halfFov = (float)Math.Atan(Math.Tan(halfFov) / aspect);
            _effect.Projection = Matrix.CreatePerspectiveFieldOfView(halfFov * 2f, aspect, 0.1f, cameraDistance * 2f);
            float cameraPitch = layers > 1 ? SafeFinite(TrailCameraPitchDeg, 30f) : CAMERA_PITCH_DEG;
            float cameraYaw = layers > 1 ? SafeFinite(TrailCameraYawDeg, 15f) : CAMERA_YAW_DEG;
            _effect.View = Matrix.CreateLookAt(
                BuildCameraDirection(cameraPitch, cameraYaw) * cameraDistance, Vector3.Zero, Vector3.Up);
            for (int layer = 0; layer < layers; layer++)
            {
                float layerScale = _scale;
                float layerAngle = _angle * SPIN_DIRECTION_SIGN;

                if (layers > 1)
                {
                    float i = layer;
                    float n = layers;
                    float sizeFactor = (float)Math.Pow(_layerScaleFactor, i);
                    layerScale = _scale * (1f + i * sizeStep * sizeFactor);

                    float pulse = 1f + pulseStrength * _layerSpinFactor * ((n - i) / n);
                    // 符号与原版一致：外层 i 越大，角度越"落后"（负偏移）→ 内层领先、外层拖尾。
                    layerAngle = _angle * pulse * SPIN_DIRECTION_SIGN
                        - i * rotStep * _layerTorsionFactor;
                }

                _effect.World = Matrix.CreateScale(layerScale)
                    * _orientation                                          // 先把该立体的 n 重轴摆到竖直
                    * Matrix.CreateFromAxisAngle(Vector3.UnitY, layerAngle);

                if (_scale > 0.001f)
                {
                    // ── 实心面：先画（带深度，背面被硬件剔除），线框后画 ──
                    // 顺序不能反：线框用 DepthStencilState.None（要能看见背面那几条棱），
                    // 若先画线再画面，面会把线盖掉一部分，棱就断了。
                    if (WantsFaces && _faceBuffer != null && _faceTriangleCount > 0)
                    {
                        // 逐面明暗：按当前旋转重算顶点色（纹素空间，光固定）
                        EnsureFaceShading();

                        gd.RasterizerState = _faceRasterizer;             // 开背面剔除
                        gd.DepthStencilState = DepthStencilState.Default; // 开深度测试 + 写入
                        gd.SetVertexBuffer(_faceBuffer);

                        foreach (EffectPass pass in _effect.CurrentTechnique.Passes)
                        {
                            pass.Apply();
                            gd.DrawPrimitives(PrimitiveType.TriangleList, 0, _faceTriangleCount);
                        }
                    }

                    if (WantsWire)
                    {
                        gd.RasterizerState = _rasterizer;
                        gd.SetVertexBuffer(_vertexBuffer);

                        if (WireDepthTested)
                        {
                            // 实心 + 线框：**线也过深度测试** —— 被实心面挡住的棱不再透出来，
                            // 于是"实心体 + 高亮棱边"（低多边形观感），而不是半个线框骨架。
                            // ⚠️ 不写深度（只在面已写好的深度上比较），否则线自己会互相遮挡。
                            gd.DepthStencilState = DepthStencilState.DepthRead;
                        }
                        else
                        {
                            // 纯线框：本来就该看见背面那几条棱
                            gd.DepthStencilState = DepthStencilState.None;
                        }

                        foreach (EffectPass pass in _effect.CurrentTechnique.Passes)
                        {
                            pass.Apply();
                            gd.DrawPrimitives(PrimitiveType.LineList, 0, _edgeCount);
                        }
                    }
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
        _faceBuffer?.Dispose();
        _faceBuffer = null;
        _effect?.Dispose();
        _effect = null;
        _rasterizer?.Dispose();
        _rasterizer = null;
        _faceRasterizer?.Dispose();
        _faceRasterizer = null;
        _edgeCount = 0;
        _faceTriangleCount = 0;
        _vertexBufferShape = (KEPlatonicShape)(-1);
        _faceBufferShape = (KEPlatonicShape)(-1);
        _axisShape = (KEPlatonicShape)(-1);
    }

    private static float Clamp01(float x) => x <= 0f ? 0f : (x >= 1f ? 1f : x);

    private static Vector3 BuildCameraDirection()
        => BuildCameraDirection(CAMERA_PITCH_DEG, CAMERA_YAW_DEG);

    private static Vector3 BuildCameraDirection(float pitchDeg, float yawDeg)
    {
        float pitch = MathHelper.ToRadians(pitchDeg);
        float yaw = MathHelper.ToRadians(yawDeg);
        return new Vector3(
            (float)(Math.Sin(yaw) * Math.Cos(pitch)),
            (float)Math.Sin(pitch),
            (float)(Math.Cos(yaw) * Math.Cos(pitch)));
    }
}
