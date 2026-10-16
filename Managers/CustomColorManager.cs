using Hacknet;
using Hacknet.Extensions;
using KernelExtensions.Utilities;
using Microsoft.Xna.Framework;
using System.Reflection;
using System.Text.RegularExpressions;

namespace KernelExtensions.Managers
{
    /// <summary>
    /// 自定义动态颜色系统（Manager 部分，从 CustomColorPatch 拆出）。
    ///
    /// ── 作用 ──
    /// 在主题 XML 和模组配置的颜色字段中，使用关键字代替静态色值，
    /// 实现每帧刷新的动态颜色（彩虹、渐变）。
    ///
    /// ── 支持的关键字 ──
    ///   1. 彩虹色系列
    ///      LDTchara                    默认彩虹（速度 0.1）
    ///      LDTchara:0.05               自定义速度
    ///      LDTchara:0.1:0.8            速度:透明度
    ///      LDTchara:0.1:1.0:0.8:1.0   速度:透明度:饱和度:明度
    ///      RGB 是 LDTchara 的别名，语法相同。
    ///
    ///   2. 双色渐变（旧语法，兼容）
    ///      Gradient:#FF0000:#00FF00:2.0  红→蓝渐变，2 秒循环
    ///
    ///   3. 预设引用（推荐）
    ///      Riptide                     引用 CustomColor/Riptide.xml
    ///      Riptide:0.5                 速度 x0.5
    ///      Riptide:0.5:0.8            速度 x0.5 + 透明度 80%
    ///      **预设优先于内置关键字**：扩展若自建了名为 RGB / LDTchara 的预设，
    ///      同名关键字就用扩展自己的预设（未建才回退内置彩虹）；
    ///      完整 Gradient:#起始:#结束:秒数 语法不受影响（它是语法，不是预设名）。
    ///
    /// ── 预设文件格式 ──
    ///   CustomColor/预设名.xml：
    ///     <ColorPreset>
    ///       <Name>Riptide</Name>
    ///       <CustomColor id="0"><Color>#FF6B6B</Color><Duration>2.0</Duration></CustomColor>
    ///       <CustomColor id="1"><Color>#4ECDC4</Color><Duration>2.0</Duration></CustomColor>
    ///       ...
    ///     </ColorPreset>
    ///
    /// ── 别名同步 ──
    ///   defaultHighlightColor ↔ highlightColor
    ///   lockedColor ↔ brightLockedColor
    ///   unlockedColor ↔ brightUnlockedColor
    ///   defaultTopBarColor ↔ topBarColor
    ///   moduleColorSolidDefault ↔ moduleColorSolid
    ///
    /// ── 注意 ──
    ///   - 仅在自定义主题（OSTheme.Custom）激活时扫描主题 XML
    ///   - 预设文件在扩展加载时扫描一次并缓存
    ///   - 适用于主题 XML、PhaseSwift 配置、CustomTrial 配置
    ///   - 不适用于非 OS 字段（IRC 颜色、任务板颜色等）
    /// </summary>
    public static class CustomColorManager
    {
        // ========== 渐变段 ==========
        public struct GradientSegment
        {
            public Color Color;          // 此段的颜色
            public float Duration;       // 停留在此颜色的时长（秒）
            public float Transition;     // 渐变到下一色的时长（秒），默认 0
        }

        // ========== 颜色配置 ==========
        public class DynColorConfig
        {
            public enum Type { Rainbow, Gradient }
            public Type ColorType;
            public float Speed = 0.1f;
            public float Saturation = 1f;
            public float Value = 1f;
            public float Alpha = 1f;
            public List<GradientSegment> Segments = new(); // 多色渐变
        }

        // ========== 字段 ==========
        private static readonly Dictionary<string, DynColorConfig> _dynamicFields = new();
        private static readonly Dictionary<string, FieldInfo> _fieldCache = new();
        // 预设名匹配用 OrdinalIgnoreCase：与 ColorHelper 的 XNA 命名色表一致，
        // 避免“预设区分大小写 / 命名色忽略大小写”的分裂（见颜色解析统一方案）。
        private static readonly Dictionary<string, List<GradientSegment>> _presetCache =
            new(StringComparer.OrdinalIgnoreCase);
        private static string _lastScannedPath = null;
        private static bool _presetsLoaded = false;

