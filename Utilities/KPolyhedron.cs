using Microsoft.Xna.Framework;
using System.Text;

namespace KernelExtensions.Utilities
{
    /// <summary>柏拉图立体（五种凸正多面体）。</summary>
    public enum KPlatonicShape
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

    /// <summary>
    /// 五种柏拉图立体的顶点 / 棱 / 过顶点旋转对称轴表，供 3D 线框演出使用。
    ///
    /// 两个刻意的做法：
    ///   · 顶点用 double 精确坐标生成（黄金比 φ 用字面量），最后统一缩放到「最远顶点距离 = 1」，
    ///     调用方因此拿到的是单位半径模型，相机距离与投影可以直接写死。
    ///   · 棱表不手抄：面表由「凸包支撑平面枚举」算出来，棱再由面环的相邻顶点对去重推导。
    ///     手抄正十二 / 二十面体的面表很容易写错一个下标，而写错的后果是棱表里出现伪对角线
    ///     （棱变多）或漏棱（棱变少）—— 肉眼几乎看不出来；由几何推导 + <see cref="ValidateSelfTest"/>
    ///     自检更可靠。
    /// </summary>
    public static class KPolyhedron
    {
        /// <summary>黄金比 φ = (1 + √5) / 2 —— 正十二 / 二十面体顶点坐标的基础常量。</summary>
        private const double Phi = 1.6180339887498949;

        /// <summary>判定「顶点落在支撑平面上」的容差。坐标本身用 double 计算，误差在 1e-15 量级。</summary>
        private const double PlaneEpsilon = 1e-9;

        /// <summary>每种形状的期望值 { 顶点数, 棱数, 面数 }：既用于自检，也用于运行期兜底校验。</summary>
        private static readonly int[][] Expected =
        {
            new[] { 4, 6, 4 },      // Tetrahedron
            new[] { 8, 12, 6 },     // Cube
            new[] { 6, 12, 8 },     // Octahedron
            new[] { 20, 30, 12 },   // Dodecahedron
            new[] { 12, 30, 20 }    // Icosahedron
        };

        private sealed class ShapeData
        {
            public Vector3[] Verts;
            public (int A, int B)[] Edges;
            public int[][] Faces;
        }

        private static readonly ShapeData[] Cache = new ShapeData[Expected.Length];
        private static readonly object CacheLock = new object();

        /// <summary>
        /// 取该立体的顶点表（半径已归一：最远顶点距离 = 1）。返回副本，调用方随便改。
        /// </summary>
        /// <param name="s">立体种类；越界值退回立方体，不抛异常。</param>
        public static Vector3[] Vertices(KPlatonicShape s) => (Vector3[])Get(s).Verts.Clone();

        /// <summary>
        /// 取该立体的棱表（由面表去重推导，按 A 再 B 排序，顺序稳定）。返回副本，调用方随便改。
        /// </summary>
        /// <param name="s">立体种类；越界值退回立方体，不抛异常。</param>
        public static (int A, int B)[] Edges(KPlatonicShape s) => ((int A, int B)[])Get(s).Edges.Clone();

        /// <summary>
        /// 取该立体的面表。返回副本，调用方随便改。
        ///
        /// <para>
        /// 每个面是**顶点下标的闭合环**（含 3~5 个下标），并且<b>绕序已经统一</b>：
        /// <see cref="BuildFaces"/> 会把法线统一成外法线，<see cref="OrderRing"/> 再按绕该法线的
        /// 角度升序排环 —— 所以每个环在**模型空间**都是"从外面看逆时针"。
        /// </para>
        /// 
        /// <para>
        /// ⚠️ <b>但别据此推断屏幕空间的绕序。</b> 透视投影（<c>CreatePerspectiveFieldOfView</c> /
        /// <c>CreateLookAt</c>）会把**手性翻过来**，于是同一个环投到屏幕上变成**顺时针**。
        /// 因此用硬件剔除时必须配 <c>CullMode.CullClockwiseFace</c>（而非默认的
        /// <c>CullCounterClockwiseFace</c>，那会"保留背面、剔掉正面"—— 实机踩过）。
        /// 详见 <c>KPolyhedronSequence._faceRasterizer</c> 的注释。
        /// </para>
        /// </summary>
        /// <param name="s">立体种类；越界值退回立方体，不抛异常。</param>
        public static int[][] Faces(KPlatonicShape s)
        {
            int[][] src = Get(s).Faces;
            var copy = new int[src.Length][];
            for (int i = 0; i < src.Length; i++) copy[i] = (int[])src[i].Clone();
            return copy;
        }

