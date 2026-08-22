using System;
using Hacknet;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace KernelExtensions.Utility
{
    /// <summary>
    /// 复刻原版 Hacknet.Effects.Cube3D（internal，KE 无法直接访问）——9.47。
    /// 3D 线框立方体（36 顶点 12 三角形）渲染，BasicEffect WireFrame。
    /// 惰性初始化：首次渲染前检查 GraphicsDevice，窗口重建（gd 变化）自动重建缓冲。
    /// 用于 PorthackHeartDaemon 默认态与心碎对齐阶段的单个立方体绘制；
    /// 心形序列复用原版 PortHackCubeSequence（public，内部走原版 Cube3D）。
    /// </summary>
    internal static class KECube3D
    {
        private const int NUM_VERTICES = 36;

        private static VertexPositionNormalTexture[] verts;
        private static VertexBuffer vBuffer;
        private static IndexBuffer ib;
        private static BasicEffect wireframeEffect;
        private static RasterizerState wireframeRaster;
        private static GraphicsDevice _lastGd;

        private static void EnsureInit(GraphicsDevice gd)
        {
            if (wireframeEffect != null && ReferenceEquals(_lastGd, gd)) return;

            ConstructCube();
            vBuffer = new VertexBuffer(gd, VertexPositionNormalTexture.VertexDeclaration, NUM_VERTICES, BufferUsage.WriteOnly);
            vBuffer.SetData(verts);
            ib = new IndexBuffer(gd, IndexElementSize.SixteenBits, 14, BufferUsage.WriteOnly);
            ib.SetData(new short[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13 });
            wireframeRaster = new RasterizerState { FillMode = FillMode.WireFrame, CullMode = CullMode.None };
            wireframeEffect = new BasicEffect(gd)
            {
                Projection = Matrix.CreatePerspectiveFieldOfView(MathHelper.PiOver4, gd.Viewport.AspectRatio, 0.01f, 3000f)
            };
            _lastGd = gd;
        }

        /// <summary>渲染线框立方体（带相机偏移重载的简版，相机在 +Z 20 处看向原点）。</summary>
        public static void RenderWireframe(GraphicsDevice gd, Vector3 position, float scale, Vector3 rotation, Color color)
            => RenderWireframe(gd, position, scale, rotation, color, new Vector3(0f, 0f, 20f));

        /// <summary>渲染线框立方体（对齐原版 Cube3D.RenderWireframe）。gd 用于惰性初始化。调用方须在 draw 主线程。</summary>
        public static void RenderWireframe(GraphicsDevice gd, Vector3 position, float scale, Vector3 rotation, Color color, Vector3 cameraOffset)
        {
            if (gd == null) return;
            EnsureInit(gd);

            scale = Math.Max(0.001f, scale);
            wireframeEffect.DiffuseColor = Utils.ColorToVec3(color);
            gd.BlendState = BlendState.Opaque;
            gd.DepthStencilState = DepthStencilState.Default;
            gd.SamplerStates[0] = SamplerState.LinearClamp;
            gd.SetVertexBuffer(vBuffer);
            gd.Indices = ib;

            RasterizerState prevRaster = gd.RasterizerState;
            gd.RasterizerState = wireframeRaster;

            Matrix world = Matrix.CreateTranslation(Vector3.Zero)
                * Matrix.CreateScale(scale)
                * Matrix.CreateRotationY(rotation.Y)
                * Matrix.CreateRotationX(rotation.X)
                * Matrix.CreateRotationZ(rotation.Z)
                * Matrix.CreateTranslation(position);
            wireframeEffect.World = world;
            wireframeEffect.View = Matrix.CreateLookAt(cameraOffset, position, Vector3.Up);

            try
            {
                foreach (EffectPass pass in wireframeEffect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    gd.DrawPrimitives(PrimitiveType.TriangleList, 0, NUM_VERTICES);
                }
            }
            catch (NotSupportedException)
            {
                // 对齐原版：某些驱动/状态下线框绘制不支持，静默
            }
            gd.RasterizerState = prevRaster;
        }

        private static void ConstructCube()
        {
            verts = new VertexPositionNormalTexture[NUM_VERTICES];
            var p1 = new Vector3(-1f, 1f, -1f);
            var p2 = new Vector3(-1f, 1f, 1f);
            var p3 = new Vector3(1f, 1f, -1f);
            var p4 = new Vector3(1f, 1f, 1f);
            var p5 = new Vector3(-1f, -1f, -1f);
            var p6 = new Vector3(-1f, -1f, 1f);
            var p7 = new Vector3(1f, -1f, -1f);
            var p8 = new Vector3(1f, -1f, 1f);
            var nZ = new Vector3(0f, 0f, 1f);
            var nNZ = new Vector3(0f, 0f, -1f);
            var nY = new Vector3(0f, 1f, 0f);
            var nNY = new Vector3(0f, -1f, 0f);
            var nX = new Vector3(-1f, 0f, 0f);
            var nNX = new Vector3(1f, 0f, 0f);
            var uvA = new Vector2(1f, 0f);
            var uvB = new Vector2(0f, 0f);
            var uvC = new Vector2(1f, 1f);
            var uvD = new Vector2(0f, 1f);

            Set(0, p1, nZ, uvA);   Set(1, p5, nZ, uvC);   Set(2, p3, nZ, uvB);
            Set(3, p5, nZ, uvC);   Set(4, p7, nZ, uvD);   Set(5, p3, nZ, uvB);
            Set(6, p2, nNZ, uvB);  Set(7, p4, nNZ, uvA);  Set(8, p6, nNZ, uvD);
            Set(9, p6, nNZ, uvD);  Set(10, p4, nNZ, uvA); Set(11, p8, nNZ, uvC);
            Set(12, p1, nY, uvC);  Set(13, p4, nY, uvB);  Set(14, p2, nY, uvA);
            Set(15, p1, nY, uvC);  Set(16, p3, nY, uvD);  Set(17, p4, nY, uvB);
            Set(18, p5, nNY, uvA); Set(19, p6, nNY, uvC); Set(20, p8, nNY, uvD);
            Set(21, p5, nNY, uvA); Set(22, p8, nNY, uvD); Set(23, p7, nNY, uvB);
            Set(24, p1, nX, uvB);  Set(25, p6, nX, uvC);  Set(26, p5, nX, uvD);
            Set(27, p2, nX, uvA);  Set(28, p6, nX, uvC);  Set(29, p1, nX, uvB);
            Set(30, p3, nNX, uvA); Set(31, p7, nNX, uvC); Set(32, p8, nNX, uvD);
            Set(33, p4, nNX, uvB); Set(34, p3, nNX, uvA); Set(35, p8, nNX, uvD);
        }

        private static void Set(int index, Vector3 pos, Vector3 normal, Vector2 uv)
        {
            verts[index] = new VertexPositionNormalTexture(pos, normal, uv);
        }
    }
}