        /// <summary>
        /// 静态解析的"没认出来"哨兵色。取 <c>Alpha = 0</c>：<see cref="ColorHelper.ParseStaticColor"/>
        /// 的所有成功路径都会把 alpha 置为 255（<c>R,G,B</c> 路径默认 255，各 hex 路径显式 255），
        /// 所以**任何合法写法都不可能解析出这个值**，用它比对即等价于"原样返回了 fallback"。
        /// </summary>
        private static readonly Color SentinelColor = new Color(0, 0, 0, 0);

        private static readonly Regex _fieldRegex = new(
            @"<(\w+)>\s*((?:LDTchara|RGB)" +
            @"(?::[\d.]+(?::[\d.]+)?(?::[\d.]+)?(?::[\d.]+)?)?" +
            @"|Gradient:(#[0-9A-Fa-f]+):(#[0-9A-Fa-f]+):([\d.]+)" +
            @"|(\w+)(?::([\d.]+))?(?::([\d.]+))?" +
            @")\s*</\1>",
            RegexOptions.IgnoreCase | RegexOptions.Compiled
        );

        // ========== 预设加载 ==========
        private static void EnsurePresetsLoaded()
        {
            if (_presetsLoaded) return;

            var extInfo = ExtensionLoader.ActiveExtensionInfo;
            if (extInfo == null) return;

            string colorDir = Path.Combine(extInfo.FolderPath, "CustomColor");
            if (!Directory.Exists(colorDir)) return;

            int loaded = 0;
            foreach (string file in Directory.GetFiles(colorDir, "*.xml"))
            {
                try
                {
                    string xml = File.ReadAllText(file);
                    var nameMatch = Regex.Match(xml, @"<Name>\s*(\w+)\s*</Name>");
                    if (!nameMatch.Success) continue;
                    string presetName = nameMatch.Groups[1].Value;

                    var segments = new List<GradientSegment>();
                    foreach (Match m in Regex.Matches(xml, @"<CustomColor\s+id=""(\d+)"">\s*<Color>\s*(#[0-9A-Fa-f]+|[\d,]+)\s*</Color>\s*<Duration>\s*([\d.]+)\s*</Duration>(?:\s*<Transition>\s*([\d.]+)\s*</Transition>)?\s*</CustomColor>"))
                    {
                        float dur = float.Parse(m.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture);
                        float trans = m.Groups[4].Success ? float.Parse(m.Groups[4].Value, System.Globalization.CultureInfo.InvariantCulture) : 0f;
                        segments.Add(new GradientSegment
                        {
                            Color = ColorHelper.ParseHexColor(m.Groups[2].Value),
                            Duration = dur,
                            Transition = trans
                        });
                    }

                    if (segments.Count >= 2)
                    {
                        _presetCache[presetName] = segments;
                        loaded++;
                    }
                }
                catch { }
            }

            _presetsLoaded = true;
            KELog.Info($"[CustomColor] loaded {loaded} presets, {_presetCache.Count} cached");
        }

        // ========== 主题更新驱动（由 Patches/CustomColorPatch 挂载调用） ==========
        public static void OnThemeUpdate()
        {
            if (ThemeManager.currentTheme != OSTheme.Custom && _dynamicFields.Count > 0)
            {
                _dynamicFields.Clear();
                _lastScannedPath = null;
            }

            EnsurePresetsLoaded();

            string currentPath = ThemeManager.LastLoadedCustomThemePath;
            if (currentPath != _lastScannedPath && ThemeManager.currentTheme == OSTheme.Custom)
            {
                _lastScannedPath = currentPath;
                _dynamicFields.Clear();
                ScanThemeFile(currentPath);
            }

            ApplyAllDynamicFields();
        }

        /// <summary>
        /// 统一的动态色入口（NONE/空=默认色；支持 CC 预设/渐变/彩虹/Hex/名称）。
        /// 由 ShowTitle/PhaseSwift 等颜色配置消费端调用。
        /// </summary>
        public static Color GetDynamicColor(string colorStr, Color defaultColor)
        {
            if (ConfigValue.IsNone(colorStr)) return defaultColor;
            var dynConfig = ParseColorString(colorStr);
            if (dynConfig != null)
                return CalcColor(dynConfig, OS.currentElapsedTime);
            // 动态未中 → 静态解析（#RGB/#RRGGBB/#AARRGGBB/R,G,B[,A]/XNA 命名色）
            return ColorHelper.ParseStaticColor(colorStr, defaultColor);
        }

