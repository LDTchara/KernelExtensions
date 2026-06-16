using HarmonyLib;
using Hacknet;
using Microsoft.Xna.Framework.Graphics;

namespace KernelExtensions.Patches
{
    /// <summary>
    /// Harmony Patch：拦截 DisplayModule.GetComputerImage，
    /// 当 comp.icon 以 @ 开头时，从本地的 CustomTextures 字典返回自定义纹理。
    /// 不再依赖 compAltIcons 反射，避免 os.display 为 null 的问题。
    /// </summary>
    [HarmonyPatch]
    internal static class NodeIconRenderPatch
    {
        internal static readonly Dictionary<string, Texture2D> CustomTextures = new();

        [HarmonyPostfix]
        [HarmonyPatch(typeof(DisplayModule), "GetComputerImage")]
        private static void Postfix(Computer comp, ref Texture2D __result)
        {
            if (comp?.icon != null && comp.icon.StartsWith("@"))
            {
                if (CustomTextures.TryGetValue(comp.icon, out var tex) && tex != null)
                    __result = tex;
            }
        }
    }
}
