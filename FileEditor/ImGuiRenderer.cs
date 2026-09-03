using Hacknet;
using Hacknet.Extensions;
using KernelExtensions.Utilities;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using ImGuiNET;

namespace KernelExtensions.FileEditor
{
    /// <summary>
    /// ImGui 渲染器（从 HacknetThemeEditor 移植）。
    /// 建立独立的 ImGui 上下文，桥接 FNA 的输入/渲染管线。
    /// 使用方式：BeforeLayout(gameTime) → 绘制 UI → AfterLayout()。
    /// </summary>
    public static class DrawVertDeclaration
    {
        public static readonly VertexDeclaration Declaration;
        public static readonly int Size;

        static DrawVertDeclaration()
        {
            unsafe { Size = sizeof(ImDrawVert); }

            Declaration = new VertexDeclaration(
                Size,

                // Position
                new VertexElement(0, VertexElementFormat.Vector2, VertexElementUsage.Position, 0),

                // UV
                new VertexElement(8, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 0),

                // Color
                new VertexElement(16, VertexElementFormat.Color, VertexElementUsage.Color, 0)
            );
        }
    }

    /// <summary>
    /// ImGui renderer for use with XNA-likes (FNA &amp; MonoGame)
    /// </summary>
    public class ImGuiRenderer
    {
        private Game _game;

        // Graphics
        private GraphicsDevice _graphicsDevice;

        private BasicEffect _effect;
        private RasterizerState _rasterizerState;

        private byte[] _vertexData;
        private VertexBuffer _vertexBuffer;
        private int _vertexBufferSize;

        private byte[] _indexData;
        private IndexBuffer _indexBuffer;
        private int _indexBufferSize;

        // Textures
        private Dictionary<IntPtr, Texture2D> _loadedTextures;

        private int _textureId;
        private IntPtr? _fontTextureId;

        // ===== Hacknet 位图字体（从 SpriteFont 提取字形注册进 ImGui 图集）=====
        /// <summary>普通文本字体（GuiData.tinyfont 字形）。</summary>
        public ImFontPtr TinyFont;
        /// <summary>标题字体（GuiData.titlefont 字形）。</summary>
        public ImFontPtr TitleFont;

        /// <summary>字体图集纹理是否已创建。</summary>
        internal bool HasFontTexture => _fontTextureId.HasValue;

        /// <summary>待回填像素的字形：源纹理、源区域、字符（图集 custom rect 按注册顺序一一对应）。</summary>
        private struct PendingGlyph
        {
            public Texture2D Source;      // 源字体纹理
            public Rectangle SrcRect;     // 字形在源纹理中的区域
            public char Char;             // 字符（仅用于区分，像素按注册顺序回填）
        }

        private readonly List<PendingGlyph> _pendingGlyphs = new();
        private readonly Dictionary<Texture2D, byte[]> _fontPixelCache = new(); // 源纹理 RGBA 缓存（一次性 GetData）

        // Input
        private int _scrollWheelValue;
        private readonly float WHEEL_DELTA = 120;
        private Keys[] _allKeys = (Keys[])Enum.GetValues(typeof(Keys));

        public ImGuiRenderer(Game game)
        {
            var context = ImGui.CreateContext();
            ImGui.SetCurrentContext(context);

            _game = game ?? throw new ArgumentNullException(nameof(game));
            _graphicsDevice = game.GraphicsDevice;

            _loadedTextures = new Dictionary<IntPtr, Texture2D>();

            _rasterizerState = new RasterizerState()
            {
                CullMode = CullMode.None,
                DepthBias = 0,
                FillMode = FillMode.Solid,
                MultiSampleAntiAlias = false,
                ScissorTestEnable = true,
                SlopeScaleDepthBias = 0
            };

            SetupInput();
        }

        #region ImGuiRenderer

