namespace KernelExtensions.Utilities
{
    /// <summary>
    /// 多边体的**呈现方式** —— 用户可见的配置选项（见 <c>PortHackV2Config</c> 的 <c>&lt;PolyhedronStyle&gt;</c>）。
    ///
    /// <para>
    /// 刻意独立成一个枚举、而不是塞进渲染序列类里：它是**面向扩展作者的选项**，
    /// 而序列类是实现细节。放在 <c>Utilities</c> 也让配置类不必反向依赖 <c>Executables</c>。
    /// </para>
    /// </summary>
    public enum KEPolyhedronStyle
    {
        /// <summary>只画线框，单层。**默认** —— 与原版 <c>PortHack</c> 观感一致。</summary>
        Wireframe = 0,

        /// <summary>只画实心面：逐面明暗（平面着色）+ 背面剔除。</summary>
        Solid = 1,

        /// <summary>实心面 + 线框叠加：低多边形观感，棱始终可见。</summary>
        SolidWire = 2,

        /// <summary>
        /// **线框 + 多层尾迹**：同一个立体画 N 遍（层数/尺寸由 Trail* 内部预设控制），
        /// 起始全层同角，spinup 中依次启动，spindown 有限收缩、不合并。
        /// 这是唯一会启用尾迹的呈现方式；普通 <see cref="Wireframe"/> 始终只画单层。
        /// </summary>
        WireframeTrail = 3
    }
}
