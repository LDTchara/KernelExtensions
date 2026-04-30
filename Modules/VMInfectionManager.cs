using Hacknet;
using Hacknet.Extensions;
using KernelExtensions.Config;
using KernelExtensions.Utility;
using System.IO;

namespace KernelExtensions.Modules
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

        /// <summary>
        /// 根据存档目录判断文件是否满足配置要求。
        /// </summary>
        public static bool CheckFileCondition(OS os, VMAttackConfig config)
        {
            if (string.IsNullOrEmpty(config.CheckFilePath)) return false;
            string fullPath = Path.Combine(HostileHackerBreakinSequence.GetBaseDirectory(), config.CheckFilePath);
            if (config.Mode == RecoveryMode.FileDeletion) return File.Exists(fullPath);
            if (config.Mode == RecoveryMode.FileExists) return !File.Exists(fullPath);
            return false;
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

            // 移除感染 Flag
            string flag = os.Flags.GetFlagStartingWith("Kernel_VMInfected_");
            if (!string.IsNullOrEmpty(flag))
            {
                os.Flags.RemoveFlag(flag);
                os.threadedSaveExecute(true);   // 立即保存
            }

            // 清理已读标记
            string guideReadFlag = "Kernel_VMGuideRead_" + CurrentConfig.ConfigName;
            if (os.Flags.HasFlag(guideReadFlag))
                os.Flags.RemoveFlag(guideReadFlag);

            // 清理引导动作完成 Flag
            string guideActionDoneFlag = "Kernel_VMGuideActionDone_" + CurrentConfig.ConfigName;
            if (os.Flags.HasFlag(guideActionDoneFlag))
                os.Flags.RemoveFlag(guideActionDoneFlag);

            // 删除虚假文件
            string baseDir = HostileHackerBreakinSequence.GetBaseDirectory();
            if (CurrentConfig.FakeFiles != null)
            {
                foreach (var f in CurrentConfig.FakeFiles)
                {
                    if (string.IsNullOrEmpty(f.Path)) continue;
                    string filePath = Path.Combine(baseDir, f.Path);
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