        /// <summary>
        /// Creates a texture and loads the font data from ImGui. Should be called when the <see cref="GraphicsDevice" /> is initialized but before any rendering is done
        /// </summary>
        public virtual unsafe void RebuildFontAtlas()
        {
            var io = ImGui.GetIO();

            // 注册 Hacknet 位图字体（tinyfont/titlefont）字形 —— 必须在图集 build 之前 AddCustomRectFontGlyph
            _pendingGlyphs.Clear();
            TinyFont = RegisterHacknetFont(io.Fonts, GuiData.tinyfont, out bool tinyOk);
            TitleFont = RegisterHacknetFont(io.Fonts, GuiData.titlefont, out bool titleOk);

            // Hacknet 字体不可用/无 CJK 时，用系统中文字体兑底（避免继续显示方框）
            bool setDefaultFont = false;
            if (!tinyOk)
            {
                TinyFont = AddSystemFontFallback(io.Fonts, 14f);
                if (TinyFont.NativePtr != null)
                {
                    setDefaultFont = true; // FontDefault 在 build 之后再写（build 前写时机不可靠）
                }
            }
            if (!titleOk)
            {
                TitleFont = AddSystemFontFallback(io.Fonts, 24f);
            }

            // 触发 build 并取得图集像素（RGBA32）
            io.Fonts.GetTexDataAsRGBA32(out byte* pixelData, out int width, out int height, out int bytesPerPixel);

            // 图集 build 失败（字形过多/超尺寸）时像素为空：不创建纹理，UI 文字整体失效
            if (pixelData == null || width <= 0 || height <= 0)
            {
                KELog.Error($"[FileEditor] font atlas build FAILED (pixels={(IntPtr)pixelData}, {width}x{height}) — UI will not render text");
                return;
            }

            // 把源字形像素回填进图集（默认字体 ASCII 之外，中文等字形都来自 Hacknet 字体）
            WriteGlyphPixels(io.Fonts, pixelData, width, height);

            if (setDefaultFont && TinyFont.NativePtr != null)
            {
                // 切到兑底字体，否则当前字体仍是无 CJK 的默认字体（io.FontDefault 属性只读，直接写字段；须在 build 后）
                io.NativePtr->FontDefault = TinyFont.NativePtr;
            }

            // 字形自检：验证两个字体是否真的包含 CJK（'中' U+4E2D；FindGlyphNoFallback 找不到返回 null）
            bool tinyCjk = TinyFont.NativePtr != null && TinyFont.FindGlyphNoFallback(0x4E2D).NativePtr != null;
            bool titleCjk = TitleFont.NativePtr != null && TitleFont.FindGlyphNoFallback(0x4E2D).NativePtr != null;
            KELog.Info($"[FileEditor] glyph check: tinyCJK={tinyCjk}, titleCJK={titleCjk}");

            // Copy the data to a managed array
            var pixels = new byte[width * height * bytesPerPixel];
            unsafe { Marshal.Copy(new IntPtr(pixelData), pixels, 0, pixels.Length); }

            // Create and register the texture as an XNA texture
            var tex2d = new Texture2D(_graphicsDevice, width, height, false, SurfaceFormat.Color);
            tex2d.SetData(pixels);

            // Should a texture already have been build previously, unbind it first so it can be deallocated
            if (_fontTextureId.HasValue) UnbindTexture(_fontTextureId.Value);

            // Bind the new texture to an ImGui-friendly id
            _fontTextureId = BindTexture(tex2d);

            // Let ImGui know where to find the texture
            io.Fonts.SetTexID(_fontTextureId.Value);
            io.Fonts.ClearTexData(); // Clears CPU side texture data
        }

        // ================= Hacknet 位图字体 =================