        /// <summary>
        /// 该立体的旋转对称轴（已归一化）—— 取**重数最高、且方向最接近竖直**的那一根。
        ///
        /// <para>
        /// 取"最接近竖直"而不是任意一根：调用方会先用 <see cref="OrientationMatrix"/> 把它摆正，
        /// 摆正角度越小、立体的姿态越接近它在本坐标里的原始朝向，观感越自然
        /// （摆 90° 会把一个恰好正立的立体翻倒）。
        /// </para>
        ///
        /// <list type="bullet">
        ///   <item><b>正四面体</b>：3 重轴 (1,1,1)/√3 —— 顶点连到对面那个面的中心，
        ///     也就是"顶点向底面作的垂线"。与竖直夹角 54.7°（最竖直的 3 重轴）。</item>
        ///   <item><b>立方体</b>：4 重轴 (0,1,0) —— **本来就竖直**，摆正矩阵是单位矩阵。</item>
        ///   <item><b>正八面体</b>：4 重轴 (0,1,0) —— 同样本来就竖直。</item>
        ///   <item><b>正十二面体</b>：3 重轴 (1/φ, φ, 0) —— 过两个相对顶点，与竖直夹角 **20.9°**
        ///     （最竖直的一根）。⚠️ Y 轴对它只是 **2 重轴**（过对棱中点），所以早先给 UnitY 是
        ///     "转得动但不够正"，观感上就是那点细微差别；另一根 3 重轴 (0,1,φ) 夹角 58.3°，更歪。</item>
        ///   <item><b>正二十面体</b>：5 重轴 (1, φ, 0) —— 与竖直夹角 **31.7°**，即上下两个相对顶点
        ///     连成的线、两端在旋转中固定不动。⚠️ 另一根 5 重轴 (0,1,φ) 夹角达 58.3°，歪近一倍；
        ///     这两个方向恰好体现十二面体/二十面体互为对偶。</item>
        /// </list>
        ///
        /// <para>
        /// 上表的重数与夹角是用脚本暴力验证过的：枚举每个几何候选轴（顶点对连线、顶点方向、
        /// 棱中点方向），检查"把顶点集合绕它转 2π/n 是否映射回自身"，再取重数最高且最竖直的。
        /// 每种的完整轴表：四面体 4 根 3 重轴；立方体/八面体 3 根 4 重轴；
        /// 十二面体 10 根 3 重轴 + 15 根 2 重轴；二十面体 6 根 5 重轴。
        /// </para>
        /// </summary>
        /// <param name="s">立体种类；越界值退回立方体的对称轴。</param>
        public static Vector3 SpinAxis(KPlatonicShape s)
        {
            switch (Index(s))
            {
                case (int)KPlatonicShape.Tetrahedron:
                    // 顶点向对底面中心作的垂线 = 体对角线方向（3 重轴）
                    return Vector3.Normalize(new Vector3(1f, 1f, 1f));
                case (int)KPlatonicShape.Dodecahedron:
                    // 最竖直的 3 重轴 —— 过两个相对顶点（(1/φ, φ, 0) 正是它的顶点向量之一）。
                    // 与竖直夹角 20.9°（比 (0,1,φ) 的 58.3° 正得多；Y 轴对它只是 2 重轴）
                    return Vector3.Normalize(new Vector3(1f / (float)Phi, (float)Phi, 0f));
                case (int)KPlatonicShape.Icosahedron:
                    // 最竖直的 5 重轴 —— 上下两个相对顶点连成的线，两端在旋转中固定不动。
                    // 与竖直夹角 31.7°（另一根 5 重轴 (0,1,φ) 是 58.3°，歪近一倍）
                    return Vector3.Normalize(new Vector3(1f, (float)Phi, 0f));
                default:
                    // 立方体 / 八面体：它们的 4 重轴恰好就是 Y 轴，无需摆正
                    return Vector3.UnitY;
            }
        }

