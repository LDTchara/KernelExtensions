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

        /// <summary>粒子数上限（实际用 <see cref="Count"/>）。</summary>
        public int Count { get; set; } = 180;

        /// <summary>
        /// 密度基准高度（像素）—— 定义"哪个窗口高度下 <see cref="Count"/> 就是正确的数量"。
        /// 默认 200：试出来的结论是 200 的窗口下 180 个粒子观感正好。
        /// </summary>
        public float ReferenceHeight { get; set; } = 200f;

        /// <summary>
        /// 数量随高度增长的指数。实际数量 = <c>Count × (高/基准高)^DensityExponent</c>。
        ///
        /// <para>
        /// <b>2.0</b> = 维持恒定覆盖率（面积是二维的，所以面积等比就是平方）。
        /// 但实测下来这个太陡：大窗口铺 1000+ 粒子显得太密，而小窗口只有 45 个又太少
        /// —— 也就是说"画面越大越能容忍低覆盖率"。默认取 <b>1.2</b>（由 1.65 → 1.4 → 1.2 逐步下调）。
        ///
        /// ⚠️ 注意覆盖率不能只按头部面积估：粒子尺寸固定时**拖尾才是视觉主体**
        /// （实测拖尾占视觉体积约 3/4，头只占 1/4），所以同一个数量看起来比"按头算"
        /// 密得多。调这个指数前先看 EffectPlayer 面板上的 n= 与 p= 两个数。
        /// </para>
        ///
        /// <para>
        /// 调这个数就等于整体调"密度随尺寸变化的曲线"：
        /// 调大 → 大窗口更多、小窗口更少；调小 → 反过来。
        /// </para>
        /// </summary>
        public float DensityExponent { get; set; } = 1.2f;

        /// <summary>折算后的实际数量下限 / 上限（防止极端尺寸下几乎没有粒子或卡顿）。</summary>
        public int MinEffectiveCount { get; set; } = 24;
        public int MaxEffectiveCount { get; set; } = 1400;

        /// <summary>基础上升速度（满屏高 / 秒）。</summary>
        public float BaseSpeed { get; set; } = 0.42f;

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
            _particles = new Particle[2048];
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
        /// 按目标高度折算实际粒子数：<c>Count × (高 / ReferenceHeight)^DensityExponent</c>，夹在上下限内。
        ///
        /// <para>
        /// 用幂律而不是线性/平方：线性折算小窗口仍偏密；平方（恒定覆盖率）大窗口又太密。
        /// 指数默认 1.65，见 <see cref="DensityExponent"/> 的说明。
        /// </para>
        ///
        /// <para>
        /// 依据目标矩形实时计算，所以窗口被拉伸/缩放时密度自动跟上。
        /// </para>
        /// </summary>
        public int EffectiveCount(int destHeight)
        {
            float refH = ReferenceHeight <= 1f ? 200f : ReferenceHeight;
            float ratio = destHeight > 0 ? destHeight / refH : 1f;
            float p = MathHelper.Clamp(DensityExponent, 0f, 4f);
            int n = (int)Math.Round(Count * Math.Pow(ratio, p));
            return (int)MathHelper.Clamp(n, MinEffectiveCount, Math.Min(MaxEffectiveCount, _particles.Length));
        }

        /// <summary>
        /// 推进粒子。传 <c>dt = 0</c> 即完全静止（供暂停使用）。
        /// </summary>
        /// <param name="dt">帧间隔（秒）。</param>
        /// <param name="load">负荷 0~1，驱动上升速度与拖尾长度。</param>
        /// <param name="destHeight">
        /// 目标矩形高度 —— 只用来折算粒子数（见 <see cref="EffectiveCount"/>）。
        /// 传 0 表示不知道尺寸，按基准高度处理。
        /// </param>
        public void Update(float dt, float load, int destHeight = 0)
        {
            if (dt <= 0f) return;

            load = MathHelper.Clamp(load, 0f, 1f);
            _time += dt;

            float speedGain = MathHelper.Lerp(1f, LoadSpeedGain, load);
            int count = EffectiveCount(destHeight);

            // 拖尾长度（归一化）：头出顶后尾巴还要滑出去，淡出要按尾巴位置算，所以这里要它。
            float trailNorm = CurrentTrailPixels(load)
                / Math.Max(1f, destHeight > 0 ? destHeight : ReferenceHeight);

            for (int i = 0; i < count; i++)
            {
                Particle p = _particles[i];

                // 横向用 sin 缓慢摆动，相位各不相同 —— 上升路径带轻微蛇形
                float sway = p.Sway * (float)Math.Sin(_time * 2.1f + p.Phase) * speedGain;

                p.Pos.X += sway * dt;
                p.Pos.Y -= p.Speed * speedGain * dt;
                p.Life -= dt;

                // ⚠️ 判据是"**头部出顶**"，不是"整条尾巴出顶"。
                // 用后者粒子会在顶部多活一大截（实测 100px 窗口下头出顶后还要飘 1.6~3.4 秒），
                // 观感就是"在顶部停了一会才消失"。头一出顶就重生，尾巴被顶边自然裁掉，
                // 看起来仍是尾巴一点点滑出去（淡出在 Draw 里按尾巴位置算）。
                float despawnY = -trailNorm * 0.5f;

                // 飞完 / 出侧 / 寿命尽 → 从底边重新起飞
                if (p.Pos.Y < despawnY || p.Life <= 0f || p.Pos.X < -0.05f || p.Pos.X > 1.05f)
                {
                    p = NewParticle(seedPos: false);
                    // 重生点横向随机化，否则会形成一条固定的"起飞线"
                    p.Pos.X = (float)_rng.NextDouble();
                }

                _particles[i] = p;
            }
        }

        /// <summary>
        /// 当前拖尾长度（像素）。抽出来是因为 <see cref="Update"/> 与 <see cref="Draw"/> 都要用它：
        /// Update 用它算重生判定与归一化拖尾长度，Draw 用它画线 ——
        /// 两边必须取同一个值，否则会出现"还没飞完就重生"或"画到一半被清掉"。
        /// </summary>
        private int CurrentTrailPixels(float load)
        {
            float px = TrailPixels * MathHelper.Lerp(1f, LoadTrailGain, MathHelper.Clamp(load, 0f, 1f));
            return Math.Max(2, (int)Math.Round(px));
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
            float speedGain = MathHelper.Lerp(1f, LoadSpeedGain, load);
            float brightness = BaseBrightness * MathHelper.Lerp(0.8f, 1.35f, load);

            // 固定像素：轨迹宽 = 头部宽，两者比例在任何窗口下都一致
            int w = Math.Max(1, (int)Math.Round(ParticlePixels));
            int trailLen = CurrentTrailPixels(load);   // 与 Update 的 despawn 判定取同一个值

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

                // ⚠️ 淡出按**尾巴位置**算，不按头部 —— 这是"看着像在顶部停了一会"的另一半原因。
                //
                // 头顶先出顶，此时尾巴还大段露在画面里；如果按头的 y 算淡出，头一出顶
                // 整条（含还在画面里的尾巴）就已经淡到很暗，剩下的时间就是"隐约的影子在顶部悬着"。
                // 改为"尾巴还在画面里就全亮、尾巴开始出顶才淡"，观感才是尾巴一点点滑出去。
                float tailBottomY = p.Pos.Y + tailLen / (float)Math.Max(1, dest.Height);   // 尾端（归一化）
                float fade = MathHelper.Clamp(tailBottomY / 0.12f, 0f, 1f);
                Color c = color * (brightness * MathHelper.Lerp(0.35f, 1f, fade));

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
