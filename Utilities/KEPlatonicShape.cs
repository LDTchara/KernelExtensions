namespace KernelExtensions.Utilities
{
    /// <summary>柏拉图立体（五种凸正多面体）。</summary>
    public enum KEPlatonicShape
    {
        /// <summary>正四面体：4 顶点 / 6 棱 / 4 个三角形面。</summary>
        Tetrahedron = 0,

        /// <summary>正六面体（立方体）：8 顶点 / 12 棱 / 6 个正方形面。</summary>
        Cube = 1,

        /// <summary>正八面体：6 顶点 / 12 棱 / 8 个三角形面。</summary>
        Octahedron = 2,

        /// <summary>正十二面体：20 顶点 / 30 棱 / 12 个正五边形面。</summary>
        Dodecahedron = 3,

        /// <summary>正二十面体：12 顶点 / 30 棱 / 20 个三角形面。</summary>
        Icosahedron = 4
    }

}
