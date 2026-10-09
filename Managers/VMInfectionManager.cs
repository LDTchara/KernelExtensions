using Hacknet;
using Hacknet.Extensions;
using KernelExtensions.Configs;
using KernelExtensions.Modules;
using KernelExtensions.Utilities;
// 傻逼PowerShell给我注释全爆了，孩子们记住永远不要用ps1改东西
namespace KernelExtensions.Managers
{
    /// <summary>
    /// 全局 VM 攻击状态管理器，负责存储配置、控制恢复界面激活标志、执行清理。
    /// </summary>
    public static class VMInfectionManager
    {
        /// <summary>当前生效的 VM 攻击配置，null 表示无攻击。</summary>
        public static VMAttackConfig CurrentConfig;
        /// <summary>是否正处于崩溃后的恢复界面阶段（由 CrashModule 补丁激活）。</summary>
        public static bool ShowRecovery;
        /// <summary>恢复模块实例（由 OSDraw 补丁使用）。</summary>
        public static FakeRecoveryModule RecoveryModule;

        /// <summary>感染 flag 前缀。</summary>
        public const string InfectionFlagPrefix = "Kernel_VMInfected_";

        /// <summary>
        /// 把配置的相对路径归一化：反斜杠统一为正斜杠、去掉开头的 "./" 与 "/"、去首尾空白。
        /// 失败/空返回 null。
        /// </summary>
        public static string NormalizeRelativePath(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath)) return null;
            string p = relativePath.Trim().Replace('\\', '/');
            while (p.StartsWith("./")) p = p.Substring(2);
            p = p.TrimStart('/');
            return p.Length == 0 ? null : p;
        }

        /// <summary>
        /// 由配置相对路径生成感染 flag。
        /// 例：VMATK/MyAttack.xml → Kernel_VMInfected_VMATK/MyAttack.xml
        /// <para>
        /// **刻意保留斜杠与 ".xml" 后缀**，使 flag 与路径一一对应、可无损反解。
        /// 若把斜杠换成 "_"，"VMATK/A_B.xml" 与 "VMATK/A/B.xml" 会归一化成同一个 flag；
        /// 而崩溃后只能靠 flag 找回配置文件，撞名会让两者互相误判为「已感染」。
        /// flag 是普通字符串（Flags 就是个 List&lt;string&gt;，存档为 XML 文本），
        /// 斜杠在 XML 文本内容中无需转义，可安全使用。
        /// </para>
        /// </summary>
        public static string BuildInfectionFlag(string relativePath)
        {
            string p = NormalizeRelativePath(relativePath);
            return p == null ? null : InfectionFlagPrefix + p;
        }

        /// <summary>
        /// 由感染 flag 反解出配置文件的完整路径（与 <see cref="BuildInfectionFlag"/> 互逆）。
        /// 新格式：flag 后缀就是相对路径；旧格式（无斜杠的纯名字）回退到 VMATK/&lt;name&gt;.xml。
        /// 找不到文件返回 null。
        /// </summary>
        public static string ResolveConfigPathFromFlag(string flag)
        {
            if (string.IsNullOrEmpty(flag) || !flag.StartsWith(InfectionFlagPrefix)) return null;
            string suffix = flag.Substring(InfectionFlagPrefix.Length);
            string extRoot = ExtensionLoader.ActiveExtensionInfo?.FolderPath?.Replace('\\', '/');
            if (string.IsNullOrEmpty(extRoot)) return null;

            // 新格式：flag 后缀即相对路径（越界则拒绝）
            string direct = KEPath.ResolveInsideExtension(suffix);
            if (direct != null && File.Exists(direct)) return direct;

            // 旧格式兼容：Kernel_VMInfected_&lt;ConfigName&gt; → VMATK/&lt;ConfigName&gt;.xml
            string legacy = KEPath.ResolveInsideExtension("VMATK/" + suffix + ".xml");
            return legacy != null && File.Exists(legacy) ? legacy : null;
        }

        /// <summary>
        /// 当前配置的标识（用于引导已读 / 引导动作完成等标记）。
        /// 取归一化后的 SourcePath；为空则退化为空串（标记仍能工作，只是不分配置）。
        /// </summary>
        public static string ConfigId(VMAttackConfig config)
        {
            return NormalizeRelativePath(config?.SourcePath) ?? "";
        }

        /// <summary>
        /// 清除存档里**所有**感染 flag（可能因历史残留而存在多个）。返回清除数量。
        /// <para>
        /// 为何不是 GetFlagStartingWith + RemoveFlag 一次：那个组合只拿得到**第一个**。
        /// 清理函数自身应当完备，不应依赖“任何时刻只有一个 flag”这个由调用方维持的不变量。
        /// </para>
        /// </summary>
        public static int ClearAllInfectionFlags(OS os)
        {
            if (os?.Flags == null) return 0;
            int removed = 0;
            string flag;
            while (!string.IsNullOrEmpty(flag = os.Flags.GetFlagStartingWith(InfectionFlagPrefix)))
            {
                os.Flags.RemoveFlag(flag);
                removed++;
                if (removed > 64) break;   // 防御：异常情况下不陷入死循环
            }
            return removed;
        }

        /// <summary>
        /// 已经告警过的越界路径（去重）。
        /// <para>
        /// 本判定在崩溃期间**每帧**都会跑（见 CrashModule 的 Update 补丁）：越界时条件不满足、
        /// Prefix 放行让原版继续，于是同一份越界会被反复判定（实测刷 16~35 条），
        /// 足以淹没其它日志。同一路径只报一次即可，由 <see cref="ResetEscapeWarnings"/> 在每次攻击开始时清空。
        /// </para>
        /// </summary>
        private static readonly HashSet<string> _warnedEscapePaths = new();

        /// <summary>清空越界告警去重表（在每次 VM 攻击启动时调用）。</summary>
        public static void ResetEscapeWarnings() => _warnedEscapePaths.Clear();

        /// <summary>
        /// 根据存档目录判断文件是否满足配置要求。
        /// </summary>
        public static bool CheckFileCondition(OS os, VMAttackConfig config)
        {
            if (ConfigValue.IsNone(config.CheckFilePath)) return false;
            string fullPath = KEPath.ResolveInsideSaveBase(config.CheckFilePath);
            // 越界视为「条件不满足」——保守处理，不误判为可恢复
            if (fullPath == null)
            {
                if (_warnedEscapePaths.Add(config.CheckFilePath))
                    KELog.Warn($"[VMInfection] CheckFilePath escapes the save directory: {config.CheckFilePath}");
                return false;
            }
            if (config.Mode == RecoveryMode.FileDeletion) return File.Exists(fullPath);
            if (config.Mode == RecoveryMode.FileExists)
            {
                if (!File.Exists(fullPath)) return true;
                // CheckFilePattern：文件内容必须与扩展目录下的参考文件一致
                return !FileContentMatches(fullPath, config);
            }
            return false;
        }

        private static bool FileContentMatches(string targetPath, VMAttackConfig config)
        {
            if (ConfigValue.IsNone(config.CheckFilePattern)) return true;
            string refPath = KEPath.ResolveInsideExtension(config.CheckFilePattern);
            if (refPath == null || !File.Exists(refPath)) return false;
            return FilesMatch(targetPath, refPath);
        }

        private static bool FilesMatch(string pathA, string pathB)
        {
            try
            {
                byte[] a = File.ReadAllBytes(pathA);
                byte[] b = File.ReadAllBytes(pathB);
                if (a.Length != b.Length) return false;
                for (int i = 0; i < a.Length; i++)
                    if (a[i] != b[i]) return false;
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 激活恢复界面（从 CrashModule 补丁调用）。
        /// </summary>
        public static void ActivateRecovery(OS os, VMAttackConfig config)
        {
            ShowRecovery = true;
            RecoveryModule = new FakeRecoveryModule(
                new Microsoft.Xna.Framework.Rectangle(0, 0, os.fullscreen.Width, os.fullscreen.Height),
                os, config);
            RecoveryModule.LoadContent();
            RecoveryModule.visible = true;

            // 禁用其他模块交互
            os.display.visible = false;
            os.ram.visible = false;
            os.netMap.visible = false;
            os.terminal.visible = false;
            os.DisableTopBarButtons = true;
            os.DisableEmailIcon = true;
            os.inputEnabled = false;

            // 确保游戏循环运行（否则模块不会更新）
            os.canRunContent = true;
            os.bootingUp = false;

            // 注入模块，使其自动更新和绘制
            os.modules.Add(RecoveryModule);
        }

        /// <summary>
        /// 清除攻击状态并恢复正常（密码正确或文件条件满足时调用）。
        /// </summary>
        public static void Recover(OS os)
        {
            if (CurrentConfig == null) return;

            // 移除感染 Flag（清所有，不只第一个）
            if (ClearAllInfectionFlags(os) > 0)
                os.threadedSaveExecute(true);   // 立即保存

            // 清理已读标记
            string configId = ConfigId(CurrentConfig);
            string guideReadFlag = "Kernel_VMGuideRead_" + configId;
            if (os.Flags.HasFlag(guideReadFlag))
                os.Flags.RemoveFlag(guideReadFlag);

            // 清理引导动作完成 Flag
            string guideActionDoneFlag = "Kernel_VMGuideActionDone_" + configId;
            if (os.Flags.HasFlag(guideActionDoneFlag))
                os.Flags.RemoveFlag(guideActionDoneFlag);

            // 删除虚假文件（同样先做越界校验，避免误删存档目录之外的东西）
            if (CurrentConfig.FakeFiles != null)
            {
                foreach (var f in CurrentConfig.FakeFiles)
                {
                    if (string.IsNullOrEmpty(f.Path)) continue;
                    string filePath = KEPath.ResolveInsideSaveBase(f.Path);
                    if (filePath == null)
                    {
                        KELog.Warn($"[VMInfection] FakeFiles Path escapes the save directory, not deleting: {f.Path}");
                        continue;
                    }
                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                        // 尝试删除空目录（可选）
                        string dir = Path.GetDirectoryName(filePath);
                        if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any())
                            Directory.Delete(dir);
                    }
                }
            }

            // 移除恢复模块，防止重启后再次触发
            if (RecoveryModule != null)
            {
                os.modules.Remove(RecoveryModule);
                RecoveryModule = null;
            }

            // 启用其他模块交互
            os.display.visible = true;
            os.ram.visible = true;
            os.netMap.visible = true;
            os.terminal.visible = true;
            os.DisableTopBarButtons = false;
            os.DisableEmailIcon = false;
            os.inputEnabled = true;

            ShowRecovery = false;
            // CurrentConfig 予以保留，供重启后播放音乐
        }
    }
}