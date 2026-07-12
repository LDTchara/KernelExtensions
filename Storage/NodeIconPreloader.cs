
using System;
using System.IO;
using Hacknet;
using Hacknet.Extensions;
using Microsoft.Xna.Framework.Graphics;
using KernelExtensions.Patches;
using KernelExtensions.Utility;

namespace KernelExtensions.Storage
{
    /// <summary>
    /// 从 KE-Config.xml 的 CustomImages 预加载自定义纹理。
    /// 在 OSLoaded 时调用，确保所有 @ 前缀图标在 Action 触发前已就绪。
    /// </summary>
    internal static class NodeIconPreloader
    {
        private static bool _loaded;

        public static void Load()
        {
            if (_loaded) return;
            _loaded = true;

            try
            {
                var extInfo = ExtensionLoader.ActiveExtensionInfo;
                if (extInfo == null) return;

                int count = 0;

                // 1. 从 BepInEx CustomImagesPath 加载（;分隔的图片路径列表，建议 128x128）
                var customImages = KEConfigLoader.CustomImages;
                if (customImages.Count > 0)
                {
                    foreach (string raw in customImages)
                    {
                        string line = raw.Trim();
                        if (string.IsNullOrEmpty(line)) continue;
                        string iconKey = "@" + Path.GetFileNameWithoutExtension(line);
                        if (NodeIconRenderPatch.CustomTextures.ContainsKey(iconKey)) continue;
                        var tex = LoadFromFile(line);
                        if (tex != null) { NodeIconRenderPatch.CustomTextures[iconKey] = tex; count++; }
                    }
                }

                if (count > 0)
                    KELog.Debug($"[SetNodeIcon] preloaded {count} textures");
            }
            catch (Exception ex) { KELog.Warn($"[SetNodeIcon] preload exception: {ex.Message}"); }
        }

        internal static Texture2D LoadFromFile(string path)
        {
            try
            {
                string fullPath = Path.IsPathRooted(path) ? path
                    : Path.Combine(ExtensionLoader.ActiveExtensionInfo?.FolderPath ?? ".", path);
                if (!File.Exists(fullPath)) { KELog.Warn($"[SetNodeIcon] file not found: {fullPath}"); return null; }
                var gd = GuiData.spriteBatch?.GraphicsDevice ?? Game1.getSingleton()?.GraphicsDevice;
                if (gd == null) { KELog.Error("[SetNodeIcon] GraphicsDevice unavailable"); return null; }
                using var fs = new FileStream(fullPath, FileMode.Open, FileAccess.Read);

                return Texture2D.FromStream(gd, fs);
            }
            catch (Exception ex) { KELog.Warn($"[SetNodeIcon] load exception: {ex.Message}"); return null; }
        }
    }
}
