using Hacknet.Extensions;
using KernelExtensions.Utilities;
using System.Xml.Linq;

namespace KernelExtensions.Configs
{
    /// <summary>
    /// 自定义结局配置（EndingConfig）。
    /// 来源：StartEnding 的 File 属性指向的独立结局文件（根元素 &lt;Ending&gt;）。
    /// 字段语义（字符串配置遵循 NONE 约定：NONE/空 = 该字段默认）：
    ///   Title/EndingText/OnCreditMusic/AfterMusic/AfterAction —— 文本与音乐，空 = 用原版/不执行
    ///   SpeechFile/TextFile/CreditsFile —— 资源路径（相对扩展根任意子目录），NONE/空 = 默认 Docs/ 下
    ///   SpeechTime（float，默认 -1）：
    ///     -1/缺省 —— 有语音跟随音频时长；无语音静默 30s 兜底（Warn）
    ///      0      —— 跳过演讲阶段，直接进入报幕
    ///      &gt;0    —— 演讲上限 N 秒：音频先播完则提前进报幕，N 先到则截断
    /// </summary>
    public class EndingConfig
    {
        // ===== 结局画面 =====
        public string Title = "Hacknet";
        public string EndingText = "Thanks For Playing";

        // ===== 音乐（空 = 原版 Music\Bit(Ending)）=====
        public string OnCreditMusic = "";
        public string AfterMusic = "";

        // ===== 收尾 =====
        public string AfterAction = ""; // NONE/空 = 不执行

        // ===== 资源路径（相对扩展根；NONE/空 = 默认 Docs/ 下）=====
        public string SpeechFile = "Docs/EndingSpeech.wav";
        public string TextFile = "Docs/Speech.txt";
        public string CreditsFile = "Docs/CreditsData.txt";

        // ===== 演讲计时（默认 -1 = 跟随音频时长；0 = 跳过演讲；&gt;0 = 上限秒数）=====
        public float SpeechTime = -1f;

        // ===== 默认资源路径（NONE/空回退目标）=====
        private const string DefaultSpeechFile = "Docs/EndingSpeech.wav";
        private const string DefaultTextFile = "Docs/Speech.txt";
        private const string DefaultCreditsFile = "Docs/CreditsData.txt";

        /// <summary>从独立结局 XML（&lt;Ending&gt; 根）加载。缺失/解析失败返回 null（调用方报错）。</summary>
        public static EndingConfig Load(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)) return null;
            string full = Path.Combine(ExtensionLoader.ActiveExtensionInfo?.FolderPath ?? "", filePath);
            if (!File.Exists(full))
            {
                KELog.Error($"[Ending] config file not found: {full}");
                return null;
            }

            try
            {
                var doc = XDocument.Load(full);
                var root = doc.Root;
                if (root == null || root.Name.LocalName != "Ending")
                {
                    KELog.Error($"[Ending] '{full}' has no <Ending> root element");
                    return null;
                }

                var cfg = new EndingConfig();
                cfg.Title = GetString(root, "Title", cfg.Title);
                cfg.EndingText = GetString(root, "EndingText", cfg.EndingText);
                cfg.OnCreditMusic = GetString(root, "OnCreditMusic", cfg.OnCreditMusic);
                cfg.AfterMusic = GetString(root, "AfterMusic", cfg.AfterMusic);
                cfg.AfterAction = GetString(root, "AfterAction", cfg.AfterAction);
                cfg.SpeechFile = ResolvePath(root, "SpeechFile", DefaultSpeechFile);
                cfg.TextFile = ResolvePath(root, "TextFile", DefaultTextFile);
                cfg.CreditsFile = ResolvePath(root, "CreditsFile", DefaultCreditsFile);
                cfg.SpeechTime = GetFloat(root, "SpeechTime", -1f);
                return cfg;
            }
            catch (Exception ex)
            {
                KELog.Error($"[Ending] config parse failed ({full}): {ex.Message}");
                return null;
            }
        }

        // ===== 解析辅助 =====

        private static string GetString(XElement root, string attr, string fallback)
        {
            var v = (string)root.Attribute(attr);
            return ConfigValue.IsNone(v) ? fallback : v.Trim();
        }

        private static string ResolvePath(XElement root, string attr, string defaultPath)
        {
            var v = (string)root.Attribute(attr);
            return ConfigValue.IsNone(v) ? defaultPath : v.Trim();
        }

        private static float GetFloat(XElement root, string attr, float fallback)
        {
            var v = (string)root.Attribute(attr);
            return float.TryParse(v, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float f) ? f : fallback;
        }
    }
}
