using System.Xml.Serialization;

namespace KernelExtensions.Configs
{
    [XmlRoot("VMAttackConfig")]
    public class VMAttackConfig
    {
        // 旧的 ConfigName 字段已移除：配置文件现在由 LaunchVMAttack 的相对路径直接定位，
        // 感染 flag 与引导标记都以该相对路径为标识（见下方 SourcePath）。

        /// <summary>
        /// 【运行时字段，不参与 XML】本配置从扩展目录下的哪个相对路径加载而来。
        /// 由 LaunchVMAttack 与崩溃后的重建逻辑填入；用于生成感染 flag 与引导标记。
        /// </summary>
        [XmlIgnore] public string SourcePath;

        // 解除模式：FileDeletion（删除文件）、FileExists（文件存在）、Password（密码）
        [XmlElement("Mode")] public RecoveryMode Mode;

        // 密码模式时需要的密码
        [XmlElement("Password")] public string Password;

        /// <summary>
        /// 【已拆分，仅作兼容保留】旧的总开关。
        /// 为 true 时等价于同时开启 <see cref="EnableHelpDocButton"/> 与 <see cref="EnableTerminalButton"/>（旧行为：
        /// 那个单一按钮既开记事本又开终端）。示例配置不再写出此项，新内容请用下面两个新开关。
        /// </summary>
        [XmlElement("EnableHelpButton")] public bool EnableHelpButton = false;

        /// <summary>密码模式：是否显示「帮助文档」按钮（Windows 弹记事本 / Unix 追加到界面文本区）。</summary>
        [XmlElement("EnableHelpDocButton")] public bool EnableHelpDocButton = false;

        /// <summary>密码模式：是否显示「终端」按钮。</summary>
        [XmlElement("EnableTerminalButton")] public bool EnableTerminalButton = false;

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
        // 可选的内容校验：参考文件路径（相对扩展目录），比对两者内容是否完全一致
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