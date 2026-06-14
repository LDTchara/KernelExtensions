using Hacknet;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Media;
using System.Diagnostics;
using System.IO;
using System.Globalization;

namespace KernelExtensions.Modules
{
    /// <summary>
    /// 视频壁纸模块。用 Stopwatch 按可调速率取帧，
    /// 速度乘数保存在视频旁的同名 .speed 文件中。
    /// </summary>
    public class VideoWallpaperModule : Module
    {
        private Video _video;
        private VideoPlayer _videoPlayer;
        private bool _isPlaying;
        private bool _updatesEnabled = true;

        private Stopwatch _clock = Stopwatch.StartNew();
        private Texture2D _currentFrame;
        private float _speed = 1f;
        private string _speedFilePath;

        private long _nextTick;
        private long FrameTicks => (long)(Stopwatch.Frequency / (60f * _speed));

        private static VideoWallpaperModule _instance;
        public static VideoWallpaperModule Instance => _instance;
        public float Speed => _speed;

        public VideoWallpaperModule(Rectangle location, OS operatingSystem)
            : base(location, operatingSystem)
        {
            name = "VideoWallpaper";
            _instance = this;
            int topBar = OS.TOP_BAR_HEIGHT;
            bounds = new Rectangle(
                -100, topBar,
                os.ScreenManager.GraphicsDevice.Viewport.Width + 200,
                os.ScreenManager.GraphicsDevice.Viewport.Height - topBar + 100);
        }

        public bool LoadAndPlay(string contentDir, string videoFileName)
        {
            try
            {
                _speedFilePath = Path.Combine(
                    Game1.singleton.Content.RootDirectory, contentDir, videoFileName + ".speed");
                LoadSpeed();

                _video = Game1.singleton.Content.Load<Video>(Path.Combine(contentDir, videoFileName));
                _videoPlayer = new VideoPlayer();
                _videoPlayer.IsLooped = true;
                _videoPlayer.Play(_video);
                _isPlaying = true;
                _clock.Restart();
                _nextTick = _clock.Elapsed.Ticks + FrameTicks;
                os.write($"[VideoWallpaper] speed={_speed:F1}");
                return true;
            }
            catch
            {
                os.write("[VideoWallpaper] ERROR loading video.");
                return false;
            }
        }

        private void LoadSpeed()
        {
            try
            {
                if (File.Exists(_speedFilePath))
                {
                    string txt = File.ReadAllText(_speedFilePath).Trim();
                    if (float.TryParse(txt, NumberStyles.Float, CultureInfo.InvariantCulture, out float v))
                        _speed = v;
                }
            }
            catch { }
        }

        public void SaveSpeed()
        {
            try
            {
                File.WriteAllText(_speedFilePath, _speed.ToString("F2", CultureInfo.InvariantCulture));
            }
            catch { }
        }

        public void AdjustSpeed(float delta)
        {
            _speed = MathHelper.Clamp(_speed + delta, 0.1f, 5f);
            SaveSpeed();
            _nextTick = _clock.Elapsed.Ticks; // 立即生效
        }

        public override void Update(float t)
        {
            if (!_updatesEnabled || _videoPlayer == null || !_isPlaying)
                return;

            long now = _clock.Elapsed.Ticks;
            if (now >= _nextTick)
            {
                _currentFrame = _videoPlayer.GetTexture();
                _nextTick = now + FrameTicks;
            }
        }

        public override void Draw(float t)
        {
            if (_currentFrame != null)
                spriteBatch.Draw(_currentFrame, bounds, Color.White);
        }

        public void Stop()
        {
            _isPlaying = false;
            _updatesEnabled = false;
        }

        public void Cleanup()
        {
            Stop();
            _videoPlayer?.Dispose();
            _videoPlayer = null;
            _video = null;
            _currentFrame = null;
            if (_instance == this) _instance = null;
        }

        public void CleanupOnKill()
        {
            _updatesEnabled = false;
            _isPlaying = false;
            _currentFrame = null;
            if (_instance == this) _instance = null;
        }


        public bool IsPlaying => _isPlaying;
    }
}