        /// <summary>
        /// 把 <paramref name="axis"/> 转到竖直的**最小旋转**矩阵（纯旋转，无缩放无镜像）。
        ///
        /// <para>
        /// 用法：<c>World = Scale * OrientationMatrix(axis) * Spin(angle)</c> ——
        /// 摆正在内、自转在外，于是自转始终绕世界竖直轴，而该立体自己的对称轴正好与它重合。
        /// </para>
        ///
        /// <para>
        /// ⚠️ 必须用**最小旋转**（按两轴夹角转过去），不能用"取 axis 当局部 Y、再随便挑一根 X"
        /// 那种构造 —— 后者会在轴上叠加一个任意滚转角，把立体转到一个陌生的姿态，
        /// 观感上就是"形状不对了"。最小旋转保证立体只是被"扶正"，其余朝向尽量不变。
        /// </para>
        ///
        /// <para>
        /// 用 Rodrigues 公式：<c>R = I + [k]×sinθ + [k]×²(1-cosθ)</c>，
        /// 其中 <c>k = a×b/|a×b|</c>、<c>θ = angle(a,b)</c>。轴已竖直时 a×b=0，
        /// 直接返回单位矩阵；两轴恰好反向时退化，做一次取反（保底，正常不会走到）。
        /// </para>
        /// </summary>
        /// <param name="axis">要摆到竖直的那根轴；零向量返回单位矩阵。</param>
        public static Matrix OrientationMatrix(Vector3 axis)
        {
            if (axis.LengthSquared() < 1e-12f) return Matrix.Identity;

            Vector3 a = Vector3.Normalize(axis);
            Vector3 b = Vector3.UnitY;

            Vector3 c = Vector3.Cross(a, b);
            float sinT = c.Length();
            if (sinT < 1e-6f)
            {
                // 已经竖直（a≈b）；或恰好反向 —— 后者罕见，做一次取反保底
                return Vector3.Dot(a, b) > 0f ? Matrix.Identity : Matrix.CreateScale(-1f, 1f, 1f);
            }

            Vector3 k = c / sinT;                     // 旋转轴（单位）
            float cosT = MathHelper.Clamp(Vector3.Dot(a, b), -1f, 1f);
            float x = k.X, y = k.Y, z = k.Z;
            float oneMinusCos = 1f - cosT;

            // 行优先：I + [k]× sinθ + [k]×²(1-cosθ)
            return new Matrix(
                cosT + x * x * oneMinusCos,
                x * y * oneMinusCos - z * sinT,
                x * z * oneMinusCos + y * sinT,
                0f,

                y * x * oneMinusCos + z * sinT,
                cosT + y * y * oneMinusCos,
                y * z * oneMinusCos - x * sinT,
                0f,

                z * x * oneMinusCos - y * sinT,
                z * y * oneMinusCos + x * sinT,
                cosT + z * z * oneMinusCos,
                0f,

                0f, 0f, 0f, 1f);
        }

