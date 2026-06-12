using Hacknet;
using Hacknet.Effects;
using Hacknet.Gui;
using KernelExtensions.Patches;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Pathfinder.Executable;
using System;
using System.Collections.Generic;

namespace KernelExtensions.Executables
{
    public class EffectsPlayerExe : GameExecutable
    {
        private class EffectEntry
        {
            public string Name;
            public string Description;
            public float Duration = 8f;
            public Action<SpriteBatch, Rectangle, float> DrawAction;
            public Action<float> UpdateAction;
        }

        private List<EffectEntry> _effects = new();
        private int _currentIndex = 0;
        private float _timer = 0f;
        private bool _paused = false;
        private string _statusText = "";

        // 特效实例
        private HexGridBackground _hexGrid;
        private ShiftingGridEffect _shiftingGrid;
        private MovingBarsEffect _movingBars;
        private RaindropsEffect _raindrops;
        private DepthDotGridEffect _depthDot;
        private Patches.CustomColorPatch.DynColorConfig _rainbowConfig;

        public EffectsPlayerExe() : base()
        {
            ramCost = 300;
            IdentifierName = "EffectsPlayer";
            name = "EffectsPlayer";
        }

        public override void OnInitialize()
        {
            base.OnInitialize();
            InitEffects();
            LoadEffects();
        }

        private void InitEffects()
        {
            _hexGrid = new HexGridBackground(os.content);
            _shiftingGrid = new ShiftingGridEffect();
            _movingBars = new MovingBarsEffect();
            _raindrops = new RaindropsEffect();
            _raindrops.Init(os.content);
            _depthDot = new DepthDotGridEffect(os.content);
            _rainbowConfig = Patches.CustomColorPatch.ParseColorString("LDTchara");
        }

        private void LoadEffects()
        {
            // ——— 1) ZoomingDotGridEffect ———
            // 静态方法。绘制从中心向外扩散/旋转的点阵，模拟星空隧道。
            // Render(dest, sb, timer, themeColor)
            //   timer: 驱动循环，内部模 12 取余，timer*0.3≈3.6s 一周期
            //   themeColor: 点阵颜色
            _effects.Add(new EffectEntry { Name = "ZoomingDotGrid", Duration = 8f,
                DrawAction = (sb, b, t) => ZoomingDotGridEffect.Render(b, sb, t * 0.3f, GetRainbowColor()) });

            // ——— 2) GridEffect ———
            // 静态方法。绘制交叉网格线，每个交叉点有十字标记。
            // DrawGridBackground(dest, sb, desiredNumOfBlocks, CrossColor)
            //   desiredNumOfBlocks: 横向块数，影响网格密度
            //   CrossColor: 十字标记的颜色
            _effects.Add(new EffectEntry { Name = "GridEffect", Duration = 5f,
                DrawAction = (sb, b, t) => GridEffect.DrawGridBackground(b, sb, 12, GetRainbowColor() * 0.3f) });

            // ——— 3) HexGridBackground ———
            // 需要 Update + Draw。六边形蜂窝网格背景，原版 KaguyaTrial 使用。
            // Draw(dest, sb, first, second, algorithm, angle)
            //   first: 背景填充色（未被网格覆盖的区域）
            //   second: 网格线和六边形颜色
            //   algorithm: 枚举值 StandardMono / SinWash / CorrectedSinWash，控制颜色抖动算法
            //   angle: 作用不明，源码里传 0
            _effects.Add(new EffectEntry { Name = "HexGrid", Duration = 8f,
                UpdateAction = (dt) => _hexGrid.Update(dt),
                DrawAction = (sb, b, t) => _hexGrid.Draw(b, sb, Color.Black, GetRainbowColor() * 0.2f,
                    HexGridBackground.ColoringAlgorithm.CorrectedSinWash, 0f) });

            // ——— 4) ShiftingGridEffect ———
            // 需要 Update + Draw。移动网格线，每个网格块独立做颜色过渡动画。
            // RenderGrid(bounds, sb, c1, c2, c3, centreEffect)
            //   c1/c2/c3: 三层颜色，由网格块的内插值（0~1）决定实际颜色
            //   centreEffect: 是否偏移网格以居中（仅影响 X 起点对齐方式）
            _effects.Add(new EffectEntry { Name = "ShiftingGrid", Duration = 8f,
                UpdateAction = (dt) => _shiftingGrid.Update(dt),
                DrawAction = (sb, b, t) => _shiftingGrid.RenderGrid(b, sb,
                    GetRainbowColor() * 0.3f, GetRainbowColor() * 0.15f, Color.White * 0.05f, false) });

            // ——— 5) MovingBarsEffect ———
            // 需要 Update + Draw。模拟音频频谱条（纵向条随机变化高度）。
            // Draw(sb, bounds, minHeight, lineWidth, lineSeperation, drawColor)
            //   minHeight: 最短条高度（像素）
            //   lineWidth: 每条宽度（像素）
            //   lineSeperation: 条间距（像素）
            // 另有公共字段 MinLineChangeTime/MaxLineChangeTime 控制变化速率（默认 0.2~2s）
            _effects.Add(new EffectEntry { Name = "MovingBars", Duration = 8f,
                UpdateAction = (dt) => _movingBars.Update(dt),
                DrawAction = (sb, b, t) => _movingBars.Draw(sb, b, 5f, 6f, 4f, GetRainbowColor() * 0.3f) });

            // ——— 6) RaindropsEffect ———
            // 需要 Update + Render。Matrix 风格数字雨 / 雨滴效果。
            // Update(dt, dropsAddedPerSecond): 每帧更新粒子，dropsPerSec 控制生成速率
            // Render(dest, sb, DropColor, maxCircleRadius, maxFlashWidth):
            //   DropColor: 雨滴颜色
            //   maxCircleRadius: 落地溅射圈最大半径
            //   maxFlashWidth: 落地闪光最大宽度
            // 另有公共字段 FallRate(下落速度)/CircleExpandRate(溅射扩散速度) 等
            _effects.Add(new EffectEntry { Name = "Raindrops", Duration = 10f,
                UpdateAction = (dt) => _raindrops.Update(dt, 8f),
                DrawAction = (sb, b, t) => _raindrops.Render(b, sb, GetRainbowColor() * 0.5f, 15f, 20f) });

            // ——— 7) DepthDotGridEffect ———
            // 纯 Draw。递归绘制多层点阵，产生 3D 景深感。
            // DrawGrid(fullAreaDest, xyOffset, sb, pixelsInOnRecurse, recursionSteps,
            //          dotColor, dotSeperation, dotSize, MaxDepthEffectDistance, timer, chaosPercent)
            //   其中后 8 个参数的含义我无法完全确定，以下是我的理解：
            //   pixelsInOnRecurse=1f    — 递归深度每层增加多少像素偏移？不确定
            //   recursionSteps=3         — 递归层数（这里 3 层）
            //   dotColor=...             — 最外层点颜色
            //   dotSeperation=15f        — 点间距
            //   dotSize=3f               — 点大小
            //   MaxDepthEffectDistance=30f — 最大景深偏移距离
            //   timer=t*0.2f             — 时间驱动
            //   chaosPercent=0.3f        — 随机扰动程度
            _effects.Add(new EffectEntry { Name = "DepthDotGrid", Duration = 8f,
                DrawAction = (sb, b, t) => _depthDot.DrawGrid(b, Vector2.Zero, sb,
                    1f, 3, GetRainbowColor() * 0.2f, 15f, 3f, 30f, t * 0.2f, 0.3f) });

            if (_effects.Count > 0)
                _statusText = $"{_effects[0].Name}  —  1/{_effects.Count}";
        }