        // ========== 扫描主题文件 ==========
        private static void ScanThemeFile(string themePath)
        {
            if (string.IsNullOrEmpty(themePath)) return;
            string fullPath = FindThemeFile(themePath);
            if (fullPath == null) return;

            string xml;
            try { xml = File.ReadAllText(fullPath); }
            catch { return; }

            foreach (Match match in _fieldRegex.Matches(xml))
            {
                string fieldName = match.Groups[1].Value;
                string matched = match.Groups[2].Value;

                // 1) 完整 Gradient 语法：组 3 非空即该候选分支整体命中（regex 已锚定三段）
                if (!string.IsNullOrEmpty(match.Groups[3].Value))
                {
                    var cfg = new DynColorConfig { ColorType = DynColorConfig.Type.Gradient };
                    cfg.Segments.Add(new GradientSegment
                    {
                        Color = ColorHelper.ParseHexColor(match.Groups[3].Value),
                        Duration = 0f
                    });
                    cfg.Segments.Add(new GradientSegment
                    {
                        Color = ColorHelper.ParseHexColor(match.Groups[4].Value),
                        Duration = float.TryParse(match.Groups[5].Value, out float d) ? d : 2f
                    });
                    _dynamicFields[fieldName] = cfg;
                    continue;
                }

                // 2) 预设优先：预设是扩展作者自己的资产，未中才回退内置关键字，
                //    否则名为 RGB / LDTchara 的预设永远轮不到内置分支。
                //    参数从值本身 Split（group7/8 只在内置关键字候选分支下才有值）。
                string[] parts2 = matched.Split(':');
                if (_presetCache.TryGetValue(parts2[0], out var presetSegments))
                {
                    var cfg = new DynColorConfig { ColorType = DynColorConfig.Type.Gradient };
                    float speedMul = 1f;
                    float alphaOverride = 1f;
                    if (parts2.Length > 1) float.TryParse(parts2[1], out speedMul);
                    if (parts2.Length > 2) float.TryParse(parts2[2], out alphaOverride);

                    foreach (var seg in presetSegments)
                    {
                        cfg.Segments.Add(new GradientSegment
                        {
                            Color = seg.Color,
                            Duration = seg.Duration * speedMul,
                            Transition = seg.Transition * speedMul
                        });
                        if (cfg.Segments.Count == 1) cfg.Alpha = alphaOverride;
                    }
                    _dynamicFields[fieldName] = cfg;
                    continue;
                }

                // 3) 内置彩虹关键字
                if (matched.StartsWith("LDTchara", StringComparison.OrdinalIgnoreCase) ||
                    matched.StartsWith("RGB", StringComparison.OrdinalIgnoreCase))
                {
                    var cfg = new DynColorConfig { ColorType = DynColorConfig.Type.Rainbow };
                    string[] parts = matched.Split(':');
                    if (parts.Length > 1) float.TryParse(parts[1], out cfg.Speed);
                    if (parts.Length > 2) float.TryParse(parts[2], out cfg.Alpha);
                    if (parts.Length > 3) float.TryParse(parts[3], out cfg.Saturation);
                    if (parts.Length > 4) float.TryParse(parts[4], out cfg.Value);
                    _dynamicFields[fieldName] = cfg;
                    continue;
                }

                // 4) 既不是预设、也不是内置关键字 —— 只在这个值**静态链也认不出**时才报。
                //    理由：XNA 命名色（Red）与 6 位 hex（FF0000）虽然也会落进本分支，
                //    但它们由静态链合法处理，报出来就是误报。用哨兵色判定：
                //    静态解析结果等于哨兵值 ⇒ 说明原样返回了 fallback ⇒ 三种写法全没认出来。
                //    这类静默跳过最难排查（颜色就是不变，且没有任何日志）。
                if (ColorHelper.ParseStaticColor(matched, SentinelColor) == SentinelColor)
                {
                    KELog.Warn($"[CustomColor] '{fullPath}': field '{fieldName}' value '{matched}' "
                        + "matched no preset and is not a built-in dynamic keyword "
                        + "(LDTchara / RGB / Gradient) - no colour applied."
                        + (matched.StartsWith("Rainbow", StringComparison.OrdinalIgnoreCase)
                           ? " Note: the built-in rainbow keyword was renamed 'Rainbow' -> 'RGB'."
                           : string.Empty));
                }
            }
        }

        // ========== 查找主题文件 ==========
        private static string FindThemeFile(string themePath)
        {
            if (string.IsNullOrEmpty(themePath)) return null;
            if (Path.IsPathRooted(themePath) && File.Exists(themePath)) return themePath;
            if (ExtensionLoader.ActiveExtensionInfo != null)
            {
                string inExt = KEPath.ResolveInsideExtension(themePath);
                if (inExt != null && File.Exists(inExt)) return inExt;
            }
            string inContent = Path.Combine("Content", themePath);
            if (File.Exists(inContent)) return inContent;
            if (File.Exists(themePath)) return themePath;
            return null;
        }

