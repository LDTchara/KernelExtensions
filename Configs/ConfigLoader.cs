using Hacknet.Extensions;
using System.Xml.Linq;
using KernelExtensions.Utilities;
using Hacknet;

namespace KernelExtensions.Configs
{
    /// <summary>
    /// 读取扩展根目录的 KE-Config.xml。
    /// 文件不存在时自动生成带注释的模板，字段缺失时回退默认值。
    /// </summary>
    public static class ConfigLoader
    {
        public static bool Debug { get; private set; }
        public static bool SkipVanillaIRCLogs { get; private set; }

        /// <summary>
        /// 额外加载的 IRC 日志文件（相对扩展根目录）。空列表 = 回退到默认文件名
        /// <c>CustomIRCLogs.txt</c>（见 <c>IRCLogInjector</c>）。
        /// </summary>
        public static List<string> CustomIRCLogFiles { get; private set; } = new();

        /// <summary>主菜单水印（“+ KernelExtensions &lt;版本&gt;”）是否显示；默认 true。KE-Config.xml 的 &lt;Watermark&gt; 控制。</summary>
        public static bool Watermark { get; private set; } = true;
        public static List<string> CustomImages { get; private set; } = new();

        /// <summary>
        /// 加载 KE-Config.xml。每次 OSLoad 时都可调用以支持热重载。
        /// </summary>
        public static void Load()
        {
            // 每次读取前重置
            Debug = false;
            SkipVanillaIRCLogs = false;
            CustomIRCLogFiles = new List<string>();
            Watermark = true;
            CustomImages = new List<string>();
            var extInfo = ExtensionLoader.ActiveExtensionInfo;
            if (extInfo == null) return;

            string root = extInfo.FolderPath.Replace('\\', '/');
            string cfgPath = Path.Combine(root, "KE-Config.xml");

            // 文件不存在 → 生成默认模板
            if (!File.Exists(cfgPath))
            {
                try { File.WriteAllText(cfgPath, GetDefaultTemplate()); }
                catch (Exception ex) { KELog.Warn($"failed to create KE-Config.xml: {ex.Message}"); }
                return; // 新生成的模板全是注释，全部使用默认值 
            }

            // 文件存在 → 解析
            try
            {
                var doc = XDocument.Load(cfgPath);
                var rootEl = doc.Root;
                if (rootEl == null) return;

                var skip = rootEl.Element("SkipVanillaIRCLogs");
                var dbg = rootEl.Element("Debug");
                if (dbg != null && bool.TryParse(dbg.Value, out bool db))
                {
                    Debug = db;
                    if (Debug)
                        KELog.WarnT("Debug mode enabled - disable before release!");
                }
                if (skip != null && bool.TryParse(skip.Value, out bool sv))
                    SkipVanillaIRCLogs = sv;

                // 额外 IRC 日志文件列表（相对扩展根；越界条目跳过并 Warn，不中断其余）
                var ircs = rootEl.Element("CustomIRCLogs");
                if (ircs != null)
                {
                    CustomIRCLogFiles = ircs.Elements("File")
                        .Select(e => e.Value.Trim())
                        .Where(v => !string.IsNullOrEmpty(v))
                        .Where(v =>
                        {
                            if (KEPath.ResolveInsideExtension(v) != null) return true;
                            KELog.WarnT($"[IRC] CustomIRCLogs entry escapes the extension folder, skipped: '{v}'");
                            return false;
                        })
                        .ToList();
                }

                var wm = rootEl.Element("Watermark");
                if (wm != null && bool.TryParse(wm.Value, out bool wmv))
                    Watermark = wmv;

                var images = rootEl.Element("CustomImages");
                if (images != null)
                {
                    CustomImages = images.Elements("Image")
                        .Select(e => e.Value.Trim())
                        .Where(v => !string.IsNullOrEmpty(v))
                        .ToList();
                }

                // 禁用用户名段（dev1 用户名系统 XML 化，热重载）
                UsernameProfiles.Apply(rootEl.Element("BannedUsernames"));





            }
            catch (Exception ex)
            {
                KELog.WarnT($"KE-Config.xml parse failed, using defaults: {ex.Message}");
            }
        }
        private static string GetDefaultTemplate()
        {
            return @"<?xml version=""1.0"" encoding=""UTF-8"" ?>
<KEConfig>
    <!-- ==========================================================
         KernelExtensions 扩展级配置
         此文件可选。不写或删除此文件时全部回退到默认值。
         修改后重启游戏生效（OSLoad 时重新读取）。
         ========================================================== -->

    <!-- 调试模式，true=开启，false=关闭（发布前关） -->
    <Debug>false</Debug>

    <!-- 是否跳过原版 BashLogs.txt IRC 日志。false=原版 + 自定义都加载；true=只加载下面列出的自定义日志。
         注意：true 且一个自定义日志都没有（也没有 CustomIRCLogs.txt）时，会强制回退原版日志并告警——
         空 FileEntry 列表会让游戏加载随机文件节点时崩溃。 -->
    <SkipVanillaIRCLogs>false</SkipVanillaIRCLogs>

    <!-- ============ 额外 IRC 日志文件（可选） ============ -->
    <!-- 会在原版 BashLogs.txt 之外，再往文件浏览器里塞若干个 IRC 日志条目。
         路径相对于扩展根目录，不得用 .. 逃出扩展目录（越界条目会被跳过并记 Warn，不影响其余）。
         不写本段（或一个 <File> 都不写）时，回退到默认文件名 CustomIRCLogs.txt。 -->

    <!-- ── 文件格式 ──
         每条日志以「行首 #」开始，例：

             # 话题标题
             第 1 行正文
             第 2 行正文
             # 另一个话题
             正文……

         · 话题标题里可以写「标题|来源」——竖线右边成为条目末尾的 Archived Via；
           不写竖线时来源取扩展名。
         · 标题里若出现 - [X] 标记会被去掉、所有空格也会去掉，
           然后前加 IRC_Log: 作为文件浏览器里的文件名。 -->
    <CustomIRCLogs>
        <!--
        <File>News/Archive1.txt</File>
        <File>News/Archive2.txt</File>
        -->
    </CustomIRCLogs>

    <!-- 主菜单水印（+ KernelExtensions 版本号，彩虹流动 + 逐字晃动）。true=显示（默认），false=隐藏 -->
    <Watermark>true</Watermark>

    <!-- 自定义图标图片列表，用于 SetNodeIcon Action（自动注册为 @文件名） -->
    <!-- 以扩展根目录为基准，建议尺寸 128x128 -->
    <CustomImages>
        <!--
        <Image>Images/MyIcon.png</Image>
        <Image>Images/AnotherIcon.png</Image>
        -->
    </CustomImages>

    <!-- ============ 禁用用户名（可选，不写则无禁用） ============ -->
    <!-- 创建新账号时拦截禁用用户名并显示原因。
         Reason=直接原因；ReasonBlock=从下方 Reasons 块随机选一条；同名 Ban 多条=多原因随机。 -->
    <BannedUsernames>
        <!--
        <Reasons>
            <Block Name=""Test1"">
                <Reason>该名称已被占用，再试一遍也没用。</Reason>
            </Block>
        </Reasons>
        <Ban Name=""admin"" Reason=""保留用户名"" />
        <Ban Name=""root"" ReasonBlock=""Test1"" />
        -->
    </BannedUsernames>
</KEConfig>";
        }
    }
}
