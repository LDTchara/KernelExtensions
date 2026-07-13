using System.Xml.Serialization;

namespace KernelExtensions.Config
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
        [XmlElement("UseDualTrackMusic")] public bool UseDualTrackMusic = true;
        [XmlElement("RestoreThemeOnStop")] public bool RestoreThemeOnStop = true;
        [XmlElement("SingleTrack")] public string SingleTrack = null;

        [XmlArray("MusicPhases"), XmlArrayItem("Phase")]
        public List<PhaseSwiftMusicPhase> MusicPhases = new();
        [XmlArray("Scenes"), XmlArrayItem("Scene")]
        public List<PhaseSwiftScene> Scenes = new();
    }

    public class PhaseSwiftMusicPhase
    {
        [XmlAttribute("id")] public int Id;
        [XmlArray("Tracks"), XmlArrayItem("Track")]
        public List<string> Tracks = new();
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