namespace KernelExtensions.Utilities
{
    /// <summary>
    /// 多边体**动画层**的默认值。渲染与配置两边都向下引用这里，避免任何一方持有对方的默认值。
    ///
    /// <para>
    /// 为什么要单独一个类：这些常量原先放在 <c>Configs.PortHackV2Config</c> 里，于是
    /// <see cref="KPolyhedron"/> 的渲染/动画消费方（<c>KPolyhedronSequence</c>）**反过来依赖某个
    /// 可执行程序的配置类** —— 而那套渲染其实已经不只服务 PortHackV2（效果播放器也用它预览立体）。
    /// 挪到这里之后，依赖方向是：配置层 → 本类 ← 渲染层。
    /// </para>
    ///
    /// <para>
    /// ⚠️ 这些是**动画层的基准值**，不是"某个配置项的默认值"。尤其
    /// <see cref="DefaultSpinSpeed"/> —— 起转角速度与急刹后的慢转角速度按**固定比例**跟着它缩放
    /// （见 <c>KPolyhedronSequence.OMEGA0_RATIO</c> / <c>OMEGA_IDLE_RATIO</c>，源码里写成
    /// <c>0.3f / DefaultSpinSpeed</c>），所以改它会连带改变整条"起转慢 → 匀速快 → 急刹回慢"曲线。
    /// </para>
    /// </summary>
    public static class KPolyhedronAnimation
    {
        /// <summary>
        /// 匀速（Cracking 期间）的默认目标角速度，单位**弧度/秒**。3.2 ≈ 183°/秒。
        ///
        /// <para>
        /// ⚠️ 数值越高，60fps 下单帧转角越大：每帧约 <c>SpinSpeed × 0.955</c> 度 ——
        /// 3.2 → 约 3.1°（稳）；6.4 → 约 6.1°（已能看出跳）；12 → 约 11.5°（明显跳帧）。
        /// 低于 20fps 时倍数更大。
        /// </para>
        /// </summary>
        public const float DefaultSpinSpeed = 3.2f;

        /// <summary>
        /// 急刹结束后**慢转**的默认角速度。<c>&lt; 0</c> = 按 <see cref="DefaultSpinSpeed"/> 等比缩放
        /// （3.2 时即 0.5 弧度/秒）。
        /// </summary>
        public const float DefaultIdleSpinSpeed = -1f;

        /// <summary>起转时长占总时长的默认比例（仅当作者没显式给起转秒数时生效）。</summary>
        public const float DefaultSpinUpRatio = 0.1f;

        /// <summary>急刹的默认时长（秒）。</summary>
        public const float DefaultSpinDownSeconds = 1.7f;
    }
}