        /// <summary>
        /// 非实机自检：核对五种形状的 v / e / f 计数、棱表（无重复、无自环、下标不越界、
        /// 每条棱恰好属于 2 个面）、半径归一（最远顶点距离 = 1）、棱长一致与旋转轴对称性
        /// （单位向量且过某个顶点）。返回英文文本（半角 ASCII），便于脚本 / 终端直接核对。
        /// </summary>
        public static string ValidateSelfTest()
        {
            var sb = new StringBuilder();
            sb.AppendLine("KPolyhedron self-test (v = vertices, e = edges, f = faces)");

            int passed = 0;
            int total = 0;
            foreach (KPlatonicShape s in (KPlatonicShape[])Enum.GetValues(typeof(KPlatonicShape)))
            {
                total++;
                int[] want = Expected[Index(s)];
                ShapeData d = Get(s);

                // ---- 棱表：重复 / 自环 / 越界 ----
                int duplicate = 0;
                int selfLoop = 0;
                int outOfRange = 0;
                var unique = new HashSet<(int A, int B)>();
                foreach ((int A, int B) e in d.Edges)
                {
                    if (e.A == e.B) selfLoop++;
                    if (e.A < 0 || e.B < 0 || e.A >= d.Verts.Length || e.B >= d.Verts.Length) outOfRange++;
                    if (!unique.Add(e)) duplicate++;
                }

                // ---- 每条棱恰好被 2 个面共用（面表写错时这里会亮） ----
                var incidence = new Dictionary<(int A, int B), int>();
                foreach (int[] face in d.Faces)
                {
                    for (int i = 0; i < face.Length; i++)
                    {
                        int a = face[i];
                        int b = face[(i + 1) % face.Length];
                        if (a == b) continue;
                        (int A, int B) e = a < b ? (a, b) : (b, a);
                        incidence.TryGetValue(e, out int c);
                        incidence[e] = c + 1;
                    }
                }
                int badIncidence = 0;
                foreach ((int A, int B) e in d.Edges)
                {
                    if (!incidence.TryGetValue(e, out int c) || c != 2) badIncidence++;
                }

                // ---- 半径归一 + 棱长一致 ----
                double maxRadius = 0.0;
                foreach (Vector3 v in d.Verts) maxRadius = Math.Max(maxRadius, v.Length());
                double minEdge = double.MaxValue;
                double maxEdge = 0.0;
                foreach ((int A, int B) e in d.Edges)
                {
                    double len = (d.Verts[e.A] - d.Verts[e.B]).Length();
                    minEdge = Math.Min(minEdge, len);
                    maxEdge = Math.Max(maxEdge, len);
                }
                if (d.Edges.Length == 0) { minEdge = 0.0; maxEdge = 0.0; }

                // ---- 旋转轴：单位长度，且确实过某个顶点 ----
                Vector3 axis = SpinAxis(s);
                bool axisUnit = Math.Abs(axis.Length() - 1f) < 1e-4f;
                bool axisOnVertex = false;
                foreach (Vector3 v in d.Verts)
                {
                    if (Math.Abs(Vector3.Dot(axis, v)) > 1f - 1e-4f) { axisOnVertex = true; break; }
                }

                int euler = d.Verts.Length - d.Edges.Length + d.Faces.Length;
                bool radiusOk = Math.Abs(maxRadius - 1.0) < 1e-5;
                bool edgeEqual = maxEdge - minEdge < 1e-5;
                bool ok = d.Verts.Length == want[0] && d.Edges.Length == want[1] && d.Faces.Length == want[2]
                          && euler == 2 && duplicate == 0 && selfLoop == 0 && outOfRange == 0 && badIncidence == 0
                          && radiusOk && edgeEqual && axisUnit && axisOnVertex;
                if (ok) passed++;

                // 数值以「放大十万倍后的整数」输出：避免小数点在不同区域设置下变成逗号，脚本好解析
                sb.AppendLine($"{s}: v={d.Verts.Length} e={d.Edges.Length} f={d.Faces.Length}"
                    + $" want={want[0]}/{want[1]}/{want[2]} euler={euler}"
                    + $" dup={duplicate} loop={selfLoop} oob={outOfRange} badEdgeFaces={badIncidence}"
                    + $" radiusX1e5={(int)Math.Round(maxRadius * 100000.0)}"
                    + $" edgeSpreadX1e6={(int)Math.Round((maxEdge - minEdge) * 1000000.0)}"
                    + $" axisUnit={axisUnit} axisVertex={axisOnVertex} => {(ok ? "PASS" : "FAIL")}");
            }

            sb.AppendLine($"Summary: {passed}/{total} shapes PASS");
            return sb.ToString();
        }

        // ======================= 内部：几何推导 =======================

        private static ShapeData Get(KPlatonicShape s)
        {
            int i = Index(s);
            lock (CacheLock)
            {
                ShapeData d = Cache[i];
                if (d == null)
                {
                    d = Build(s);
                    Cache[i] = d;
                }
                return d;
            }
        }

        /// <summary>越界的枚举值（外部传进来的脏数据）退回立方体，避免抛异常。</summary>
        private static int Index(KPlatonicShape s)
        {
            int i = (int)s;
            return i >= 0 && i < Expected.Length ? i : (int)KPlatonicShape.Cube;
        }

        private static ShapeData Build(KPlatonicShape s)
        {
            double[][] raw = RawVertices(s);

            // 半径归一：最远顶点距离 = 1
            double max = 0.0;
            foreach (double[] v in raw) max = Math.Max(max, Math.Sqrt(v[0] * v[0] + v[1] * v[1] + v[2] * v[2]));
            double scale = max > 0.0 ? 1.0 / max : 1.0;

            var verts = new Vector3[raw.Length];
            var exact = new double[raw.Length][];
            for (int i = 0; i < raw.Length; i++)
            {
                exact[i] = new[] { raw[i][0] * scale, raw[i][1] * scale, raw[i][2] * scale };
                verts[i] = new Vector3((float)exact[i][0], (float)exact[i][1], (float)exact[i][2]);
            }

            int[][] faces = BuildFaces(exact);
            var data = new ShapeData { Verts = verts, Faces = faces, Edges = BuildEdges(faces) };

            int[] want = Expected[Index(s)];
            if (verts.Length != want[0] || data.Edges.Length != want[1] || faces.Length != want[2])
            {
                KELog.Error($"[KPolyhedron] {s} geometry mismatch: v={verts.Length} e={data.Edges.Length}"
                    + $" f={faces.Length}, expected v={want[0]} e={want[1]} f={want[2]}.");
            }
            return data;
        }