        /// <summary>
        /// 把 Hacknet 的 SpriteFont（tinyfont/titlefont）字形注册为 ImGui 字体：
        /// 每个字形 AddCustomRectFontGlyph 加入图集，build 后再把源纹理像素回填到图集对应区域。
        /// 反射读取 FNA SpriteFont 内部 glyphs/kerning 字典（无公开 API）。
        /// </summary>
        /// <param name="atlas">ImGui 字体图集。</param>
        /// <param name="spriteFont">Hacknet 位图字体（GuiData.tinyfont / titlefont）。</param>
        /// <param name="ok">true = 字形已成功注册（含 CJK）；false = 反射失败/字体缺 CJK，调用方应回退系统字体。</param>
        private unsafe ImFontPtr RegisterHacknetFont(ImFontAtlasPtr atlas, SpriteFont spriteFont, out bool ok)
        {
            ok = false;

            // 先加默认字体保底（ASCII + fallback '?'），Hacknet 字形随后覆盖同名 glyph
            ImFontPtr font = atlas.AddFontDefault();
            if (spriteFont == null)
            {
                KELog.Warn("[FileEditor] GuiData font is null");
                return font;
            }

            try
            {
                var glyphs = GetGlyphMap(spriteFont);
                var kerning = GetKerningMap(spriteFont);
                Texture2D tex = GetFontTexture(spriteFont);
                if (glyphs == null || glyphs.Count == 0)
                {
                    KELog.Warn($"[FileEditor] glyph map {(glyphs == null ? "null" : "empty")} for {spriteFont} — fallback to system font");
                    return font;
                }
                if (tex == null)
                {
                    KELog.Warn($"[FileEditor] font texture not found for {spriteFont} — fallback to system font");
                    return font;
                }

                // 诊断：字形数/纹理/是否有 CJK
                int cjkCount = 0;
                foreach (var ch in glyphs.Keys)
                {
                    if (ch >= 0x4E00 && ch <= 0x9FFF)
                    {
                        cjkCount++;
                    }
                }
                KELog.Info($"[FileEditor] Hacknet font ok: glyphs={glyphs.Count}, cjk={cjkCount}, kerning={kerning?.Count ?? 0}, tex={tex.Width}x{tex.Height}");
                if (cjkCount == 0)
                {
                    // 字体本身不含 CJK（如原版 Latin 字体）：交给系统字体兑底
                    KELog.Warn("[FileEditor] Hacknet font has no CJK glyphs — using system font fallback");
                    return font;
                }

                foreach (var kv in glyphs)
                {
                    Rectangle r = kv.Value;
                    if (r.Width <= 0 || r.Height <= 0)
                    {
                        continue;
                    }

                    char ch = kv.Key;
                    float advance = r.Width + 1f;
                    if (kerning != null && kerning.TryGetValue(ch, out Vector3 k) && k.Y > 0f)
                    {
                        advance = k.Y;
                    }

                    atlas.AddCustomRectFontGlyph(font, ch, r.Width, r.Height, advance, System.Numerics.Vector2.Zero);
                    _pendingGlyphs.Add(new PendingGlyph
                    {
                        Source = tex,
                        SrcRect = r,
                        Char = ch
                    });
                }
            }
            catch (Exception ex)
            {
                KELog.Warn($"[FileEditor] Hacknet font register failed: {ex.Message}");
                return font;
            }

            ok = true;
            return font;
        }

        /// <summary>用扩展字体（FontReplace 的 ttf，与游戏一致）优先，回退系统中文 TTF/TTC。全部失败返回 null 指针。</summary>
        private static unsafe ImFontPtr AddSystemFontFallback(ImFontAtlasPtr atlas, float size)
        {
            // 优先：HacknetFontReplace 实际使用的字体文件（读取其配置，或扫描 Plugins/Font）
            string extFont = FindFontReplaceTtf();
            if (!string.IsNullOrEmpty(extFont))
            {
                try
                {
                    IntPtr ranges = atlas.GetGlyphRangesChineseSimplifiedCommon();
                    ImFontPtr font = atlas.AddFontFromFileTTF(extFont, size, default, ranges);
                    if (font.NativePtr != null)
                    {
                        KELog.Info($"[FileEditor] FontReplace font ok: {Path.GetFileName(extFont)} ({size}px)");
                        return font;
                    }
                }
                catch (Exception ex)
                {
                    KELog.Warn($"[FileEditor] FontReplace font load failed: {ex.Message}");
                }
            }

            string fontsDir = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
            string[] candidates = { "msyh.ttc", "simhei.ttf", "simsun.ttc", "msyh.ttf", "arialuni.ttf" };

            foreach (string name in candidates)
            {
                string path = Path.Combine(fontsDir, name);
                if (!File.Exists(path))
                {
                    continue;
                }
                try
                {
                    // 用"简体中文常用字"范围（约 6k 字形）：ChineseFull 约 2.5 万字形 × 2 个字体
                    // 极易撑爆 4096x4096 图集导致 build 失败、UI 文字整体失效
                    IntPtr ranges = atlas.GetGlyphRangesChineseSimplifiedCommon();
                    ImFontConfigPtr cfg = default;
                    ImFontPtr font = atlas.AddFontFromFileTTF(path, size, cfg, ranges);
                    if (font.NativePtr != null)
                    {
                        KELog.Info($"[FileEditor] system font fallback ok: {name} ({size}px)");
                        return font;
                    }
                }
                catch (Exception ex)
                {
                    KELog.Warn($"[FileEditor] fallback font {name} failed: {ex.Message}");
                }
            }

            KELog.Warn("[FileEditor] no system CJK font available for fallback");
            return default;
        }

