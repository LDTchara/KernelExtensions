using System.Collections.Generic;
using System.Xml.Serialization;

namespace KernelExtensions.Config
{
    /// <summary>
    /// 试炼配置的根对象，对应 XML 根元素 <TrialConfig>。
    /// </summary>
    [XmlRoot("TrialConfig")]
    public class TrialConfig
    {
        // ----- 全局配置 -----
        [XmlElement("SpinUpDuration")] public float SpinUpDuration = 13.8f;      // 旋转动画时长（秒）
        [XmlElement("EnableFlickering")] public bool EnableFlickering = true;    // 是否启用 UI 闪烁+节点消失特效
        [XmlElement("EnableMailIconDestroy")] public bool EnableMailIconDestroy = true; // 是否启用邮件图标爆炸特效
        [XmlElement("FlickeringDuration")] public float FlickeringDuration = 10f;      // 闪烁特效持续时间
        [XmlElement("MailIconDestroyDuration")] public float MailIconDestroyDuration = 3.82f; // 邮件图标爆炸持续时间
        [XmlElement("StartMusic")] public string StartMusic = null;              // 程序启动到点击按钮前的背景音乐
        [XmlElement("TrialStartMusic")] public string TrialStartMusic = null;    // 点击“开始试炼”后播放的音乐
        [XmlElement("EnableNodeDestruction")] public bool EnableNodeDestruction = true; // 是否启用节点摧毁（Flickering 期间）

        // ----- 阶段列表 -----
        [XmlArray("Phases"), XmlArrayItem("Phase")]
        public List<PhaseConfig> Phases = new List<PhaseConfig>();

        // ----- 最终完成动作 -----
        [XmlElement("OnComplete")] public ActionFileRef OnComplete = null;
    }

    /// <summary>
    /// 单个阶段的配置。
    /// </summary>
    public class PhaseConfig
    {
        [XmlAttribute("id")] public int Id;                     // 阶段编号（仅用于识别）
        [XmlElement("Title")] public string Title;              // 阶段标题（显示在屏幕中央）
        [XmlElement("Subtitle")] public string Subtitle;        // 阶段副标题
        [XmlElement("DescriptionText")] public string DescriptionText; // 描述文本（文件路径或直接文本）
        [XmlElement("MissionFile")] public string MissionFile;  // 任务 XML 文件路径（相对于扩展根目录）
        [XmlElement("Timeout")] public float Timeout = 0f;      // 任务超时时间（秒），0 表示无超时
        [XmlElement("Music")] public string Music = null;       // 本阶段专用背景音乐
        [XmlElement("OnComplete")] public ActionFileRef OnComplete; // 阶段完成时执行的动作文件
        [XmlElement("OnFail")] public ActionFileRef OnFail;     // 阶段失败时执行的动作文件
    }

    /// <summary>
    /// 动作文件引用，包含一个 file 属性。
    /// </summary>
    public class ActionFileRef
    {
        [XmlAttribute("file")] public string FilePath;          // 动作 XML 文件的路径（相对或绝对）
    }
}