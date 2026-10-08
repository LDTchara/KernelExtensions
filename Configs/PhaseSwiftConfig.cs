using System.Xml.Serialization;

namespace KernelExtensions.Configs
{
    [XmlRoot("PhaseSwiftConfig")]
    public class PhaseSwiftConfig
    {
        [XmlElement("ProgramName")] public string ProgramName = "PhaseSwift";
        [XmlElement("BackgroundColor")] public string BackgroundColor = null;
        [XmlElement("DefaultFadeDuration")] public float DefaultFadeDuration = 1.5f;
        [XmlElement("ThemeFlickerDuration")] public float ThemeFlickerDuration = 0.8f;
        [XmlElement("InitialScene")] public int InitialScene = 0;
        [XmlElement("ChangeLayout")] public bool ChangeLayout = false;

        [XmlElement("StartButtonText")] public string StartButtonText = "开始";
        [XmlElement("ShiftButtonText")] public string ShiftButtonText = "Shift";
        [XmlElement("ShowSceneNumber")] public bool ShowSceneNumber = true;
        [XmlElement("CompleteText")] public string CompleteText = null;
        [XmlElement("FinishMode")] public string FinishMode = "none";
        /// <summary>Stop 时的拓扑处理：restore（缺省，恢复 PS 启动时备份的原始链接）/ scene_N（原始链接 + 场景 N 的 Topology）/ merge（清除受控节点间链接后合并全部场景拓扑）。</summary>
        [XmlElement("TopologyMode")] public string TopologyMode = "restore";
        [XmlElement("UseDualTrackMusic")] public bool UseDualTrackMusic = true;
        [XmlElement("RestoreThemeOnStop")] public bool RestoreThemeOnStop = true;
        [XmlElement("SingleTrack")] public string SingleTrack = null;

        [XmlElement("GlobalDiscovery")] public bool GlobalDiscovery = true;

        [XmlArray("MusicPhases"), XmlArrayItem("Phase")]
        public List<PhaseSwiftMusicPhase> MusicPhases = new();
        [XmlArray("Scenes"), XmlArrayItem("Scene")]
        public List<PhaseSwiftScene> Scenes = new();
    }

    public class PhaseSwiftMusicPhase
    {
        [XmlAttribute("id")] public int Id;
        [XmlArray("Tracks"), XmlArrayItem("Track")]
        public List<PhaseSwiftTrack> Tracks = new();
    }

    /// <summary>
    /// 音轨条目。路径写在元素文本里（&lt;Track&gt;Music/a.ogg&lt;/Track&gt;，与旧写法完全兼容），
    /// 循环点/音调/音量作为可选属性。
    ///
    /// ⚠️ 四个数值属性**故意声明为 string**：XmlSerializer 反序列化 float 属性时，
    /// 遇到不可解析的内容（如 LoopEnd="abc"）会**直接抛异常**，导致**整份配置加载失败**。
    /// 收成 string 后由本类自行解析，写错一个属性只影响该属性，不会连累整个配置。
    /// 合法值语义见 AGENTS.md「负数/无效值约定（9.55）」：负数或 NaN/Infinity 一律回退默认。
    /// </summary>
    public class PhaseSwiftTrack
    {
        /// <summary>扩展根目录下的相对路径（含文件名）。</summary>
        [XmlText] public string Path;
        /// <summary>循环起点（秒）。缺省/空/非数字/负数 = 0（文件开头）。</summary>
        [XmlAttribute("LoopStart")] public string LoopStart;
        /// <summary>循环终点（秒）。缺省/空/非数字/负数 = 整曲末尾。</summary>
        [XmlAttribute("LoopEnd")] public string LoopEnd;
        /// <summary>音调/速度倍率：范围 [-1, 1]，0 = 原速（底层 AL_PITCH = 2^pitch，变速必变调）。</summary>
        [XmlAttribute("Pitch")] public string Pitch;
        /// <summary>单曲音量倍率。缺省/空/非数字/负数 = 1（不衰减）。</summary>
        [XmlAttribute("Volume")] public string Volume;

        /// <summary>解析“非负数值”属性：未写 / 空 / 非数字 / 负数 / NaN / Infinity 一律返回 null（= 用默认）。</summary>
        private static float? ParseNonNegative(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            if (!float.TryParse(raw.Trim(), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float v)) return null;
            if (float.IsNaN(v) || float.IsInfinity(v) || v < 0f) return null;
            return v;
        }

        /// <summary>循环起点（秒）；null = 用默认（0）。</summary>
        public float? LoopStartSeconds => ParseNonNegative(LoopStart);
        /// <summary>循环终点（秒）；null = 用默认（整曲）。</summary>
        public float? LoopEndSeconds => ParseNonNegative(LoopEnd);
        /// <summary>单曲音量倍率；null = 用默认（1）。</summary>
        public float? VolumeMultiplier => ParseNonNegative(Volume);

        /// <summary>Pitch 允许负数（范围 [-1, 1]），所以单独解析；非法值回退 0。</summary>
        public float PitchValue
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Pitch)) return 0f;
                if (!float.TryParse(Pitch.Trim(), System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out float v)) return 0f;
                if (float.IsNaN(v) || float.IsInfinity(v)) return 0f;
                return v > 1f ? 1f : (v < -1f ? -1f : v);
            }
        }
    }

    public class PhaseSwiftScene
    {
        [XmlAttribute("id")] public int Id;
        [XmlElement("Theme")] public string Theme;
        [XmlElement("OnSwitch")] public ActionFileRef OnSwitch = null;
        [XmlArray("StartNodes"), XmlArrayItem("Node")] public List<PhaseSwiftNodeRef> StartNodes = new();
        [XmlArray("VisibleNodes"), XmlArrayItem("Node")] public List<PhaseSwiftNodeRef> VisibleNodes = new();
        [XmlArray("Topology"), XmlArrayItem("Link")] public List<PhaseSwiftLink> Topology = new();
        [XmlArray("BlockedNodes"), XmlArrayItem("Node")] public List<string> BlockedNodes = new();
    }

    public class PhaseSwiftNodeRef
    {
        [XmlAttribute("id")] public string Id;
    }

    public class PhaseSwiftLink
    {
        [XmlAttribute("from")] public string From;
        [XmlAttribute("to")] public string To;
    }
}
