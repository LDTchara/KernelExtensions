using System.Collections.Generic;
using System.Xml.Serialization;

namespace KernelExtensions.Config
{
    [XmlRoot("VMAttackConfig")]
    public class VMAttackConfig
    {
        [XmlElement("ConfigName")] public string ConfigName;

        // 解除模式：FileDeletion（删除文件）、FileExists（文件存在）、Password（密码）
        [XmlElement("Mode")] public RecoveryMode Mode;

        // 密码模式时需要的密码
        [XmlElement("Password")] public string Password;

        /// <summary>密码模式下是否显示“帮助”按钮，用于打开终端和帮助文件</summary>
        [XmlElement("EnableHelpButton")] public bool EnableHelpButton = false;

        // 自定义错误消息（替换原版的 VMBootloaderTrap.dll）
        [XmlElement("ErrorMessage")] public string ErrorMessage = "ERROR: Critical boot error loading \"VMBootloaderTrap.dll\"";

        // 相对扩展根目录的 txt 文件
        [XmlArray("SystemLogFiles"), XmlArrayItem("File")]public List<string> SystemLogFiles;

        // 两个日志文件之间的停顿秒数
        [XmlElement("SystemLogPauseBetween")]public float SystemLogPauseBetween = 2f; 

        // 引导文本（支持 % 停顿）
        [XmlArray("GuideText"), XmlArrayItem("Line")] public List<string> GuideText;

        // 显示引导文本时执行的动作文件（相对于扩展根目录）
        [XmlElement("ActionOnGuideTextStart")] public string ActionOnGuideTextStart;

        // 再进入恢复界面时若已读过一遍引导文本则是否跳过0.2秒输出
        [XmlElement("EnableGuideReadFlag")] public bool EnableGuideReadFlag = false;

        // 按钮文本
        [XmlElement("ButtonText")] public string ButtonText = "Proceed";

        //自定义帮助文件（相对于扩展根目录）
        [XmlElement("HelpFile")] public string HelpFile;

        // 成功恢复后播放的音乐
        [XmlElement("SuccessMusic")] public string SuccessMusic;

        // 虚假文件列表
        [XmlArray("FakeFiles"), XmlArrayItem("File")] public List<FakeFileInfo> FakeFiles;

        // 文件检测相关的目标路径（相对于存档目录）
        [XmlElement("CheckFilePath")] public string CheckFilePath;
        // 可选的内容正则匹配（文件存在模式）
        [XmlElement("CheckFilePattern")] public string CheckFilePattern;
    }

    public enum RecoveryMode
    {
        FileDeletion,
        FileExists,
        Password
    }

    public class FakeFileInfo
    {
        [XmlAttribute("Path")] public string Path;
        [XmlAttribute("Size")] public long Size;
        [XmlAttribute("Source")] public string Source; // 可选，从扩展复制文件
    }
}