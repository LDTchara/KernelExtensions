using Hacknet.Extensions;
using System.Xml.Linq;
using KernelExtensions.Utilities;
using KernelExtensions.CustomPortPublicCracker;
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
        public static List<string> CustomImages { get; private set; } = new();
        public static Dictionary<string, UPSConfig> UPSConfigs { get; private set; } = new();///Protocol => Obj(UPSConfings)

        /// <summary>
        /// 加载 KE-Config.xml。每次 OSLoad 时都可调用以支持热重载。
        /// </summary>
        public static void Load()
        {
            // 每次读取前重置
            Debug = false;
            SkipVanillaIRCLogs = false;
            CustomImages = new List<string>();
            UPSConfigs = new Dictionary<string, UPSConfig>();
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

                var UPS = rootEl.Element("UPS");
                if (UPS != null)
                {
                    // 解析所有 <port> 子元素
                    var portElements = UPS.Elements("port");
                    foreach (var port in portElements)
                    {
                        // 读取 protocol，用作字典 Key
                        string protocol = GetAttributeValue(port, "protocol");
                        if (string.IsNullOrEmpty(protocol))
                        {
                            KELog.Warn($"Skipping <port> with missing or empty 'protocol' attribute.");
                            continue;
                        }

                        // 解析单个 port 为 UPSConfig
                        var config = new UPSConfig
                        {
                            Protocol = protocol,
                            Time = GetAttributeFloat(port, "time", 0f),
                            Title = GetAttributeValue(port, "title"),
                            RamCost = GetRamCostAttribute(port),
                            Useable = (OS.currentInstance?.Flags.HasFlag(protocol) ?? false) ? false : GetAttributeBool(port, "useable", true),
                            Debuffs = UPSConfig.ResolveAnyBuffsList(GetAttributeValue(port, "debuff"), false),
                            Buffs = UPSConfig.ResolveAnyBuffsList(GetAttributeValue(port, "buff"), true),
                            RequireOpenPortsBefore = GetPortsBeforeAttribute(port),

                            RequireSolveFirewall = GetAttributeBool(port, "SolveFirewallBefor", false),
                            RequireProxyOverload = GetAttributeBool(port, "OverloadProxyBefore", false)
                        };

                        // 存入字典（若有重复 protocol，后覆盖前）
                        UPSConfigs[protocol] = config;
                        if (Debug)
                            KELog.Info($"Loaded UPS config for protocol: {protocol}");
                    }
                }





            }
            catch (Exception ex)
            {
                KELog.Warn($"KE-Config.xml parse failed, using defaults: {ex.Message}");
            }
        }
        // ---------- 私有辅助方法 ----------
        private static string GetAttributeValue(XElement element, string attrName)
        {
            var attr = element.Attribute(attrName);
            return attr == null ? string.Empty : attr.Value.Trim().Trim('"');
        }

        private static float GetAttributeFloat(XElement element, string attrName, float defaultValue)
        {
            var attr = element.Attribute(attrName);
            if (attr == null) return defaultValue;
            string val = attr.Value.Trim().Trim('"');
            if (float.TryParse(val, System.Globalization.NumberStyles.Float,
                               System.Globalization.CultureInfo.InvariantCulture, out float result))
                return result;
            return defaultValue;
        }

        private static int GetAttributeInt(XElement element, string attrName, int defaultValue)
        {
            var attr = element.Attribute(attrName);
            if (attr == null) return defaultValue;
            string val = attr.Value.Trim().Trim('"');
            if (int.TryParse(val, out int result))
                return result;
            return defaultValue;
        }

        private static bool GetAttributeBool(XElement element, string attrName, bool defaultValue)
        {
            var attr = element.Attribute(attrName);
            if (attr == null) return defaultValue;
            string val = attr.Value.Trim().Trim('"').ToLowerInvariant();
            return val switch
            {
                "true" or "1" or "yes" => true,
                "false" or "0" or "no" => false,
                _ => defaultValue
            };
        }

        private static List<string> GetAttributeStringList(XElement element, string attrName)
        {
            var attr = element.Attribute(attrName);
            if (attr == null) return new List<string>();
            string val = attr.Value.Trim().Trim('"');
            if (string.IsNullOrEmpty(val)) return new List<string>();
            return val.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                      .Select(s => s.Trim())
                      .ToList();
        }

        /// <summary>读取前置开放端口列表，兼容历史两种写法：OpenPortsBefore（模板/文档）与 OpenPortBefore。</summary>
        private static List<string> GetPortsBeforeAttribute(XElement element)
        {
            var list = GetAttributeStringList(element, "OpenPortsBefore");
            if (list.Count == 0)
                list = GetAttributeStringList(element, "OpenPortBefore");
            return list;
        }

        /// <summary>读取破解内存占用，兼容历史两种写法：ramcost（模板/文档）与 ram。</summary>
        private static int GetRamCostAttribute(XElement element)
        {
            var attr = element.Attribute("ramcost") ?? element.Attribute("ram");
            if (attr == null) return 0;
            string val = attr.Value.Trim().Trim('"');
            return int.TryParse(val, out int result) ? result : 0;
        }

        private static List<int> GetAttributeIntList(XElement element, string attrName)
        {
            var attr = element.Attribute(attrName);
            if (attr == null) return new List<int>();
            string val = attr.Value.Trim().Trim('"');
            if (string.IsNullOrEmpty(val)) return new List<int>();
            return val.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                      .Select(s => int.TryParse(s.Trim(), out int n) ? n : 0)
                      .ToList();
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
    <!-- 自定义端口破解器配置器 仅前四个必填-->
    <UPS>
        <port>
        protocol=""aaa"" 
        time=""12"" 
        ramcost = ""200""
        title=""loading......"" 
        debuff=""name1,n2...."" 
        buff=""..."" 
        OpenPortsBefore=""ptcls"" 
        SolveFirewallBefor=""true"" 
        OverloadProxyBefore=""true""
        </port>
    </UPS>
</KEConfig>";
        }
    }
}
