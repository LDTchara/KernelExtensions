using Hacknet.Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace KernelExtensions.Utility
{
    /// <summary>
    /// 读取扩展根目录的 KE-Config.xml。
    /// 文件不存在时自动生成带注释的模板，字段缺失时回退默认值。
    /// </summary>
    public static class KEConfigLoader
    {
        public static bool Debug { get; private set; }
        public static bool SkipVanillaIRCLogs { get; private set; }
        public static List<string> CustomImages { get; private set; } = new();

        /// <summary>
        /// 加载 KE-Config.xml。每次 OSLoad 时都可调用以支持热重载。
        /// </summary>
        public static void Load()
        {
            // 每次读取前重置（热重载用）
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
                catch (Exception ex) { Console.WriteLine($"[KE] 无法创建 KE-Config.xml: {ex.Message}"); }
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
                    Debug = db;
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
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[KE] KE-Config.xml 解析失败，使用默认值: {ex.Message}");
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

    <!-- 调试模式开关。开发时开，发布前关。 -->
    <!-- <Debug>false</Debug> -->

    <!-- 是否跳过原版 BashLogs.txt 的 IRC 日志（只加载 CustomIRCLogs.txt） -->
    <!-- 默认 false（原版日志 + 自定义日志同时加载） -->
    <!-- <SkipVanillaIRCLogs>false</SkipVanillaIRCLogs> -->

    <!-- 自定义图标图片路径列表（每行一条），用于 SetNodeIcon Action -->
    <!-- 以扩展根目录为基准，建议尺寸 128x128 -->
    <!-- 写入的值会自动注册为 @文件名 前缀，供 SetNodeIcon 引用 -->
    <!-- <CustomImages> -->
    <!--     <Image>Images/MyIcon.png</Image> -->
    <!--     <Image>Images/AnotherIcon.png</Image> -->
    <!-- </CustomImages> -->
</KEConfig>";
        }
    }
}
