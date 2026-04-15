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
        // ==================== 全局配置 ====================
        [XmlElement("SpinUpDuration")] public float SpinUpDuration = 13.8f;          // 旋转动画时长（秒）
        [XmlElement("EnableFlickering")] public bool EnableFlickering = true;        // 是否启用 UI 闪烁+节点消失特效
        [XmlElement("EnableMailIconDestroy")] public bool EnableMailIconDestroy = true; // 是否启用邮件图标爆炸特效
        [XmlElement("FlickeringDuration")] public float FlickeringDuration = 10f;    // 闪烁特效持续时间
        [XmlElement("MailIconDestroyDuration")] public float MailIconDestroyDuration = 3.82f; // 邮件爆炸持续时间
        [XmlElement("EnableNodeDestruction")] public bool EnableNodeDestruction = true; // 是否启用节点摧毁（Flickering 期间）
        [XmlElement("StartMusic")] public string StartMusic = null;                  // 程序启动到点击按钮前的背景音乐
        [XmlElement("TrialStartMusic")] public string TrialStartMusic = null;        // 点击“开始试炼”后播放的音乐

        // ==================== 自定义颜色（可选，不设置则使用当前主题色） ====================
        [XmlElement("BackgroundColor")] public string BackgroundColor = null;        // 程序窗口背景颜色（支持名称/#RRGGBB/机密）
        [XmlElement("GlobalTimerColor")] public string GlobalTimerColor = null;      // 全局进度条颜色
        [XmlElement("PhaseTimerColor")] public string PhaseTimerColor = null;        // 阶段进度条颜色
        [XmlElement("SpinUpColor")] public string SpinUpColor = null;                // 旋转动画颜色

        // ==================== 全局计时器 ====================
        [XmlElement("GlobalTimeout")] public float GlobalTimeout = 0f;               // 全局超时秒数，0=无限制
        [XmlElement("EnableGlobalTimer")] public bool EnableGlobalTimer = false;     // 是否显示全局倒计时条
        [XmlElement("OnGlobalFail")] public ActionFileRef OnGlobalFail = null;       // 全局超时时执行的动作文件

        // ==================== 阶段计时器显示开关 ====================
        [XmlElement("EnablePhaseTimer")] public bool EnablePhaseTimer = true;        // 是否显示阶段倒计时条（默认 true）

        // ==================== 试炼开始时执行的动作 ====================
        [XmlElement("OnStart")] public ActionFileRef OnStart = null;                 // 点击“开始试炼”后立即执行的动作

        // ==================== 阶段列表 ====================
        [XmlArray("Phases"), XmlArrayItem("Phase")]
        public List<PhaseConfig> Phases = new List<PhaseConfig>();

        // ==================== 最终完成动作 ====================
        [XmlElement("OnComplete")] public ActionFileRef OnComplete = null;           // 所有阶段完成后执行的动作
    }

    /// <summary>
    /// 单个阶段的配置。
    /// </summary>
    public class PhaseConfig
    {
        [XmlAttribute("id")] public int Id;                     // 阶段编号（仅用于识别）
        [XmlElement("Title")] public string Title;              // 阶段标题（显示在窗口中央）
        [XmlElement("Subtitle")] public string Subtitle;        // 阶段副标题
        [XmlElement("DescriptionText")] public string DescriptionText; // 描述文本（文件或内嵌，支持 % 和 %% 停顿）
        [XmlElement("MissionFile")] public string MissionFile;  // 任务 XML 文件路径（相对于扩展根目录）
        [XmlElement("Timeout")] public float Timeout = 0f;      // 阶段超时秒数，0=无限制
        [XmlElement("Music")] public string Music = null;       // 本阶段专用背景音乐
        [XmlElement("OnPhaseStart")] public ActionFileRef OnPhaseStart; // 阶段开始时执行的动作文件（新增）
        [XmlElement("OnComplete")] public ActionFileRef OnComplete; // 阶段完成时执行的动作文件
        [XmlElement("OnFail")] public ActionFileRef OnFail;     // 阶段失败时执行的动作文件
        [XmlElement("EnableResetOnFail")] public bool EnableResetOnFail = false; // 失败后是否重置当前阶段
    }

    /// <summary>
    /// 动作文件引用，包含一个 file 属性。
    /// </summary>
    public class ActionFileRef
    {
        [XmlAttribute("file")] public string FilePath;          // 动作 XML 文件的路径（相对或绝对）
    }
}