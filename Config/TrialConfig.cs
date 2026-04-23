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
        [XmlElement("ProgramName")] public string ProgramName = "CustomTrial";       // 自定义程序名称
        [XmlElement("SpinUpDuration")] public float SpinUpDuration = 13.8f;          // 旋转动画时长（秒）
        [XmlElement("EnableFlickering")] public bool EnableFlickering = true;        // 是否启用 UI 闪烁+节点消失特效
        [XmlElement("EnableMailIconDestroy")] public bool EnableMailIconDestroy = true; // 是否启用邮件图标爆炸特效
        [XmlElement("FlickeringDuration")] public float FlickeringDuration = 10f;    // 闪烁特效持续时间
        [XmlElement("MailIconDestroyDuration")] public float MailIconDestroyDuration = 3.82f; // 邮件爆炸持续时间
        [XmlElement("EnableNodeDestruction")] public bool EnableNodeDestruction = true; // 是否启用节点摧毁（Flickering 期间）
        [XmlElement("StartMusic")] public string StartMusic = null;                  // 程序启动到点击按钮前的背景音乐
        [XmlElement("TrialStartMusic")] public string TrialStartMusic = null;        // 点击“开始试炼”后播放的音乐
        [XmlElement("OnStart")] public ActionFileRef OnStart = null;                 // 点击“开始试炼”后立即执行的动作
        [XmlElement("OnAnimationComplete")] public ActionFileRef OnAnimationComplete = null; // 所有动画完成后执行的动作

        // ==================== 自定义颜色（可选，不设置则使用当前主题色） ====================
        [XmlElement("BackgroundColor")] public string BackgroundColor = null;        // 程序窗口背景颜色（支持名称/#RRGGBB/机密）
        [XmlElement("GlobalTimerColor")] public string GlobalTimerColor = null;      // 全局进度条颜色
        [XmlElement("PhaseTimerColor")] public string PhaseTimerColor = null;        // 阶段进度条颜色
        [XmlElement("SpinUpColor")] public string SpinUpColor = null;                // 旋转动画颜色

        // ==================== 内存缩减配置（进入破解阶段后动态降低 RAM 占用） ====================
        /// <summary>
        /// 进入破解阶段（即所有动画结束后）延迟多少秒开始缩减内存，默认 5 秒。
        /// </summary>
        [XmlElement("RamReductionDelay")] public float RamReductionDelay = 5f;
        /// <summary>
        /// 缩减内存所需的总秒数，默认 3 秒。在此时间内，ramCost 从初始值线性减少到目标值 88。
        /// </summary>
        [XmlElement("RamReductionDuration")] public float RamReductionDuration = 3f;
        /// <summary>
        /// 是否启用动态内存缩减：根据窗口内实际控件高度自动计算所需 ramCost，取代固定目标值。
        /// 默认 false（使用固定目标值 96）。若为 true，则 TargetRamCost 无效。
        /// </summary>
        [XmlElement("DynamicRamReduction")] public bool DynamicRamReduction = false;

        // ==================== 全局计时器 ====================
        [XmlElement("GlobalTimeout")] public float GlobalTimeout = 0f;               // 全局超时秒数，0=无限制
        [XmlElement("EnableGlobalTimer")] public bool EnableGlobalTimer = false;     // 是否显示全局倒计时条
        [XmlElement("OnGlobalFail")] public ActionFileRef OnGlobalFail = null;       // 全局超时时执行的动作文件

        // ==================== 邮件摧毁阶段聚焦遮罩 ====================
        /// <summary>
        /// 是否在邮件图标爆炸期间，将终端以外的区域变暗以聚焦注意力。默认 true。
        /// </summary>
        [XmlElement("MailPhaseDarkenEnabled")] public bool MailPhaseDarkenEnabled = true;

        // ==================== 阶段开始/完成时的终端聚焦特效 ====================
        /// <summary>
        /// 阶段开始时是否显示终端聚焦特效（遮罩变暗 + 边框扩散）。默认 true。
        /// </summary>
        [XmlElement("EnablePhaseStartFocus")] public bool EnablePhaseStartFocus = true;

        /// <summary>
        /// 试炼全部完成时是否显示终端聚焦特效（遮罩变暗 + 边框扩散）。默认 true。
        /// </summary>
        [XmlElement("EnableTrialCompleteFocus")] public bool EnableTrialCompleteFocus = true;

        // ==================== 主题切换配置 ====================
        [XmlElement("ThemeToSwitch")] public string ThemeToSwitch = null;            // 要切换的主题（预设名或自定义主题文件路径）
        [XmlElement("ThemeFlickerDuration")] public float ThemeFlickerDuration = 2f; // 主题切换时的闪烁时长

        // ==================== 节点摧毁后等待时间 ====================
        [XmlElement("PostDestructionDelay")] public float PostDestructionDelay = 0f; // 摧毁完成后、邮件爆炸前的等待秒数

        // ==================== 阶段计时器显示开关 ====================
        [XmlElement("EnablePhaseTimer")] public bool EnablePhaseTimer = true;        // 是否显示阶段倒计时条（默认 true）

        // ==================== 阶段列表 ====================
        [XmlArray("Phases"), XmlArrayItem("Phase")]
        public List<PhaseConfig> Phases = new();

        // ==================== 最终完成动作 ====================
        [XmlElement("OnComplete")] public ActionFileRef OnComplete = null;           // 所有阶段完成后执行的动作
        [XmlElement("OutroText")] public string OutroText = null;                    // 试炼完成时显示在终端的描述文本（支持文件路径或内嵌文本）
        [XmlElement("ConnectTarget")] public string ConnectTarget = null;            // 试炼完成后连接的目标节点 ID
        [XmlElement("StopMusicOnConnect")] public bool StopMusicOnConnect = true;    // 转连前是否停止音乐，默认 true
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
        [XmlElement("ResetText")] public string ResetText = null;   // 重置时额外显示的文本（支持文件路径或内嵌）
        [XmlElement("ExecuteOnPhaseStartOnReset")] public bool ExecuteOnPhaseStartOnReset = false;   // 重置阶段时是否再次执行 OnPhaseStart 动作
    }

    /// <summary>
    /// 动作文件引用，包含一个 file 属性。
    /// </summary>
    public class ActionFileRef
    {
        [XmlAttribute("file")] public string FilePath;          // 动作 XML 文件的路径（相对或绝对）
    }
}