        /// <summary>
        /// 定位 FontReplace 实际使用的字体文件（与游戏显示一致）：
        /// 优先解析 Plugins/Font/HacknetFontReplace.config.xml 的 ActiveFontGroup → 第一个 FontPath；
        /// 失败则扫 Plugins/Font 下第一个 ttf/otf/ttc；再失败返回 null（调用方回退系统字体）。
        /// </summary>
        private static string FindFontReplaceTtf()
        {
            try
            {
                string extRoot = ExtensionLoader.ActiveExtensionInfo?.FolderPath;
                if (string.IsNullOrEmpty(extRoot))
                {
                    return null;
                }

                string configPath = Path.Combine(extRoot, "Plugins", "Font", "HacknetFontReplace.config.xml");
                if (File.Exists(configPath))
                {
                    var doc = XDocument.Load(configPath);
                    string activeGroup = doc.Root?.Element("ActiveFontGroup")?.Value?.Trim();
                    if (string.IsNullOrEmpty(activeGroup))
                    {
                        activeGroup = doc.Root?.Element("HacknetFontReplace")?.Element("ActiveFontGroup")?.Value?.Trim();
                    }

                    foreach (var group in doc.Root?.Descendants("FontGroup") ?? Enumerable.Empty<XElement>())
                    {
                        string name = group.Attribute("Name")?.Value;
                        if (!string.IsNullOrEmpty(activeGroup) && name != activeGroup)
                        {
                            continue;
                        }

                        string rel = group.Elements("FontPath").FirstOrDefault()?.Value?.Trim();
                        if (string.IsNullOrEmpty(rel))
                        {
                            continue;
                        }

                        string p = Path.Combine(extRoot, rel);
                        if (File.Exists(p))
                        {
                            return p;
                        }
                    }
                }

                // 兜底：Plugins/Font 下第一个字体文件
                string fontDir = Path.Combine(extRoot, "Plugins", "Font");
                if (Directory.Exists(fontDir))
                {
                    foreach (string pattern in new[] { "*.ttf", "*.otf", "*.ttc" })
                    {
                        string f = Directory.GetFiles(fontDir, pattern).FirstOrDefault();
                        if (f != null)
                        {
                            return f;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                KELog.Warn($"[FileEditor] FontReplace font locate failed: {ex.Message}");
            }

            return null;
        }

        /// <summary>把待回填字形的像素从源纹理拷进图集（atlasPixels 为 RGBA32）。</summary>
        /// <remarks>AddCustomRectFontGlyph 按调用顺序 append 到 atlas.CustomRects，
        /// 因此 _pendingGlyphs 与 CustomRects 按下标一一对应（注册都在 build 前）。</remarks>
        private unsafe void WriteGlyphPixels(ImFontAtlasPtr atlas, byte* atlasPixels, int atlasWidth, int atlasHeight)
        {
            if (_pendingGlyphs.Count == 0)
            {
                return;
            }

            var rects = atlas.CustomRects;
            int count = Math.Min(rects.Size, _pendingGlyphs.Count);
            for (int i = 0; i < count; i++)
            {
                var pg = _pendingGlyphs[i];
                var r = rects[i]; // ImFontAtlasCustomRect：build 后 X/Y/Width/Height 为图集内像素区域

                int w = pg.SrcRect.Width;
                int h = pg.SrcRect.Height;
                int dstX = r.X;
                int dstY = r.Y;
                if (w <= 0 || h <= 0 || dstX + w > atlasWidth || dstY + h > atlasHeight)
                {
                    continue;
                }

                if (!_fontPixelCache.TryGetValue(pg.Source, out byte[] src))
                {
                    src = GetTextureRGBA(pg.Source);
                    _fontPixelCache[pg.Source] = src;
                }
                if (src == null)
                {
                    continue;
                }

                int texW = pg.Source.Width;
                for (int y = 0; y < h; y++)
                {
                    int srcRow = (pg.SrcRect.Y + y) * texW + pg.SrcRect.X;
                    int dstRow = (dstY + y) * atlasWidth + dstX;
                    for (int x = 0; x < w; x++)
                    {
                        int si = (srcRow + x) * 4;
                        int di = (dstRow + x) * 4;
                        atlasPixels[di] = src[si];
                        atlasPixels[di + 1] = src[si + 1];
                        atlasPixels[di + 2] = src[si + 2];
                        atlasPixels[di + 3] = src[si + 3];
                    }
                }
            }
        }

        /// <summary>整张纹理转 RGBA 字节（一次性，按字体纹理缓存）。</summary>
        private static byte[] GetTextureRGBA(Texture2D tex)
        {
            try
            {
                var colors = new Color[tex.Width * tex.Height];
                tex.GetData(colors);
                var bytes = new byte[colors.Length * 4];
                for (int i = 0; i < colors.Length; i++)
                {
                    bytes[i * 4] = colors[i].R;
                    bytes[i * 4 + 1] = colors[i].G;
                    bytes[i * 4 + 2] = colors[i].B;
                    bytes[i * 4 + 3] = colors[i].A;
                }
                return bytes;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>反射读取 SpriteFont 的字体纹理（FNA 无公开 Texture 属性，字段/属性名带下划线或裸名）。</summary>
        private static Texture2D GetFontTexture(SpriteFont font)
        {
            try
            {
                foreach (string name in new[] { "Texture", "_texture", "texture" })
                {
                    var prop = typeof(SpriteFont).GetProperty(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    if (prop?.GetValue(font) is Texture2D t)
                    {
                        return t;
                    }
                    var field = typeof(SpriteFont).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    if (field?.GetValue(font) is Texture2D t2)
                    {
                        return t2;
                    }
                }
            }
            catch { }
            return null;
        }

        /// <summary>反射读取 SpriteFont 的 glyphs 字典（FNA 字段名带下划线，兼容 MonoGame 无下划线）。</summary>
        private static Dictionary<char, Rectangle> GetGlyphMap(SpriteFont font)
        {
            try
            {
                foreach (string name in new[] { "glyphs", "_glyphs", "Glyphs" })
                {
                    var field = typeof(SpriteFont).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    if (field?.GetValue(font) is Dictionary<char, Rectangle> d)
                    {
                        return d;
                    }
                }
            }
            catch { }
            return null;
        }

        /// <summary>反射读取 SpriteFont 的 kerning 字典（Vector3.X=左 bearing，Y=advance，Z=右 bearing）。</summary>
        private static Dictionary<char, Vector3> GetKerningMap(SpriteFont font)
        {
            try
            {
                foreach (string name in new[] { "kerning", "_kerning", "Kerning" })
                {
                    var field = typeof(SpriteFont).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    if (field?.GetValue(font) is Dictionary<char, Vector3> d)
                    {
                        return d;
                    }
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Creates a pointer to a texture, which can be passed through ImGui calls such as <c>ImGui.Image</c>. That pointer is then used by ImGui to let us know what texture to draw
        /// </summary>
        public virtual IntPtr BindTexture(Texture2D texture)
        {
            var id = new IntPtr(_textureId++);

            _loadedTextures.Add(id, texture);

            return id;
        }

        /// <summary>
        /// Removes a previously created texture pointer, releasing its reference and allowing it to be deallocated
        /// </summary>
        public virtual void UnbindTexture(IntPtr textureId)
        {
            _loadedTextures.Remove(textureId);
        }

        /// <summary>
        /// Sets up ImGui for a new frame, should be called at frame start
        /// </summary>
        public virtual void BeforeLayout(GameTime gameTime)
        {
            ImGui.GetIO().DeltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

            UpdateInput();

            ImGui.NewFrame();
        }

        /// <summary>
        /// Asks ImGui for the generated geometry data and sends it to the graphics pipeline, should be called after the UI is drawn using ImGui.** calls
        /// </summary>
        public virtual void AfterLayout()
        {
            ImGui.Render();

            unsafe
            {
                var drawData = ImGui.GetDrawData();
                drawData.ScaleClipRects(ImGui.GetIO().DisplayFramebufferScale); // 对齐 HacknetThemeEditor 的 RenderDrawData
                RenderDrawData(drawData);      // 顶点/索引拷贝到 GPU buffer
                RenderCommandLists(drawData);  // 设置渲染状态并实际提交绘制
            }
        }

        #endregion ImGuiRenderer

        #region Setup &amp; Update

        /// <summary>
        /// Setup key input event handler.
        /// </summary>
        protected virtual void SetupInput()
        {
            var io = ImGui.GetIO();

            // FNA-specific ///////////////////////////
            TextInputEXT.TextInput += c =>
            {
                if (c == '\t') return;

                ImGui.GetIO().AddInputCharacter(c);
            };
            ///////////////////////////////////////////
        }

        /// <summary>
        /// Updates the <see cref="Effect" /> to the current matrices and texture
        /// </summary>
        protected virtual Effect UpdateEffect(Texture2D texture)
        {
            _effect = _effect ?? new BasicEffect(_graphicsDevice);

            var io = ImGui.GetIO();

            _effect.World = Matrix.Identity;
            _effect.View = Matrix.Identity;
            _effect.Projection = Matrix.CreateOrthographicOffCenter(0f, io.DisplaySize.X, io.DisplaySize.Y, 0f, -1f, 1f);
            _effect.TextureEnabled = true;
            _effect.Texture = texture;
            _effect.VertexColorEnabled = true;

            return _effect;
        }

        /// <summary>
        /// Sends XNA input state to ImGui
        /// </summary>
        protected virtual void UpdateInput()
        {
            if (!_game.IsActive) return;

            var io = ImGui.GetIO();

            var mouse = Mouse.GetState();
            // 标志位：本次 Keyboard.GetState 是 ImGui 内部读取，KeyboardGetStatePrefix 放行真实状态
            FileEditorPatch.ImGuiReadingKeyboard = true;
            KeyboardState keyboard;
            try
            {
                keyboard = Keyboard.GetState();
            }
            finally
            {
                FileEditorPatch.ImGuiReadingKeyboard = false;
            }
            io.AddMousePosEvent(mouse.X, mouse.Y);
            io.AddMouseButtonEvent(0, mouse.LeftButton == ButtonState.Pressed);
            io.AddMouseButtonEvent(1, mouse.RightButton == ButtonState.Pressed);
            io.AddMouseButtonEvent(2, mouse.MiddleButton == ButtonState.Pressed);
            io.AddMouseButtonEvent(3, mouse.XButton1 == ButtonState.Pressed);
            io.AddMouseButtonEvent(4, mouse.XButton2 == ButtonState.Pressed);

            io.AddMouseWheelEvent(
                0,
                (mouse.ScrollWheelValue - _scrollWheelValue) / WHEEL_DELTA);
            _scrollWheelValue = mouse.ScrollWheelValue;

            foreach (var key in _allKeys)
            {
                if (TryMapKeys(key, out ImGuiKey imguikey))
                {
                    io.AddKeyEvent(imguikey, keyboard.IsKeyDown(key));
                }
            }

            io.DisplaySize = new System.Numerics.Vector2(_graphicsDevice.PresentationParameters.BackBufferWidth, _graphicsDevice.PresentationParameters.BackBufferHeight);
            io.DisplayFramebufferScale = new System.Numerics.Vector2(1f, 1f);
        }

        private bool TryMapKeys(Keys key, out ImGuiKey imguikey)
        {
            //Special case not handed in the switch...
            //If the actual key we put in is "None", return none and true.
            //otherwise, return none and false.
            if (key == Keys.None)
            {
                imguikey = ImGuiKey.None;
                return true;
            }
            
            imguikey = key switch
            {
                Keys.Back => ImGuiKey.Backspace,
                Keys.Tab => ImGuiKey.Tab,
                Keys.Enter => ImGuiKey.Enter,
                Keys.CapsLock => ImGuiKey.CapsLock,
                Keys.Escape => ImGuiKey.Escape,
                Keys.Space => ImGuiKey.Space,
                Keys.PageUp => ImGuiKey.PageUp,
                Keys.PageDown => ImGuiKey.PageDown,
                Keys.End => ImGuiKey.End,
                Keys.Home => ImGuiKey.Home,
                Keys.Left => ImGuiKey.LeftArrow,
                Keys.Right => ImGuiKey.RightArrow,
                Keys.Up => ImGuiKey.UpArrow,
                Keys.Down => ImGuiKey.DownArrow,
                Keys.PrintScreen => ImGuiKey.PrintScreen,
                Keys.Insert => ImGuiKey.Insert,
                Keys.Delete => ImGuiKey.Delete,
                >= Keys.D0 and <= Keys.D9 => ImGuiKey._0 + (key - Keys.D0),
                >= Keys.A and <= Keys.Z => ImGuiKey.A + (key - Keys.A),
                >= Keys.NumPad0 and <= Keys.NumPad9 => ImGuiKey.Keypad0 + (key - Keys.NumPad0),
                Keys.Multiply => ImGuiKey.KeypadMultiply,
                Keys.Add => ImGuiKey.KeypadAdd,
                Keys.Subtract => ImGuiKey.KeypadSubtract,
                Keys.Decimal => ImGuiKey.KeypadDecimal,
                Keys.Divide => ImGuiKey.KeypadDivide,
                >= Keys.F1 and <= Keys.F24 => ImGuiKey.F1 + (key - Keys.F1),
                Keys.NumLock => ImGuiKey.NumLock,
                Keys.Scroll => ImGuiKey.ScrollLock,
                Keys.LeftShift => ImGuiKey.ModShift,
                Keys.LeftControl => ImGuiKey.ModCtrl,
                Keys.LeftAlt => ImGuiKey.ModAlt,
                Keys.OemSemicolon => ImGuiKey.Semicolon,
                Keys.OemPlus => ImGuiKey.Equal,
                Keys.OemComma => ImGuiKey.Comma,
                Keys.OemMinus => ImGuiKey.Minus,
                Keys.OemPeriod => ImGuiKey.Period,
                Keys.OemQuestion => ImGuiKey.Slash,
                Keys.OemTilde => ImGuiKey.GraveAccent,
                Keys.OemOpenBrackets => ImGuiKey.LeftBracket,
                Keys.OemCloseBrackets => ImGuiKey.RightBracket,
                Keys.OemPipe => ImGuiKey.Backslash,
                Keys.OemQuotes => ImGuiKey.Apostrophe,
                _ => ImGuiKey.None
            };

            return imguikey != ImGuiKey.None;
        }

        #endregion Setup &amp; Update

        #region Internals

        /// <summary>
        /// Creates the structure needed to tell the graphics device what to draw and where to draw it
        /// </summary>
        private unsafe void RenderDrawData(ImDrawDataPtr drawData)
        {
            // If there's nothing to render there's nothing else to do and we can return
            if (drawData.CmdListsCount == 0)
            {
                return;
            }

            // If the number of vertices we need to render is greater than the size of our vertex buffer, we need to increase the size of our buffer
            if (drawData.TotalVtxCount > _vertexBufferSize)
            {
                _vertexBuffer?.Dispose();

                _vertexBufferSize = (int)(drawData.TotalVtxCount * 1.5f);
                _vertexBuffer = new VertexBuffer(_graphicsDevice, DrawVertDeclaration.Declaration, _vertexBufferSize, BufferUsage.None);
                _vertexData = new byte[_vertexBufferSize * DrawVertDeclaration.Size];
            }

            // If the number of indices we need to render is greater than the size of our index buffer, we need to increase the size of our buffer
            if (drawData.TotalIdxCount > _indexBufferSize)
            {
                _indexBuffer?.Dispose();

                _indexBufferSize = (int)(drawData.TotalIdxCount * 1.5f);
                _indexBuffer = new IndexBuffer(_graphicsDevice, IndexElementSize.SixteenBits, _indexBufferSize, BufferUsage.None);
                _indexData = new byte[_indexBufferSize * sizeof(ushort)];
            }

            // Copy ImGui's vertices and indices to a set of managed byte arrays
            int vtxOffset = 0;
            int idxOffset = 0;

            for (int n = 0; n < drawData.CmdListsCount; n++)
            {
                ImDrawListPtr cmdList = drawData.CmdLists[n];

                fixed (void* vtxDstPtr = &_vertexData[vtxOffset * DrawVertDeclaration.Size])
                fixed (void* idxDstPtr = &_indexData[idxOffset * sizeof(ushort)])
                {
                    Buffer.MemoryCopy((void*)cmdList.VtxBuffer.Data, vtxDstPtr, _vertexData.Length, cmdList.VtxBuffer.Size * DrawVertDeclaration.Size);
                    Buffer.MemoryCopy((void*)cmdList.IdxBuffer.Data, idxDstPtr, _indexData.Length, cmdList.IdxBuffer.Size * sizeof(ushort));
                }

                vtxOffset += cmdList.VtxBuffer.Size;
                idxOffset += cmdList.IdxBuffer.Size;
            }

            // Copy the managed byte arrays to the gpu vertex- and index buffers
            _vertexBuffer.SetData(_vertexData, 0, drawData.TotalVtxCount * DrawVertDeclaration.Size);
            _indexBuffer.SetData(_indexData, 0, drawData.TotalIdxCount * sizeof(ushort));
        }

        private unsafe void RenderCommandLists(ImDrawDataPtr drawData)
        {
            _graphicsDevice.SetVertexBuffer(_vertexBuffer);
            _graphicsDevice.Indices = _indexBuffer;

            // 渲染状态：完全对齐 HacknetThemeEditor 的 RenderDrawData（已验证可正常显示）。
            // 关键：NonPremultiplied 混合（直通 alpha 字形）+ DepthRead + 全屏视口。
            var lastViewport = _graphicsDevice.Viewport;
            var lastScissorBox = _graphicsDevice.ScissorRectangle;

            _graphicsDevice.BlendFactor = Color.White;
            _graphicsDevice.BlendState = BlendState.NonPremultiplied;
            _graphicsDevice.RasterizerState = _rasterizerState;
            _graphicsDevice.DepthStencilState = DepthStencilState.DepthRead;
            _graphicsDevice.SamplerStates[0] = SamplerState.LinearClamp;
            _graphicsDevice.Viewport = new Viewport(0, 0,
                _graphicsDevice.PresentationParameters.BackBufferWidth,
                _graphicsDevice.PresentationParameters.BackBufferHeight);

            int vtxOffset = 0;
            int idxOffset = 0;

            for (int n = 0; n < drawData.CmdListsCount; n++)
            {
                ImDrawListPtr cmdList = drawData.CmdLists[n];

                for (int cmdi = 0; cmdi < cmdList.CmdBuffer.Size; cmdi++)
                {
                    ImDrawCmdPtr drawCmd = cmdList.CmdBuffer[cmdi];

                    if (drawCmd.ElemCount == 0)
                    {
                        continue;
                    }

                    if (!_loadedTextures.ContainsKey(drawCmd.TextureId))
                    {
                        throw new InvalidOperationException($"Could not find a texture with id '{drawCmd.TextureId}', please check your bindings");
                    }

                    _graphicsDevice.ScissorRectangle = new Rectangle(
                        (int)drawCmd.ClipRect.X,
                        (int)drawCmd.ClipRect.Y,
                        (int)(drawCmd.ClipRect.Z - drawCmd.ClipRect.X),
                        (int)(drawCmd.ClipRect.W - drawCmd.ClipRect.Y)
                    );

                    var effect = UpdateEffect(_loadedTextures[drawCmd.TextureId]);

                    foreach (var pass in effect.CurrentTechnique.Passes)
                    {
                        pass.Apply();

#pragma warning disable CS0618 // // FNA does not expose an alternative method.
                        _graphicsDevice.DrawIndexedPrimitives(
                            primitiveType: PrimitiveType.TriangleList,
                            baseVertex: (int)drawCmd.VtxOffset + vtxOffset,
                            minVertexIndex: 0,
                            numVertices: cmdList.VtxBuffer.Size,
                            startIndex: (int)drawCmd.IdxOffset + idxOffset,
                            primitiveCount: (int)drawCmd.ElemCount / 3
                        );
#pragma warning restore CS0618
                    }
                }

                vtxOffset += cmdList.VtxBuffer.Size;
                idxOffset += cmdList.IdxBuffer.Size;
            }

            // 恢复被修改的状态（与 HacknetThemeEditor 一致）
            _graphicsDevice.Viewport = lastViewport;
            _graphicsDevice.ScissorRectangle = lastScissorBox;
        }

        #endregion Internals
    }
}