        /// <summary>各立体的原始顶点坐标（未归一）。</summary>
        private static double[][] RawVertices(KPlatonicShape s)
        {
            var list = new List<double[]>();
            switch (Index(s))
            {
                case (int)KPlatonicShape.Tetrahedron:
                    // 立方体 8 个顶点里「符号积为正」的 4 个，恰好构成一组正四面体
                    foreach (double[] p in SignVariants(1, 1, 1))
                    {
                        if (p[0] * p[1] * p[2] > 0.0) list.Add(p);
                    }
                    break;

                case (int)KPlatonicShape.Cube:
                    list.AddRange(SignVariants(1, 1, 1));
                    break;

                case (int)KPlatonicShape.Octahedron:
                    list.AddRange(SignVariants(1, 0, 0));
                    list.AddRange(SignVariants(0, 1, 0));
                    list.AddRange(SignVariants(0, 0, 1));
                    break;

                case (int)KPlatonicShape.Dodecahedron:
                    list.AddRange(SignVariants(1, 1, 1));
                    list.AddRange(SignVariants(0, 1 / Phi, Phi));
                    list.AddRange(SignVariants(1 / Phi, Phi, 0));
                    list.AddRange(SignVariants(Phi, 0, 1 / Phi));
                    break;

                default: // Icosahedron
                    list.AddRange(SignVariants(0, 1, Phi));
                    list.AddRange(SignVariants(1, Phi, 0));
                    list.AddRange(SignVariants(Phi, 0, 1));
                    break;
            }
            return list.ToArray();
        }

        /// <summary>枚举坐标分量的独立符号翻转（0 分量不翻，否则会出现重复顶点）。</summary>
        private static IEnumerable<double[]> SignVariants(double x, double y, double z)
        {
            int nx = x == 0.0 ? 1 : 2;
            int ny = y == 0.0 ? 1 : 2;
            int nz = z == 0.0 ? 1 : 2;
            for (int ix = 0; ix < nx; ix++)
            {
                for (int iy = 0; iy < ny; iy++)
                {
                    for (int iz = 0; iz < nz; iz++)
                    {
                        yield return new[] { ix == 0 ? x : -x, iy == 0 ? y : -y, iz == 0 ? z : -z };
                    }
                }
            }
        }

