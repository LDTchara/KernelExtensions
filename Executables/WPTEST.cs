using Hacknet;
using Hacknet.Gui;
using KernelExtensions.Modules;
using Microsoft.Xna.Framework;
using Pathfinder.Executable;

namespace KernelExtensions.Executables
{
    /// <summary>
    /// 视频壁纸 EXE —— 只负责创建/管理 VideoWallpaperModule 和提供 UI 按钮。
    /// 视频渲染由 Module 在 os.modules 背景层完成。
    /// </summary>
    public class WPTEST : BaseExecutable
    {
        private VideoWallpaperModule _videoModule;
        private bool _moduleAdded;

        public WPTEST(Rectangle location, OS operatingSystem, string[] args)
            : base(location, operatingSystem, args)
        {
            ramCost = 0;
            IdentifierName = "WPTEST";
            PID = 1;
        }

        public override void LoadContent()
        {
            base.LoadContent();

            // 创建视频模块
            _videoModule = new VideoWallpaperModule(
                new Rectangle(0, 0,
                    os.ScreenManager.GraphicsDevice.Viewport.Width,
                    os.ScreenManager.GraphicsDevice.Viewport.Height),
                os);

            if (_videoModule.LoadAndPlay("VideoTest", "1"))
            {
                // 插入到 os.modules 头部 → 背景层
                os.modules.Insert(0, _videoModule);
                _moduleAdded = true;
            }
            else
            {
                isExiting = true;
            }
        }

        public override void Update(float t)
        {
            base.Update(t);

            if (isExiting && _moduleAdded)
            {
                RemoveModule();
            }
        }

        public override void Draw(float t)
        {
            base.Draw(t);
            drawTarget();
            drawOutline();

            Rectangle content = new(
                bounds.X + 2,
                bounds.Y + PANEL_HEIGHT,
                bounds.Width - 4,
                bounds.Height - PANEL_HEIGHT - 2);

            // 透明背景 —— 让壁纸透过 EXE 窗口可见
            int y = content.Y + 10;
            int btnW = content.Width - 20;
            int btnH = 25;
            int gap = 5;

            TextItem.doFontLabel(
                new Vector2(content.X + 10, y),
                "Video Wallpaper", GuiData.font, Color.White);
            y += 30;

            if (_moduleAdded && _videoModule != null)
            {
                string status = _videoModule.IsPlaying
                    ? "Status: Playing"
                    : "Status: Stopped";
                TextItem.doFontLabel(
                    new Vector2(content.X + 10, y),
                    status, GuiData.smallfont, Color.LightGreen);
                y += btnH + gap;

                // 速度调节
                string speedLabel = $"Speed: {_videoModule.Speed:F2}x";
                TextItem.doFontLabel(
                    new Vector2(content.X + 10, y),
                    speedLabel, GuiData.smallfont, Color.Cyan);
                y += btnH + gap;

                int halfW = (btnW - gap) / 2;
                if (Button.doButton(9002, content.X + 10, y, halfW, btnH,
                    "-0.01", os.lockedColor))
                {
                    _videoModule.AdjustSpeed(-0.01f);
                }
                if (Button.doButton(9003, content.X + 10 + halfW + gap, y, halfW, btnH,
                    "+0.01", os.highlightColor))
                {
                    _videoModule.AdjustSpeed(+0.01f);
                }
                y += btnH + gap;

                if (Button.doButton(9001, content.X + 10, y, btnW, btnH,
                    "Stop && Close", os.brightLockedColor))
                {
                    isExiting = true;
                }
            }
            else
            {
                TextItem.doFontLabel(
                    new Vector2(content.X + 10, y),
                    "Video not loaded.", GuiData.smallfont, Color.Red);
            }
        }

        public override void Killed()
        {
            if (_moduleAdded)
            {
                RemoveModule();
            }
            base.Killed();
        }

        public override void Completed()
        {
            RemoveModule();
            base.Completed();
        }

        private void RemoveModule()
        {
            if (!_moduleAdded || _videoModule == null)
                return;

            _videoModule.Cleanup();
            os.modules.Remove(_videoModule);
            _moduleAdded = false;
            _videoModule = null;
        }
    }
}
