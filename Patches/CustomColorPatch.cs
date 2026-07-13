using Hacknet;
using Hacknet.Extensions;
using HarmonyLib;
using KernelExtensions.Utility;
using Microsoft.Xna.Framework;
using System.Reflection;
using System.Text.RegularExpressions;

namespace KernelExtensions.Patches
{
    /// <summary>
    /// 自定义动态颜色系统。
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
    ///      Rainbow 是 LDTchara 的别名，语法相同。
    ///
    ///   2. 双色渐变（旧语法，兼容）
    ///      Gradient:#FF0000:#00FF00:2.0  红→蓝渐变，2 秒循环
    ///
    ///   3. 预设引用（推荐）
    ///      Riptide                     引用 CustomColor/Riptide.xml
    ///      Riptide:0.5                 速度 x0.5
    ///      Riptide:0.5:0.8            速度 x0.5 + 透明度 80%
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
    [HarmonyPatch]
    public static class CustomColorPatch
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
        private static readonly Dictionary<string, List<GradientSegment>> _presetCache = new();
        private static string _lastScannedPath = null;
        private static bool _presetsLoaded = false;

        private static readonly Regex _fieldRegex = new(
            @"<(\w+)>\s*((?:LDTchara|Rainbow)" +
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
                            Color = ColorUtils.ParseHexColor(m.Groups[2].Value),
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
            KELog.Debug($"[CustomColor] loaded {loaded} presets, {_presetCache.Count} cached");
        }

        // ========== ThemeManager.Update Prefix ==========
        [HarmonyPrefix]
        [HarmonyPatch(typeof(ThemeManager), nameof(ThemeManager.Update))]
        static void ThemeUpdatePrefix()
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

                // 彩虹色
                if (matched.StartsWith("LDTchara", StringComparison.OrdinalIgnoreCase) ||
                    matched.StartsWith("Rainbow", StringComparison.OrdinalIgnoreCase))
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

                // 渐变（兼容旧两色语法）
                if (matched.StartsWith("Gradient:", StringComparison.OrdinalIgnoreCase))
                {
                    var cfg = new DynColorConfig { ColorType = DynColorConfig.Type.Gradient };
                    cfg.Segments.Add(new GradientSegment
                    {
                        Color = ColorUtils.ParseHexColor(match.Groups[3].Value),
                        Duration = 0f
                    });
                    cfg.Segments.Add(new GradientSegment
                    {
                        Color = ColorUtils.ParseHexColor(match.Groups[4].Value),
                        Duration = float.TryParse(match.Groups[5].Value, out float d) ? d : 2f
                    });
                    _dynamicFields[fieldName] = cfg;
                    continue;
                }

                // 预设名
                string presetName = match.Groups[6].Value;
                List<GradientSegment> presetSegments = null;
                bool found = !string.IsNullOrEmpty(presetName) && _presetCache.TryGetValue(presetName, out presetSegments);
;
                if (found)
                {
                    var cfg = new DynColorConfig { ColorType = DynColorConfig.Type.Gradient };
                    float speedMul = 1f;
                    float alphaOverride = 1f;
                    if (match.Groups[7].Success) float.TryParse(match.Groups[7].Value, out speedMul);
                    if (match.Groups[8].Success) float.TryParse(match.Groups[8].Value, out alphaOverride);

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
                string inExt = Path.Combine(ExtensionLoader.ActiveExtensionInfo.FolderPath, themePath);
                if (File.Exists(inExt)) return inExt;
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
                Color c = ColorUtils.HSVToColor(hue, cfg.Saturation, cfg.Value);
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

        /// 解析颜色字符串为 DynColorConfig，供外部（CustomTrialExe）判断是否为动态色。
        /// 返回 null 表示纯静态色。
        public static DynColorConfig ParseColorString(string input)
        {
            if (string.IsNullOrEmpty(input)) return null;

            // 彩虹色
            if (input.StartsWith("LDTchara", StringComparison.OrdinalIgnoreCase) ||
                input.StartsWith("Rainbow", StringComparison.OrdinalIgnoreCase))
            {
                var cfg = new DynColorConfig { ColorType = DynColorConfig.Type.Rainbow };
                string[] parts = input.Split(':');
                if (parts.Length > 1) float.TryParse(parts[1], out cfg.Speed);
                if (parts.Length > 2) float.TryParse(parts[2], out cfg.Alpha);
                if (parts.Length > 3) float.TryParse(parts[3], out cfg.Saturation);
                if (parts.Length > 4) float.TryParse(parts[4], out cfg.Value);
                return cfg;
            }

            // 渐变
            if (input.StartsWith("Gradient:", StringComparison.OrdinalIgnoreCase))
            {
                var m = Regex.Match(input,
                    @"^Gradient:(#[0-9A-Fa-f]+):(#[0-9A-Fa-f]+):([\d.]+)$",
                    RegexOptions.IgnoreCase);
                if (m.Success)
                {
                    var cfg = new DynColorConfig { ColorType = DynColorConfig.Type.Gradient };
                    cfg.Segments.Add(new GradientSegment { Color = ColorUtils.ParseHexColor(m.Groups[1].Value), Duration = 0f });
                    cfg.Segments.Add(new GradientSegment { Color = ColorUtils.ParseHexColor(m.Groups[2].Value), Duration = float.Parse(m.Groups[3].Value) });
                    return cfg;
                }
                return null;
            }

            // 预设名
            EnsurePresetsLoaded();
            var parts2 = input.Split(':');
            string presetName = parts2[0];
            if (_presetCache.TryGetValue(presetName, out var segs))
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
