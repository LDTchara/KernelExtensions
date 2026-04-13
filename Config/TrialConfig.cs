using System.Collections.Generic;
using System.Xml.Serialization;

namespace KernelExtensions.Config
{
    [XmlRoot("TrialConfig")]
    public class TrialConfig
    {
        [XmlElement("SpinUpDuration")] public float SpinUpDuration = 13.8f;
        [XmlElement("EnableFlickering")] public bool EnableFlickering = true;
        [XmlElement("EnableMailIconDestroy")] public bool EnableMailIconDestroy = true;
        [XmlElement("StartMusic")] public string StartMusic = null;
        [XmlElement("TrialStartMusic")] public string TrialStartMusic = null;

        [XmlArray("Phases"), XmlArrayItem("Phase")]
        public List<PhaseConfig> Phases = new List<PhaseConfig>();

        [XmlElement("OnComplete")] public ActionFileRef OnComplete = null;
    }

    public class PhaseConfig
    {
        [XmlAttribute("id")] public int Id;
        [XmlElement("Title")] public string Title;
        [XmlElement("Subtitle")] public string Subtitle;
        [XmlElement("DescriptionText")] public string DescriptionText;
        [XmlElement("MissionFile")] public string MissionFile;
        [XmlElement("Timeout")] public float Timeout = 0f;
        [XmlElement("Music")] public string Music = null;
        [XmlElement("OnComplete")] public ActionFileRef OnComplete;
        [XmlElement("OnFail")] public ActionFileRef OnFail;
    }

    public class ActionFileRef
    {
        [XmlAttribute("file")] public string FilePath;
    }
}