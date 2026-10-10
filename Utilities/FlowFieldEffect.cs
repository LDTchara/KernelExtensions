using Hacknet;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace KernelExtensions.Utilities
{
    /// <summary>
    /// 流场（Flow Field）—— 数据洪流式的自定义背景。
    ///
    /// <para>
    /// 意图：表现"大量数据正被快速推过这里"，与 DDOS 那种**宏大且快速**的意味对齐。
    /// 画面由两层构成：
    /// <list type="bullet">
    ///   <item><b>底纹网格</b>：随流场缓慢漂移的点阵，给洪流一个"空间尺度"的参照</item>
    ///   <item><b>粒子流</b>：粒子沿随时间起伏的矢量场流动，各自拖出速度方向的短拖尾 ——
    ///     拖尾是"流动感"的来源（单帧的点看不出来，线段才看得出方向与速度）</item>
    /// </list>
    /// </para>
    ///
    /// <para>
    /// <b>缩放策略（这是本类的主要设计约束）</b>：位置与速度全部用 <b>0~1 归一化</b>坐标表示，
    /// 只在绘制时乘目标矩形的宽高。所以同一个效果在 100 像素高的小窗口和 600 像素的大窗口里
    /// **构图、流向、速度感完全一致**，不会像像素参数化的效果那样"换个尺寸就变味"。
    /// 这样做的代价与补偿：小窗口下拖尾会显得粗（像素比例大），所以拖尾长度按
    /// <c>min(宽,高)</c> 的比例给定，而不是固定像素。
    /// </para>
    ///
    /// <para>
    /// <b>负荷驱动</b>：<c>Load</c>（0~1）同时放大流速、拖尾长度与亮度 ——
    /// 负荷上升 = 洪流变快变亮，负荷回落 = 慢下来。这样它既能当"恒定的数据背景"，
    /// 也能当"负载在升"的可视化（配合 RAM 爬升之类的外部进度）。
    /// </para>
    /// </summary>
    public sealed class FlowFieldEffect
    {
        /// <summary>一个粒子。位置/速度用归一化坐标（0~1 为屏幕内）。</summary>
        private struct Particle
        {
            public Vector2 Pos;
            public Vector2 Vel;
            /// <summary>速度档位 0~1 —— 决定拖尾长度与亮度（快的更亮，视觉上"冲"）。</summary>
            public float Energy;
        }

        private readonly Particle[] _particles;
        private readonly Random _rng = new();

        /// <summary>时间累加（秒）。用它驱动流场起伏，因此暂停时把 dt 传 0 就会真的停住。</summary>
        private float _time;

        // ---- 可调观感参数（都用归一化单位或比例，不写死像素）----

        /// <summary>粒子数。</summary>
        public int Count { get; set; } = 220;

        /// <summary>基础流速（归一化坐标/秒）。负荷满时会乘上 <see cref="LoadSpeedGain"/>。</summary>
        public float BaseSpeed { get; set; } = 0.16f;

        /// <summary>负荷满时流速的倍数。</summary>
        public float LoadSpeedGain { get; set; } = 3.2f;

        /// <summary>拖尾长度（占 min(宽,高) 的比例）。0 = 只画点。</summary>
        public float TrailRatio { get; set; } = 0.055f;

        /// <summary>负荷满时拖尾的倍数（越快尾巴越长 —— 这是"高速"最直接的视觉线索）。</summary>
        public float LoadTrailGain { get; set; } = 2.4f;

        /// <summary>底纹网格的格数（按 min(宽,高) 分档后乘以它）。</summary>
        public int GridCells { get; set; } = 12;

        /// <summary>底纹网格是否绘制。</summary>
        public bool ShowGrid { get; set; } = true;

        /// <summary>粒子基准亮度（1 = 全亮，越小越含蓄）。</summary>
        public float BaseBrightness { get; set; } = 0.55f;

        /// <summary>
        /// 粒子单帧最大位移上限（归一化）。防止极端参数下 dt 抖动导致粒子瞬移穿过整个画面。
        /// </summary>
        public float MaxStepPerFrame { get; set; } = 0.08f;

        public FlowFieldEffect()
        {
            _particles = new Particle[512];
            Reset();
        }

        /// <summary>重新铺满粒子并复位时间。切换效果时调用，避免残留上一个效果的分布。</summary>
        public void Reset()
        {
            _time = 0f;
            // 用 Count 而不是整个数组长度铺，改 Count 后立即生效
            for (int i = 0; i < _particles.Length; i++)
            {
                _particles[i] = new Particle
                {
                    Pos = new Vector2((float)_rng.NextDouble(), (float)_rng.NextDouble()),
                    Vel = Vector2.Zero,
                    Energy = (float)_rng.NextDouble()
                };
            }
        }

        /// <summary>
        /// 流场函数：给定归一化位置与时间，返回归一化方向。
        ///
        /// <para>
        /// 用两层不同频率/相位的正弦叠出一个**无旋、有起伏**的场（而不是单向直线流）：
        /// 单向流看起来像雨，叠加之后才有"涡流与挤压"的感觉，更像被搅动的数据。
        /// 全部是三角函数的组合，没有随机项 —— 所以流向随时间平滑变化、不会跳。
        /// </para>
        /// </summary>
        private static Vector2 Field(float x, float y, float t)
        {
            // 主方向：整体从左向右 + 轻微上抬（数据被"推过去"）
            float vx = 1f;
            float vy = 0f;

            // 两层扰动：低频给大尺度涡流，高频给小尺度抖动
            // （net472 没有 MathF，用 (float)Math.Sin）
            vy += 0.55f * (float)Math.Sin(6.0 * x + 1.7 * t);
            vy += 0.30f * (float)Math.Sin(11.0 * y - 2.3 * t + 1.1);
            vx += 0.30f * (float)Math.Sin(4.5 * y + 1.3 * t);
            vx += 0.15f * (float)Math.Sin(9.0 * x - 3.1 * t);

            return Vector2.Normalize(new Vector2(vx, vy));
        }

        /// <summary>
        /// 推进粒子。<paramref name="load"/> = 0~1 负荷，驱动流速。
        /// 传 <c>dt = 0</c> 即完全静止（供暂停使用）。
        /// </summary>
        public void Update(float dt, float load)
        {
            if (dt <= 0f) return;

            load = MathHelper.Clamp(load, 0f, 1f);
            _time += dt;

            float speed = BaseSpeed * MathHelper.Lerp(1f, LoadSpeedGain, load);
            int count = (int)MathHelper.Clamp(Count, 1, _particles.Length);

            for (int i = 0; i < count; i++)
            {
                Particle p = _particles[i];
                Vector2 dir = Field(p.Pos.X, p.Pos.Y, _time);

                Vector2 vel = dir * speed;
                // 档位在速度方向上缓慢收敛：快的更快、慢的更慢，形成"快慢分层"的洪流
                float targetEnergy = MathHelper.Clamp(dir.Length() * 1f, 0f, 1f);
                p.Energy = MathHelper.Lerp(p.Energy, targetEnergy, dt * 1.5f);

                p.Vel = vel;

                Vector2 step = p.Vel * dt;
                float len = step.Length();
                if (len > MaxStepPerFrame) step *= MaxStepPerFrame / len;
                p.Pos += step;

                // 环绕出界：从另一侧回来（数据洪流是持续的，不该有"粒子用完了"的状态）
                if (p.Pos.X > 1f) p.Pos.X -= 1f;
                else if (p.Pos.X < 0f) p.Pos.X += 1f;
                if (p.Pos.Y > 1f) p.Pos.Y -= 1f;
                else if (p.Pos.Y < 0f) p.Pos.Y += 1f;

                _particles[i] = p;
            }
        }

        /// <summary>
        /// 绘制。<paramref name="dest"/> 是目标矩形，所有归一化坐标在这里乘上它的宽高。
        /// </summary>
        public void Draw(SpriteBatch sb, Rectangle dest, Color color, float load)
        {
            if (dest.Width <= 4 || dest.Height <= 4) return;

            load = MathHelper.Clamp(load, 0f, 1f);
            float minSide = Math.Min(dest.Width, dest.Height);
            float trailPx = minSide * TrailRatio * MathHelper.Lerp(1f, LoadTrailGain, load);
            float brightness = BaseBrightness * MathHelper.Lerp(0.75f, 1.4f, load);

            if (ShowGrid) DrawGrid(sb, dest, color, minSide, load, brightness);

            int count = (int)MathHelper.Clamp(Count, 1, _particles.Length);
            // 单帧内位置/速度取自同一次 Update 的结果，拖尾方向就是真实流向
            for (int i = 0; i < count; i++)
            {
                Particle p = _particles[i];
                Vector2 head = ToPixels(p.Pos, dest);
                Vector2 tail = head - p.Vel * trailPx * 8f;   // Vel 是"每秒"量，乘拖尾像素得到一个视觉长度

                if (trailPx <= 0.5f)
                {
                    sb.Draw(Utils.white, new Rectangle((int)head.X, (int)head.Y, 1, 1), color * (brightness * 0.9f));
                    continue;
                }

                Color c = color * (brightness * MathHelper.Lerp(0.45f, 1f, p.Energy));
                Utils.drawLine(sb, tail, head, Vector2.Zero, c, 0.3f);
            }
        }

        /// <summary>底纹网格：等距点阵，随流场轻微漂移 —— 给洪流一个空间参照。</summary>
        private void DrawGrid(SpriteBatch sb, Rectangle dest, Color color, float minSide, float load, float brightness)
        {
            // 格数按 min(宽,高) 分档：小窗口少画，大窗口密一些，保证"格子看起来一样大"
            int cells = (int)MathHelper.Clamp(GridCells, 2, 32);
            float stepY = (float)dest.Height / cells;
            float stepX = minSide / (float)cells;

            float drift = _time * 0.06f * MathHelper.Lerp(1f, LoadSpeedGain, load);
            Color gridColor = color * (brightness * 0.10f);

            for (float y = 0f; y < dest.Height; y += stepY)
            {
                // 每一行沿 X 反向漂移，行与行错开，形成"纹路在动"的错觉
                float rowDrift = (drift + y * 0.013f) % stepX;
                for (float x = -stepX; x < dest.Width; x += stepX)
                {
                    int px = (int)(dest.X + x + rowDrift);
                    int py = (int)(dest.Y + y);
                    sb.Draw(Utils.white, new Rectangle(px, py, 1, 1), gridColor);
                }
            }
        }

        /// <summary>归一化坐标 → 目标矩形内的像素坐标。</summary>
        private static Vector2 ToPixels(Vector2 norm, Rectangle dest)
            => new(dest.X + norm.X * dest.Width, dest.Y + norm.Y * dest.Height);
    }
}