        // ========== 应用动态颜色 ==========
        private static void ApplyAllDynamicFields()
        {
            if (_dynamicFields.Count == 0) return;
            OS os = OS.currentInstance;
            if (os == null) return;

            Type osType = os.GetType();
            double time = OS.currentElapsedTime;

            foreach (var kv in _dynamicFields)
            {
                string fieldName = kv.Key;
                var cfg = kv.Value;
                Color color = CalcColor(cfg, time);
                ApplyField(os, osType, fieldName, color);
            }
        }

        internal static Color CalcColor(DynColorConfig cfg, double time)
        {
            if (cfg.ColorType == DynColorConfig.Type.Rainbow)
            {
                float hue = (float)(time * cfg.Speed) % 1.0f;
                Color c = ColorHelper.HSVToColor(hue, cfg.Saturation, cfg.Value);
                if (cfg.Alpha < 1f) c *= cfg.Alpha;
                return c;
            }

            // 多色渐变（停留 + 过渡两阶段）
            var segs = cfg.Segments;
            if (segs == null || segs.Count < 2) return Color.White;

            float totalDuration = segs.Sum(s => s.Duration + s.Transition);
            if (totalDuration <= 0f) return segs[0].Color;

            float t = (float)(time % totalDuration);
            float accumulated = 0f;
            for (int i = 0; i < segs.Count; i++)
            {
                int next = (i + 1) % segs.Count;

                // 停留阶段
                if (t < accumulated + segs[i].Duration)
                {
                    Color c = segs[i].Color;
                    if (cfg.Alpha < 1f) c *= cfg.Alpha;
                    return c;
                }
                accumulated += segs[i].Duration;

                // 过渡阶段
                if (t < accumulated + segs[i].Transition)
                {
                    float segT = (t - accumulated) / segs[i].Transition;
                    Color c = Color.Lerp(segs[i].Color, segs[next].Color, Math.Min(1f, segT));
                    if (cfg.Alpha < 1f) c *= cfg.Alpha;
                    return c;
                }
                accumulated += segs[i].Transition;
            }
            return segs.Last().Color;
        }

        /// <summary>
        /// 解析颜色字符串为 DynColorConfig，供外部（CustomTrialExe）判断是否为动态色。
        /// 返回 null 表示纯静态色。
        ///
        /// 入口先 Trim：动态关键字是**前缀/整串匹配**，不 Trim 的话 " RGB " 会一路落到静态链
        /// 解析失败（<see cref="ColorHelper.ParseStaticColor"/> 与命名色表都会 Trim，只有这里不会）。
        ///
        /// 分支顺序（预设优先）：完整 Gradient 语法 → 自定义预设 → 内置彩虹关键字。
        /// 预设是扩展作者自己的资产，未中才回退内置关键字（否则名为 RGB / LDTchara 的预设永远轮不到）；
        /// 完整 Gradient 语法排在最前是因为它是**语法**而非预设名（预设名不可能含 '#'），
        /// 先判它可保证扩展即使建了名为 Gradient 的预设也不会打断该语法。
        /// </summary>
        public static DynColorConfig ParseColorString(string input)
        {
            if (string.IsNullOrEmpty(input)) return null;
            input = input.Trim();
            if (input.Length == 0) return null;

            // 1. 完整 Gradient 语法（regex 自身已锚定 ^Gradient: 与结尾 $）
            var gm = Regex.Match(input,
                @"^Gradient:(#[0-9A-Fa-f]+):(#[0-9A-Fa-f]+):([\d.]+)$",
                RegexOptions.IgnoreCase);
            if (gm.Success)
            {
                var gcfg = new DynColorConfig { ColorType = DynColorConfig.Type.Gradient };
                gcfg.Segments.Add(new GradientSegment { Color = ColorHelper.ParseHexColor(gm.Groups[1].Value), Duration = 0f });
                gcfg.Segments.Add(new GradientSegment { Color = ColorHelper.ParseHexColor(gm.Groups[2].Value), Duration = float.Parse(gm.Groups[3].Value) });
                return gcfg;
            }

            // 2. 预设优先（未命中继续往下，不提前 return）
            EnsurePresetsLoaded();
            var parts2 = input.Split(':');
            if (_presetCache.TryGetValue(parts2[0], out var segs))
            {
                var cfg = new DynColorConfig { ColorType = DynColorConfig.Type.Gradient };
                float speedMul = 1f, alphaOverride = 1f;
                if (parts2.Length > 1) float.TryParse(parts2[1], out speedMul);
                if (parts2.Length > 2) float.TryParse(parts2[2], out alphaOverride);
                foreach (var seg in segs)
                    cfg.Segments.Add(new GradientSegment { Color = seg.Color, Duration = seg.Duration * speedMul, Transition = seg.Transition * speedMul });
                cfg.Alpha = alphaOverride;
                return cfg;
            }

            // 3. 内置彩虹关键字
            if (input.StartsWith("LDTchara", StringComparison.OrdinalIgnoreCase) ||
                input.StartsWith("RGB", StringComparison.OrdinalIgnoreCase))
            {
                var cfg = new DynColorConfig { ColorType = DynColorConfig.Type.Rainbow };
                string[] parts = input.Split(':');
                if (parts.Length > 1) float.TryParse(parts[1], out cfg.Speed);
                if (parts.Length > 2) float.TryParse(parts[2], out cfg.Alpha);
                if (parts.Length > 3) float.TryParse(parts[3], out cfg.Saturation);
                if (parts.Length > 4) float.TryParse(parts[4], out cfg.Value);
                return cfg;
            }

            return null; // 纯静态色
        }

