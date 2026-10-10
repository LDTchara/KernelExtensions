using Hacknet;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace KernelExtensions.Utilities
{
    /// <summary>
    /// 上升数据流 —— 数据洪流式的自定义背景（"自下而上飞出去"）。
    ///
    /// <para>
    /// 结构参照原版 <c>RaindropsEffect</c>（矩阵雨），但方向相反、语义不同：
    /// <list type="bullet">
    ///   <item><b>上升粒子</b>：每个粒子从底边生成、带着自己的相位向上加速飞，
    ///     身后拖一条与速度成正比的竖线（快的尾巴长 —— "高速"最直接的线索）。
    ///     粒子各自独立上冲，不是整片一起漂。</item>
    ///   <item><b>底纹点阵</b>：整体向上缓慢漂移的点阵，给洪流一个"空间尺度"的参照。</item>
    /// </list>
    /// </para>
    ///
    /// <para>
    /// <b>尺寸策略（试出来的结论）</b>：粒子用<b>固定尺寸</b>，不随窗口缩放。
    /// 一开始按 <c>min(宽,高)</c> 的比例算尺寸，结果是 100 像素窗口里观感很好
    /// （方块与轨迹差不多宽），窗口一大就"不对劲"——因为按比例放大的方块变成了大色块，
    /// 而轨迹还是一条细线，两者比例失衡。改成固定尺寸后，**方块与轨迹在任何窗口下
    /// 都是同一对比例**，观感与 100 窗口一致；大窗口得到的是"更多粒子"而不是"更大的粒子"。
    /// 坐标仍然全部 0~1 归一化，所以密度与流量随面积自然增长。
    /// </para>
    ///
    /// <para>
    /// <b>为什么不用线段</b>：第一版用 <c>Utils.drawLine</c> 画长拖尾，结果是
    /// (a) 所有粒子共用一个流场，看起来像整条线在动、没有粒子感；
    /// (b) 线段不参与裁剪，会画出目标矩形之外（画出 exe 窗口）。
    /// 现在每个粒子一个矩形，逐轴夹进目标矩形，矩形天然不会越界。
    /// </para>
    /// </summary>
    public sealed class FlowFieldEffect
    {
        /// <summary>一个上升粒子。位置/速度用归一化坐标（Y=1 是底边，向上是 Y 减小）。</summary>
        private struct Particle
        {
            /// <summary>头部位置（归一化）。</summary>
            public Vector2 Pos;
            /// <summary>上升速度（归一化/秒，正值表示每秒上升多少）。</summary>
            public float Speed;
            /// <summary>横向漂移幅度（归一化/秒）。</summary>
            public float Sway;
            /// <summary>本粒子的相位 —— 用它错开摆动，避免所有粒子同步。</summary>
            public float Phase;
            /// <summary>剩余寿命（秒）。到 0 就回到底部重生。</summary>
            public float Life;
        }

        private readonly Particle[] _particles;
        private readonly Random _rng = new();

        /// <summary>时间累加（秒）。暂停时把 dt 传 0 就会真的停住。</summary>
        private float _time;

        // ---- 可调观感参数 ----

        /// <summary>粒子数上限（实际用 <see cref="EffectiveCount"/> 的折算结果）。</summary>
        public int Count { get; set; } = 260;

        /// <summary>
        /// 粒子数的幂律公式：<c>n = DensityCoefficient × 高度^DensityExponent</c>。
        ///
        /// <para>
        /// 高度是**目标矩形的高度（像素）**，直接取绘制区尺寸 —— 不再依赖"内容区 = 窗口高 − N"
        /// 这类版面换算，所以改版面也不会让密度整体偏一档。
        /// </para>
        ///
        /// <para>
        /// 两个系数由两组实测目标解出（见下），解出来是
        /// <c>n = 16.4341 × h^0.5052</c>：
        /// <list type="bullet">
        ///   <item>h = 155 → 210 个</item>
        ///   <item>h = 555 → 400 个</item>
        /// </list>
        /// 指数 0.505 ≈ 1/2，是个很好记的形状：**数量随高度的平方根增长**。
        /// 这比线性增长平缓得多（线性会让大窗口爆量），比恒定覆盖率（平方）平缓得不只一点。
        /// </para>
        ///
        /// <para>
        /// 想调密度就改这两个数：<c>DensityCoefficient</c> 整体乘一个倍数（全部尺寸一起变密/变稀），
        /// <c>DensityExponent</c> 调"大窗口相对小窗口的密度差"（调大 → 大窗口更多）。
        /// </para>
        /// </summary>
        public float DensityCoefficient { get; set; } = 16.4341f;
        public float DensityExponent { get; set; } = 0.5052f;

        /// <summary>折算后的实际数量下限 / 上限（防止极端尺寸下几乎没有粒子或卡顿）。</summary>
        public int MinEffectiveCount { get; set; } = 24;
        public int MaxEffectiveCount { get; set; } = 1400;

        /// <summary>
        /// 基础上升速度，单位是"满屏高 / 秒"。**实际速度由 <see cref="SpeedPxPerSecond"/> 决定**，
        /// 这个值只作为速度档位的相对比例，不参与绝对快慢。
        /// </summary>
        public float BaseSpeed { get; set; } = 0.42f;

        /// <summary>
        /// 粒子速度的两个锚点，单位是**每秒像素**。
        ///
        /// <para>
        /// 低锚点 = 已被确认"正好"的那档（严格反比方案在 Size 100 内容区 70 下的速度）；
        /// 高锚点 = "明显要更快"的那档（Size 400 内容区 370）。
        /// 两者之间按对数线性插值，之外外推。
        /// </para>
        /// </summary>
        public float SpeedLowAnchorHeight { get; set; } = 55f;
        public float SpeedLowAnchorPxPerSec { get; set; } = 218f;
        public float SpeedHighAnchorHeight { get; set; } = 370f;
        public float SpeedHighAnchorPxPerSec { get; set; } = 300f;

        /// <summary>
        /// 速度档位数。粒子速度在 <see cref="MinSpeedFactor"/> ~ <see cref="MaxSpeedFactor"/> 之间
        /// 量化成这么多档 —— 量化本身不产生"分层"观感，反而比连续随机更整齐；
        /// 真正的分层感来自快慢差距太大，所以档位多 + 范围收窄才是解。
        /// </summary>
        public int SpeedTiers { get; set; } = 8;

        /// <summary>最慢速度（相对 <see cref="BaseSpeed"/> 的倍数）。必须够快，慢粒子才会和快的一起到顶。</summary>
        public float MinSpeedFactor { get; set; } = 0.55f;

        /// <summary>最快速度（相对 <see cref="BaseSpeed"/> 的倍数）。</summary>
        public float MaxSpeedFactor { get; set; } = 1.55f;

        /// <summary>
        /// 速度分布的聚拢程度。0 = 各档位均等（快慢分得很开，容易看成"分层"）；
        /// 1 = 标准三角分布（峰值在中间档，两个极端各约占 6%）—— 默认。
        /// 实现上取两个均匀随机数的平均：它们的和是三角分布，峰值天然落在中间。
        /// </summary>
        public float SpeedClustering { get; set; } = 1f;

        /// <summary>负荷满时速度的倍数。</summary>
        public float LoadSpeedGain { get; set; } = 2.6f;

        /// <summary>拖尾长度（固定像素，不随窗口缩放）。</summary>
        public float TrailPixels { get; set; } = 16f;

        /// <summary>负荷满时拖尾的倍数。</summary>
        public float LoadTrailGain { get; set; } = 2.0f;

        /// <summary>粒子尺寸（固定像素，不随窗口缩放）。轨迹与头部用同一宽度。</summary>
        public float ParticlePixels { get; set; } = 3f;

        /// <summary>横向摆动幅度（归一化）。让上升路径不是死直线。</summary>
        public float SwayAmount { get; set; } = 0.012f;

        /// <summary>底纹网格的格数（按 min(宽,高) 分档）。</summary>
        public int GridCells { get; set; } = 10;

        /// <summary>底纹网格是否绘制。</summary>
        public bool ShowGrid { get; set; } = true;

        /// <summary>粒子亮度基准。</summary>
        public float BaseBrightness { get; set; } = 0.85f;

        /// <summary>
        /// 寿命相对"飞满一屏所需时间"的倍数。
        /// 小于 1 会让慢粒子在到顶之前消失；给 1.35 留足余量（负荷放大速度时也够）。
        /// </summary>
        public float LifeGain { get; set; } = 1.35f;

        public FlowFieldEffect()
        {
            _particles = new Particle[1024];
            Reset();
        }

        /// <summary>重新铺满粒子并复位时间。切换效果时调用，避免残留上一个效果的分布。</summary>
        public void Reset()
        {
            _time = 0f;
            for (int i = 0; i < _particles.Length; i++)
                _particles[i] = NewParticle(seedPos: true);
        }

        /// <summary>
        /// 抽一个速度：档位量化 + 三角分布（峰值在中间档，慢档比快档更多）。
        ///
        /// <para>
        /// 为什么是三角分布而不是均匀分布：均匀分布下最慢与最快各占 1/8，
        /// 且速度范围 2.8 倍 —— 屏幕上会明显看出"一撮慢的、一撮快的"，
        /// 也就是分层感。三角分布让中间档占多数、两个极端各约 6%，
        /// 慢粒子仍然存在（保留快慢层次）但不再成团。
        /// </para>
        ///
        /// <para>
        /// 慢档故意比快档多：快粒子一瞬就飞过去了，慢粒子在屏幕上停留更久、
        /// 数量感更强，所以让分布略微偏向慢侧反而更均匀。
        /// </para>
        /// </summary>
        private float RollSpeed()
        {
            int tiers = Math.Max(1, SpeedTiers);
            // 两个均匀随机取平均 → 三角分布；再用 SpeedClustering 在"均匀"与"三角"之间插值
            double u = (_rng.NextDouble() + _rng.NextDouble()) * 0.5;
            double shaped = MathHelper.Lerp((float)_rng.NextDouble(), (float)u,
                MathHelper.Clamp(SpeedClustering, 0f, 1f));
            int tier = Math.Min(tiers - 1, (int)(shaped * tiers));
            float f = MathHelper.Lerp(MinSpeedFactor, MaxSpeedFactor, tiers <= 1 ? 0.5f : (float)tier / (tiers - 1));
            return Math.Max(0.02f, BaseSpeed * f);
        }

        /// <summary>
        /// 造一个粒子。<paramref name="seedPos"/> = true 时位置与寿命随机铺开（初始化用），
        /// 否则从底边重新起飞 —— 重生陆续发生，不会整批同时刷新。
        /// </summary>
        private Particle NewParticle(bool seedPos)
        {
            float speed = RollSpeed();
            return new Particle
            {
                Pos = new Vector2((float)_rng.NextDouble(), seedPos ? (float)_rng.NextDouble() : 1f),
                Speed = speed,
                Sway = ((float)_rng.NextDouble() * 2f - 1f) * SwayAmount,
                Phase = (float)_rng.NextDouble() * MathHelper.TwoPi,
                // ⚠️ 寿命必须与速度挂钩：飞满一屏需要 1/speed 秒。固定寿命会让慢粒子
                // 中途"到期重生"、永远到不了顶（画面下半段越积越多，且看着像凭空消失）。
                // LifeGain 给足余量，夹上限避免极慢粒子永生。
                Life = MathHelper.Clamp(LifeGain / speed, 0.8f, 12f)
            };
        }

        /// <summary>
        /// 按目标高度算实际粒子数：<c>n = DensityCoefficient × 高度^DensityExponent</c>。
        ///
        /// <para>
        /// 直接取绘制区高度，不做"内容区 = 窗口高 − N"那类版面换算 —— 那种换算会把
        /// 版面改动连带成密度整体偏一档（前面几轮就吃过这个亏）。
        /// </para>
        /// </summary>
        public int EffectiveCount(int destHeight)
        {
            float h = destHeight > 0 ? destHeight : 1f;
            float n = DensityCoefficient * (float)Math.Pow(h, DensityExponent);

            return (int)MathHelper.Clamp((float)Math.Round(n), MinEffectiveCount,
                Math.Min(MaxEffectiveCount, _particles.Length));
        }

        /// <summary>
        /// 该高度下的实际速度（**每秒像素**）：过
        /// (<see cref="SpeedLowAnchorHeight"/>, <see cref="SpeedLowAnchorPxPerSec"/>)
        /// 与 (<see cref="SpeedHighAnchorHeight"/>, <see cref="SpeedHighAnchorPxPerSec"/>)
        /// 两点按**对数线性**插值/外推。
        ///
        /// <para>
        /// 为什么用"每秒像素"而不是"满屏高/秒"：后者是归一化单位，同一组参数在 70 与 600 的
        /// 内容区下每秒像素能差 8 倍，光看参数根本判断不出实际快慢 —— 前几轮调整就是被这个
        /// 单位误导的。像素/秒是能直接对着屏幕感受的量。
        /// </para>
        /// </summary>
        public float SpeedPxPerSecond(int destHeight)
        {
            float h = destHeight > 0 ? destHeight : SpeedLowAnchorHeight;
            float loH = SpeedLowAnchorHeight <= 1f ? 70f : SpeedLowAnchorHeight;
            float hiH = SpeedHighAnchorHeight <= 1f ? 370f : SpeedHighAnchorHeight;
            float loV = Math.Max(1f, SpeedLowAnchorPxPerSec);
            float hiV = Math.Max(1f, SpeedHighAnchorPxPerSec);

            float exponent = (float)(Math.Log((double)hiV / loV) / Math.Log((double)hiH / loH));
            return loV * (float)Math.Pow(h / loH, exponent);
        }

        /// <summary>
        /// 推进粒子。<paramref name="load"/> = 0~1 负荷，驱动上升速度。
        /// 传 <c>dt = 0</c> 即完全静止（供暂停使用）。
        /// </summary>
        /// <param name="dt">帧间隔（秒）。</param>
        /// <param name="load">负荷 0~1。</param>
        /// <param name="destHeight">
        /// 目标矩形高度 —— 用来折算粒子数（<see cref="EffectiveCount"/>）
        /// 与速度（<see cref="SpeedPxPerSecond"/>）。传 0 表示按锚点高度处理。
        /// </param>
        public void Update(float dt, float load, int destHeight = 0)
        {
            if (dt <= 0f) return;

            load = MathHelper.Clamp(load, 0f, 1f);
            _time += dt;

            // 实际速度由「每秒像素」定（见 SpeedPxPerSecond），粒子自身存的是归一化速度，
            // 所以这里换算：每秒像素 / 高度 = 每归一化单位要乘多少
            float speedGain = SpeedPxPerSecond(destHeight) * MathHelper.Lerp(1f, LoadSpeedGain, load)
                / Math.Max(1, destHeight);
            int count = EffectiveCount(destHeight);

            for (int i = 0; i < count; i++)
            {
                Particle p = _particles[i];

                // 横向用 sin 缓慢摆动，相位各不相同 —— 上升路径带轻微蛇形
                float sway = p.Sway * (float)Math.Sin(_time * 2.1f + p.Phase) * speedGain;

                p.Pos.X += sway * dt;
                p.Pos.Y -= p.Speed * speedGain * dt;
                p.Life -= dt;

                // 出顶 / 出侧 / 寿命尽 → 从底边重新起飞
                if (p.Pos.Y < -0.05f || p.Life <= 0f || p.Pos.X < -0.05f || p.Pos.X > 1.05f)
                {
                    p = NewParticle(seedPos: false);
                    // 重生点横向随机化，否则会形成一条固定的"起飞线"
                    p.Pos.X = (float)_rng.NextDouble();
                }

                _particles[i] = p;
            }
        }

        /// <summary>
        /// 绘制。<paramref name="dest"/> 是目标矩形，位置在这里乘上它的宽高；
        /// 但**粒子与拖尾的尺寸是固定像素**（见类注释的尺寸策略）。
        /// 每个粒子一个矩形 + 一个头部方块，逐轴夹进 dest，所以不会画出窗口。
        /// </summary>
        public void Draw(SpriteBatch sb, Rectangle dest, Color color, float load)
        {
            if (dest.Width <= 4 || dest.Height <= 4) return;

            load = MathHelper.Clamp(load, 0f, 1f);
            float speedGain = SpeedPxPerSecond(dest.Height) * MathHelper.Lerp(1f, LoadSpeedGain, load)
                / Math.Max(1, dest.Height);
            float brightness = BaseBrightness * MathHelper.Lerp(0.8f, 1.35f, load);

            // 固定像素：轨迹宽 = 头部宽，两者比例在任何窗口下都一致
            int w = Math.Max(1, (int)Math.Round(ParticlePixels));
            int trailLen = Math.Max(2, (int)Math.Round(TrailPixels * MathHelper.Lerp(1f, LoadTrailGain, load)));

            if (ShowGrid) DrawGrid(sb, dest, color, load);

            int count = EffectiveCount(dest.Height);
            for (int i = 0; i < count; i++)
            {
                Particle p = _particles[i];

                int headX = dest.X + (int)(p.Pos.X * dest.Width);
                int headY = dest.Y + (int)(p.Pos.Y * dest.Height);

                // 拖尾长度与本粒子速度成正比（快的尾巴长），再乘负荷增益
                float speedRatio = p.Speed * speedGain / Math.Max(0.02f, BaseSpeed);
                int tailLen = Math.Max(2, (int)(trailLen * MathHelper.Clamp(speedRatio, 0.5f, 2.2f)));

                // ⚠️ 不在生成处做"透明淡入"：原先 y 靠近 1 时亮度趋近 0，粒子一出生
                // 几乎看不见、要等一会儿才浮现，观感就是"凭空出现/凭空消失"。
                // 现在只在顶端做一点收尾淡出（那里本来就要重生），底部全亮。
                float fade = MathHelper.Clamp((1.05f - p.Pos.Y) * 3f, 0f, 1f);
                Color c = color * (brightness * MathHelper.Lerp(0.55f, 1f, fade));

                // 拖尾：从头部往下（来路）的一条竖线，上下端夹进 dest
                int tailTop = ClampToRange(headY, dest.Y, dest.Y + dest.Height);
                int tailBot = ClampToRange(headY + tailLen, dest.Y, dest.Y + dest.Height);
                if (tailBot > tailTop)
                {
                    int tx = ClampToRange(headX - w / 2, dest.X, dest.X + dest.Width - w);
                    sb.Draw(Utils.white, new Rectangle(tx, tailTop, w, tailBot - tailTop), c * 0.5f);
                }

                // 头部：与轨迹同宽、但更高一点的实心块 —— 让"粒子"看得出来，且比例恒定
                int hx = ClampToRange(headX - w / 2, dest.X, dest.X + dest.Width - w);
                int hy = ClampToRange(headY - w, dest.Y, dest.Y + dest.Height - w * 2);
                sb.Draw(Utils.white, new Rectangle(hx, hy, w, w * 2), c);
            }
        }

        /// <summary>底纹点阵：整体缓慢上移，与粒子同向（强化"从下往上"的方向感）。</summary>
        private void DrawGrid(SpriteBatch sb, Rectangle dest, Color color, float load)
        {
            int cells = (int)MathHelper.Clamp(GridCells, 2, 32);
            float stepY = (float)dest.Height / cells;
            float stepX = Math.Min(dest.Width, dest.Height) / (float)cells;

            // 漂移方向与粒子一致（向上），速度只有粒子的一小部分
            float drift = (_time * BaseSpeed * 0.12f * MathHelper.Lerp(1f, LoadSpeedGain, load)) % stepY;
            Color gridColor = color * (BaseBrightness * 0.11f);

            for (float row = 0f; row <= cells; row++)
            {
                int py = ClampToRange((int)(dest.Y + row * stepY - drift), dest.Y, dest.Y + dest.Height - 1);
                for (float x = 0f; x < dest.Width; x += stepX)
                {
                    int px = ClampToRange((int)(dest.X + x), dest.X, dest.X + dest.Width - 1);
                    sb.Draw(Utils.white, new Rectangle(px, py, 1, 1), gridColor);
                }
            }
        }

        private static int ClampToRange(int v, int min, int max)
            => v < min ? min : (v > max ? max : v);
    }
}
