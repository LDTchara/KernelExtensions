using System;
using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace KernelExtensions.Utilities
{
    /// <summary>
    /// 原版 Cube3D（Hacknet.Effects.Cube3D，internal）的反射桥。
    /// 自实现线框立方体与原版视觉存在细微差异，改为直接反射调用原版
    /// RenderWireframe（原版 Cube3D.Initilize 已在 Game1.cs:244 游戏启动时调用，
    /// 静态缓冲就绪，反射 Invoke 即与原版渲染完全一致）。
    /// 用于 PorthackHeartDaemon 默认态与心碎对齐阶段的单个立方体绘制。
    /// </summary>
    internal static class KECube3D
    {
        private static MethodInfo _render;

        private static MethodInfo RenderMethod
        {
            get
            {
                if (_render == null)
                {
                    try
                    {
                        var type = AccessTools.TypeByName("Hacknet.Effects.Cube3D");
                        _render = type?.GetMethod("RenderWireframe",
                            BindingFlags.Public | BindingFlags.Static,
                            null,
                            new[] { typeof(Vector3), typeof(float), typeof(Vector3), typeof(Color), typeof(Vector3) },
                            null);
                    }
                    catch { }
                }
                return _render;
            }
        }

        /// <summary>渲染线框立方体（相机在 +Z 20 处看向原点；gd 参数仅用于调用方签名兼容，原版自管设备）。</summary>
        public static void RenderWireframe(GraphicsDevice gd, Vector3 position, float scale, Vector3 rotation, Color color)
            => RenderWireframe(gd, position, scale, rotation, color, new Vector3(0f, 0f, 20f));

        /// <summary>反射调用原版 Cube3D.RenderWireframe(position, scale, rotation, color, cameraOffset)。</summary>
        public static void RenderWireframe(GraphicsDevice gd, Vector3 position, float scale, Vector3 rotation, Color color, Vector3 cameraOffset)
        {
            try
            {
                RenderMethod?.Invoke(null, new object[] { position, scale, rotation, color, cameraOffset });
            }
            catch
            {
                // 反射失败静默：原版 Cube3D 不可用时不渲染（不崩）
            }
        }
    }
}