        /// 注册动态颜色字段
        public static void RegisterDynamicField(string fieldName, DynColorConfig config)
        {
            _dynamicFields[fieldName] = config ?? new DynColorConfig();
        }

        /// 获取当前时刻某动态字段的颜色（每帧调用）
        public static Color GetCurrentColor(string fieldName)
        {
            if (_dynamicFields.TryGetValue(fieldName, out var cfg))
                return CalcColor(cfg, OS.currentElapsedTime);
            return Color.Transparent;
        }

        public static bool IsDynamicField(string fieldName)
            => _dynamicFields.ContainsKey(fieldName);

        private static void ApplyField(OS os, Type osType, string fieldName, Color color)
        {
            if (!_fieldCache.TryGetValue(fieldName, out var field))
            {
                field = osType.GetField(fieldName);
                _fieldCache[fieldName] = field;
            }
            if (field != null && field.FieldType == typeof(Color))
            {
                field.SetValue(os, color);
                ApplyAliasField(os, osType, fieldName, color);
            }
        }

        // ========== 别名映射 ==========
        private static void ApplyAliasField(OS os, Type osType, string fieldName, Color color)
        {
            string alias = null;
            if (fieldName == "defaultHighlightColor" && !_dynamicFields.ContainsKey("highlightColor"))
                alias = "highlightColor";
            else if (fieldName == "highlightColor" && !_dynamicFields.ContainsKey("defaultHighlightColor"))
                alias = "defaultHighlightColor";
            else if (fieldName == "lockedColor" && !_dynamicFields.ContainsKey("brightLockedColor"))
                alias = "brightLockedColor";
            else if (fieldName == "brightLockedColor" && !_dynamicFields.ContainsKey("lockedColor"))
                alias = "lockedColor";
            else if (fieldName == "unlockedColor" && !_dynamicFields.ContainsKey("brightUnlockedColor"))
                alias = "brightUnlockedColor";
            else if (fieldName == "brightUnlockedColor" && !_dynamicFields.ContainsKey("unlockedColor"))
                alias = "unlockedColor";
            else if (fieldName == "defaultTopBarColor" && !_dynamicFields.ContainsKey("topBarColor"))
                alias = "topBarColor";
            else if (fieldName == "topBarColor" && !_dynamicFields.ContainsKey("defaultTopBarColor"))
                alias = "defaultTopBarColor";
            else if (fieldName == "moduleColorSolidDefault" && !_dynamicFields.ContainsKey("moduleColorSolid"))
                alias = "moduleColorSolid";
            else if (fieldName == "moduleColorSolid" && !_dynamicFields.ContainsKey("moduleColorSolidDefault"))
                alias = "moduleColorSolidDefault";
            if (alias == null) return;
            var aliasField = osType.GetField(alias);
            if (aliasField != null && aliasField.FieldType == typeof(Color))
                aliasField.SetValue(os, color);

        }
        public static void ResetPresets()
        {
            _presetsLoaded = false;
            _presetCache.Clear();
            _dynamicFields.Clear();
            _fieldCache.Clear();
            _lastScannedPath = null;
        }

    }
}
