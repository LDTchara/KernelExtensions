using System.IO;
using Hacknet;
using Hacknet.Extensions;
using Microsoft.Xna.Framework.Audio;

namespace KernelExtensions.Utility
{
    public static class SoundHelper
    {
        /// <summary>
        /// 播放扩展内指定路径的 WAV 音效文件。
        /// 路径相对于扩展根目录，且必须包含 .wav 扩展名。
        /// </summary>
        /// <param name="os">当前 OS 实例（未使用，保留备用）</param>
        /// <param name="soundPath">例如 "Sounds/Boom.wav"</param>
        public static void PlaySound(OS os, string soundPath, float volume = 0.5f, float pitch = 0.5f, float pan = 0f)
        {
            if (string.IsNullOrEmpty(soundPath)) return;

            string extensionRoot = ExtensionLoader.ActiveExtensionInfo?.FolderPath?.Replace('\\', '/');
            if (string.IsNullOrEmpty(extensionRoot))
            {
                Console.WriteLine("[KernelExtensions] SoundHelper: No extension root.");
                return;
            }

            string cleanPath = soundPath.Replace('\\', '/');
            string fullPath = Path.Combine(extensionRoot, cleanPath);

            if (!File.Exists(fullPath))
            {
                Console.WriteLine($"[KernelExtensions] SoundHelper: File not found: {fullPath}");
                return;
            }

            try
            {
                using var stream = File.OpenRead(fullPath);
                SoundEffect sound = SoundEffect.FromStream(stream);
                if (sound != null)
                {
                    // 使用三参数版本，与 CrashModule.beep 相同
                    bool success = sound.Play(volume, pitch, pan);
                    Console.WriteLine($"[KernelExtensions] SoundHelper: Play returned {success}");
                }
            }
            catch (System.Exception e)
            {
                Console.WriteLine($"[KernelExtensions] SoundHelper: Error playing sound '{fullPath}': {e.Message}");
            }
        }
    }
}