        /// <summary>
        /// 由顶点集求面表：任取 3 个顶点定一个平面，若其余顶点全在该平面同侧，该平面就是凸包的一个
        /// 支撑面；落在平面上的全部顶点恰好是该面的顶点（凸位置），按绕法线的角度排成环即可。
        /// 同一个面会被若干组三点重复找到，用顶点下标集合去重。
        /// </summary>
        private static int[][] BuildFaces(double[][] verts)
        {
            var faces = new List<int[]>();
            var seen = new HashSet<long>();
            int n = verts.Length;

            for (int i = 0; i < n; i++)
            {
                for (int j = i + 1; j < n; j++)
                {
                    for (int k = j + 1; k < n; k++)
                    {
                        double[] normal = Cross(Sub(verts[j], verts[i]), Sub(verts[k], verts[i]));
                        double len = Length(normal);
                        if (len < 1e-12) continue; // 三点共线，定不出平面
                        for (int c = 0; c < 3; c++) normal[c] /= len;

                        // 三点顺序（i&lt;j&lt;k）决定的叉积朝向是朝外还是朝里并不固定，
                        // 所以「顶点全在同侧」的两种情况都要接受：否则纯三角形面恰好有一半
                        // 会因为朝向相反被整片漏掉（四边形 / 五边形面有多个三点组合，总能撞上
                        // 正确朝向，所以只有三角形面会成片丢失 —— 正四面体、正八面体、正二十面体）。
                        double plane = Dot(normal, verts[i]);
                        double maxProjection = double.NegativeInfinity;
                        double minProjection = double.PositiveInfinity;
                        for (int m = 0; m < n; m++)
                        {
                            double p = Dot(normal, verts[m]);
                            maxProjection = Math.Max(maxProjection, p);
                            minProjection = Math.Min(minProjection, p);
                        }
                        bool supportsAtTop = maxProjection - plane <= PlaneEpsilon;
                        bool supportsAtBottom = plane - minProjection <= PlaneEpsilon;
                        if (!supportsAtTop && !supportsAtBottom) continue; // 顶点落在平面两侧：不是支撑面

                        // 统一转成外法线，后面的共面判定与排环都用它
                        if (!supportsAtTop)
                        {
                            for (int c = 0; c < 3; c++) normal[c] = -normal[c];
                            plane = -plane;
                        }

                        var onPlane = new List<int>();
                        for (int m = 0; m < n; m++)
                        {
                            if (Math.Abs(Dot(normal, verts[m]) - plane) <= PlaneEpsilon) onPlane.Add(m);
                        }
                        if (onPlane.Count < 3) continue; // 只挨到一个顶点 / 一条棱的支撑平面，不是面

                        var sorted = new List<int>(onPlane);
                        sorted.Sort();
                        long key = sorted.Count; // 顶点数 ≤ 20，用「长度 + 32 进制下标」做无歧义的集合键
                        foreach (int m in sorted) key = key * 32 + m;
                        if (!seen.Add(key)) continue;

                        faces.Add(OrderRing(verts, onPlane, normal));
                    }
                }
            }
            return faces.ToArray();
        }

        /// <summary>把同一个面上的顶点按绕面法线的角度排成不自交的环。</summary>
        private static int[] OrderRing(double[][] verts, List<int> onPlane, double[] normal)
        {
            var center = new double[3];
            foreach (int m in onPlane)
            {
                for (int c = 0; c < 3; c++) center[c] += verts[m][c];
            }
            for (int c = 0; c < 3; c++) center[c] /= onPlane.Count;

            double[] u = Normalize(Sub(verts[onPlane[0]], center));
            double[] w = Cross(normal, u);

            var byAngle = new List<(int Index, double Angle)>(onPlane.Count);
            foreach (int m in onPlane)
            {
                double[] p = Sub(verts[m], center);
                byAngle.Add((m, Math.Atan2(Dot(p, w), Dot(p, u))));
            }
            byAngle.Sort((a, b) => a.Angle.CompareTo(b.Angle));

            var ring = new int[byAngle.Count];
            for (int i = 0; i < ring.Length; i++) ring[i] = byAngle[i].Index;
            return ring;
        }

        /// <summary>棱表 = 面环的相邻顶点对，去重后按 A 再 B 排序（顺序稳定，便于比对）。</summary>
        private static (int A, int B)[] BuildEdges(int[][] faces)
        {
            var seen = new HashSet<(int A, int B)>();
            var edges = new List<(int A, int B)>();
            foreach (int[] face in faces)
            {
                for (int i = 0; i < face.Length; i++)
                {
                    int a = face[i];
                    int b = face[(i + 1) % face.Length];
                    if (a == b) continue; // 面环顶点互异，理论上不会出现
                    (int A, int B) e = a < b ? (a, b) : (b, a);
                    if (seen.Add(e)) edges.Add(e);
                }
            }
            edges.Sort((p, q) => p.A != q.A ? p.A.CompareTo(q.A) : p.B.CompareTo(q.B));
            return edges.ToArray();
        }

        private static double[] Sub(double[] a, double[] b) => new[] { a[0] - b[0], a[1] - b[1], a[2] - b[2] };

        private static double[] Cross(double[] a, double[] b) => new[]
        {
            a[1] * b[2] - a[2] * b[1],
            a[2] * b[0] - a[0] * b[2],
            a[0] * b[1] - a[1] * b[0]
        };

        private static double Dot(double[] a, double[] b) => a[0] * b[0] + a[1] * b[1] + a[2] * b[2];

        private static double Length(double[] a) => Math.Sqrt(Dot(a, a));

        private static double[] Normalize(double[] a)
        {
            double len = Length(a);
            if (len <= 1e-12) return new[] { 1.0, 0.0, 0.0 };
            return new[] { a[0] / len, a[1] / len, a[2] / len };
        }
    }
}