        public override void OnUpdate(float dt)
        {
            base.OnUpdate(dt);
            if (_effects.Count == 0 || _paused) return;
            _timer += dt;
            _effects[_currentIndex].UpdateAction?.Invoke(dt);
        }

        private void NextEffect()
        {
            _timer = 0f;
            _currentIndex = (_currentIndex + 1) % _effects.Count;
            _statusText = $"{_effects[_currentIndex].Name}  —  {_currentIndex + 1}/{_effects.Count}";
        }

        private void PrevEffect()
        {
            _timer = 0f;
            _currentIndex = (_currentIndex - 1 + _effects.Count) % _effects.Count;
            _statusText = $"{_effects[_currentIndex].Name}  —  {_currentIndex + 1}/{_effects.Count}";
        }

        public override void Draw(float t)
        {
            base.Draw(t);
            drawOutline();
            Rectangle bg = new(bounds.X + 2, bounds.Y + PANEL_HEIGHT,
                               bounds.Width - 4, bounds.Height - PANEL_HEIGHT - 2);
            spriteBatch.Draw(Utils.white, bg, new Color(10, 10, 10));
            if (_effects.Count == 0) return;
            var cur = _effects[_currentIndex];
            cur.DrawAction?.Invoke(spriteBatch, bg, _timer);
            TextItem.doFontLabel(new Vector2(bg.X + 10, bg.Y + 5),
                _statusText, GuiData.font, GetRainbowColor());
            TextItem.doFontLabel(new Vector2(bg.X + 10, bg.Y + bg.Height - 40),
                cur.Description ?? cur.Name, GuiData.smallfont, Color.White * 0.7f);
            // 进度条已取消
            int btnY = bg.Y + bg.Height - 28;
            if (Button.doButton(9001 + PID, bg.X + 10, btnY, 50, 22, "上一个", os.highlightColor))
                PrevEffect();
            if (Button.doButton(9002 + PID, bg.X + 66, btnY, 40, 22, _paused ? "继续" : "暂停", os.lockedColor))
                _paused = !_paused;
            if (Button.doButton(9003 + PID, bg.X + 112, btnY, 50, 22, "下一个", os.highlightColor))
                NextEffect();
            if (Button.doButton(9004 + PID, bg.X + bg.Width - 40, btnY, 30, 22, "X", os.brightLockedColor))
                isExiting = true;

            // 去掉进度条
        }
        /// <summary>返回当前帧的彩虹色（来自 CustomColor 系统），供特效 Draw 使用</summary>
        private Color GetRainbowColor()
        {
            if (_rainbowConfig != null)
                return Patches.CustomColorPatch.CalcColor(_rainbowConfig, OS.currentElapsedTime);
            return Color.Cyan;
        }
    }
}
