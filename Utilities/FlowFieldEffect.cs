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
    ///   <item><b>上升粒子</b>：每个粒子从底边生成、**独立**带着自己的相位向上加速飞，
    ///     身后拖一条与速度成正比的竖线（越快的粒子尾巴越长 —— 这是"高速"最直接的线索）。
    ///     每个粒子有独立的起始相位与速度，所以看到的是一串串各自上冲，而不是整片一起漂。</item>
    ///   <item><b>底纹点阵</b>：整体向上缓慢漂移的点阵，给洪流一个"空间尺度"的参照
    ///     （没有它，纯粒子看不出背景在动）。</item>
    /// </list>
    /// </para>
    ///
    /// <para>
    /// <b>为什么不用线段</b>：本类第一版用 <c>Utils.drawLine</c> 画长拖尾，结果是
    /// (a) 所有粒子共用一个流场，看起来像整条线在动、没有粒子感；
    /// (b) 线段不参与裁剪，会画出目标矩形之外（画出 exe 窗口）。
    /// 现在改为**每个粒子一个矩形**：位置逐轴夹在目标矩形内，矩形本身天然不会越界。
    /// </para>
    ///
    /// <para>
    /// <b>缩放策略</b>：坐标全部 0~1 归一化，只在绘制时乘目标矩形宽高；
    /// 线宽/头宽按 <c>min(宽,高)</c> 的比例算。因此 100 像素高的小窗口与
    /// 600 像素的大窗口里，**粒子密度、相对速度感、线的粗细比例完全一致**。
    /// </para>
    ///
    /// <para>
    /// <b>负荷驱动</b>：<c>Load</c>（0~1）同时放大上升速度、拖尾长度与亮度 ——
    /// 负荷上升 = 数据冲得更快更亮。
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

        // ---- 可调观感参数（归一化单位或比例，不写死像素）----

        /// <summary>粒子数上限（实际用 <see cref="Count"/>）。</summary>
        public int Count { get; set; } = 260;

        /// <summary>基础上升速度（满屏高 / 秒）。负荷满时会乘上 <see cref="LoadSpeedGain"/>。</summary>
        public float BaseSpeed { get; set; } = 0.42f;

        /// <summary>上升速度的随机区间（相对 <see cref="BaseSpeed"/> 的倍数）—— 产生快慢分层。</summary>
        public float SpeedSpread { get; set; } = 1.1f;

        /// <summary>负荷满时速度的倍数。</summary>
        public float LoadSpeedGain { get; set; } = 2.6f;

        /// <summary>拖尾长度（占目标高度的比例）。</summary>
        public float TrailRatio { get; set; } = 0.16f;

        /// <summary>负荷满时拖尾的倍数。</summary>
        public float LoadTrailGain { get; set; } = 2.0f;

        /// <summary>横向摆动幅度（归一化）。让上升路径不是死直线。</summary>
        public float SwayAmount { get; set; } = 0.012f;

        /// <summary>底纹网格的格数（按 min(宽,高) 分档）。</summary>
        public int GridCells { get; set; } = 10;

        /// <summary>底纹网格是否绘制。</summary>
        public bool ShowGrid { get; set; } = true;

        /// <summary>粒子亮度基准。</summary>
        public float BaseBrightness { get; set; } = 0.85f;

        /// <summary>头部方块边长（占 min(宽,高) 的比例）。0 = 不画头。</summary>
        public float HeadRatio { get; set; } = 0.030f;

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
                _particles[i] = NewParticle(seedLife: true);
        }

        /// <summary>
        /// 造一个粒子。<paramref name="seedLife"/> = true 时位置与寿命随机铺开（初始化用），
        /// 否则从底边重新起飞 —— 这样重生是陆续发生的，不会整批同时刷新。
        /// </summary>
        private Particle NewParticle(bool seedLife)
        {
            float spread = 1f + ((float)_rng.NextDouble() * 2f - 1f) * SpeedSpread;
            float speed = Math.Max(0.05f, BaseSpeed * spread);
            return new Particle
            {
                Pos = new Vector2((float)_rng.NextDouble(), seedLife ? (float)_rng.NextDouble() : 1f),
                Speed = speed,
                Sway = ((float)_rng.NextDouble() * 2f - 1f) * SwayAmount,
                Phase = (float)_rng.NextDouble() * MathHelper.TwoPi,
                // ⚠️ 寿命必须与速度挂钩：飞满一屏需要 1/speed 秒。若给固定寿命，
                // 慢粒子会中途"到期重生"、永远到不了顶，画面下半段越积越多。
                // 乘 1.25 留余量，夹到区间里避免极慢粒子永生。
                Life = MathHelper.Clamp(1.25f / speed, 0.5f, 6f)
            };
        }

        /// <summary>
        /// 推进粒子。<paramref name="load"/> = 0~1 负荷，驱动上升速度。
        /// 传 <c>dt = 0</c> 即完全静止（供暂停使用）。
        /// </summary>
        public void Update(float dt, float load)
        {
            if (dt <= 0f) return;

            load = MathHelper.Clamp(load, 0f, 1f);
            _time += dt;

            float speedGain = MathHelper.Lerp(1f, LoadSpeedGain, load);
            int count = (int)MathHelper.Clamp(Count, 1, _particles.Length);

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
                    p = NewParticle(seedLife: false);
                    // 重生点横向随机化，否则会形成一条固定的"起飞线"
                    p.Pos.X = (float)_rng.NextDouble();
                }

                _particles[i] = p;
            }
        }

        /// <summary>
        /// 绘制。<paramref name="dest"/> 是目标矩形，所有归一化坐标在这里乘上它的宽高。
        /// 每个粒子一个矩形 + 一个头部方块，**逐轴夹在 dest 内**，所以不会画出窗口。
        /// </summary>
        public void Draw(SpriteBatch sb, Rectangle dest, Color color, float load)
        {
            if (dest.Width <= 4 || dest.Height <= 4) return;

            load = MathHelper.Clamp(load, 0f, 1f);
            float minSide = Math.Min(dest.Width, dest.Height);
            float trailPx = dest.Height * TrailRatio * MathHelper.Lerp(1f, LoadTrailGain, load);
            float speedGain = MathHelper.Lerp(1f, LoadSpeedGain, load);
            float brightness = BaseBrightness * MathHelper.Lerp(0.8f, 1.35f, load);

            // 线宽按窗口尺寸走：小窗口至少 1px，大窗口按比例变粗（保持"看起来一样粗"）
            int lineW = Math.Max(1, (int)Math.Round(minSide * 0.008f));
            int headW = Math.Max(lineW, (int)Math.Round(minSide * HeadRatio));

            if (ShowGrid) DrawGrid(sb, dest, color, minSide, load);

            int count = (int)MathHelper.Clamp(Count, 1, _particles.Length);
            for (int i = 0; i < count; i++)
            {
                Particle p = _particles[i];

                int headX = dest.X + (int)(p.Pos.X * dest.Width);
                int headY = dest.Y + (int)(p.Pos.Y * dest.Height);

                // 拖尾长度与本粒子速度成正比（快的尾巴长），再乘负荷增益
                float tailPx = trailPx * MathHelper.Clamp(p.Speed * speedGain / Math.Max(0.05f, BaseSpeed), 0.35f, 2.4f);

                // 亮度按位置淡入淡出：刚从底部出现和快到顶部时都暗一点，中段最亮
                float lifeFade = MathHelper.Clamp(p.Pos.Y * 4f, 0f, 1f) * MathHelper.Clamp((1.05f - p.Pos.Y) * 3f, 0f, 1f);
                Color c = color * (brightness * MathHelper.Lerp(0.35f, 1f, lifeFade));

                // 拖尾：从头部往下（来路）的一条竖线，上下端夹进 dest
                int tailTop = ClampToRange(headY, dest.Y, dest.Y + dest.Height);
                int tailBot = ClampToRange(headY + (int)tailPx, dest.Y, dest.Y + dest.Height);
                if (tailBot > tailTop)
                {
                    int tx = ClampToRange(headX - lineW / 2, dest.X, dest.X + dest.Width - lineW);
                    sb.Draw(Utils.white, new Rectangle(tx, tailTop, lineW, tailBot - tailTop), c * 0.55f);
                }

                // 头部：小方块，比拖尾亮 —— 让"粒子"这件事看得出来
                if (headW > 0)
                {
                    int hx = ClampToRange(headX - headW / 2, dest.X, dest.X + dest.Width - headW);
                    int hy = ClampToRange(headY - headW / 2, dest.Y, dest.Y + dest.Height - headW);
                    sb.Draw(Utils.white, new Rectangle(hx, hy, headW, headW), c);
                }
            }
        }

        /// <summary>底纹点阵：整体缓慢上移，与粒子同向（强化"从下往上"的方向感）。</summary>
        private void DrawGrid(SpriteBatch sb, Rectangle dest, Color color, float minSide, float load)
        {
            int cells = (int)MathHelper.Clamp(GridCells, 2, 32);
            float stepY = (float)dest.Height / cells;
            float stepX = minSide / (float)cells;

            // 漂移方向与粒子一致（向上），速度只有粒子的一小部分
            float drift = (_time * BaseSpeed * 0.12f * MathHelper.Lerp(1f, LoadSpeedGain, load)) % stepY;
            Color gridColor = color * (BaseBrightness * 0.11f);

            for (float row = 0f; row <= cells; row++)
            {
                // 行位置整体上移（-drift），再用 +stepY 保证顶行不出现空档
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
