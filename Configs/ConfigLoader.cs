using Hacknet.Extensions;
using System.Xml.Linq;
using KernelExtensions.Utilities;

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
        public static List<string> CustomImages { get; private set; } = new();

        /// <summary>
        /// 加载 KE-Config.xml。每次 OSLoad 时都可调用以支持热重载。
        /// </summary>
        public static void Load()
        {
            // 每次读取前重置
            Debug = false;
            SkipVanillaIRCLogs = false;
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
                        KELog.Warn("Debug mode enabled - disable before release!");
                }
                if (skip != null && bool.TryParse(skip.Value, out bool sv))
                    SkipVanillaIRCLogs = sv;

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
                KELog.Warn($"KE-Config.xml parse failed, using defaults: {ex.Message}");
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

    <!-- 是否跳过原版 BashLogs.txt IRC 日志，只加载 CustomIRCLogs.txt。false=同时加载 -->
    <SkipVanillaIRCLogs>false</SkipVanillaIRCLogs>

    <!-- 自定义图标图片列表，用于 SetNodeIcon Action（自动注册为 @文件名） -->
    <!-- 以扩展根目录为基准，建议尺寸 128x128 -->
    <CustomImages>
        <Image>Images/MyIcon.png</Image>
        <Image>Images/AnotherIcon.png</Image>
    </CustomImages>

    <!-- ============ 禁用用户名（可选，不写则无禁用） ============ -->
    <!-- 创建新账号时拦截禁用用户名并显示原因。
         Reason=直接原因；ReasonBlock=从下方 Reasons 块随机选一条；同名 Ban 多条=多原因随机。 -->
    <BannedUsernames>
        <Reasons>
            <Block Name=""Test1"">
                <Reason>该名称已被占用，再试一遍也没用。</Reason>
            </Block>
        </Reasons>
        <Ban Name=""admin"" Reason=""保留用户名"" />
        <Ban Name=""root"" ReasonBlock=""Test1"" />
    </BannedUsernames>
</KEConfig>";
        }
    }
}
