using Hacknet;
using Hacknet.Extensions;
using KernelExtensions.Configs;
using KernelExtensions.Managers;
using KernelExtensions.Modules;
using Pathfinder.Action;
using Pathfinder.Util;
using KernelExtensions.Utilities;

namespace KernelExtensions.Actions.VMAttack
{
    public class LaunchVMAttackAction : KEAction
    {
        /// <summary>
        /// 配置文件路径，**相对于扩展根目录**（例：VMATK/MyAttack.xml）。
        /// 不再需要单独的 ConfigName —— 路径本身就是身份，感染 flag 也由它推导。
        /// </summary>
        [XMLStorage]
        public string ConfigPath;

        public override void Trigger(OS os)
        {
            string relativePath = VMInfectionManager.NormalizeRelativePath(ConfigPath);
            if (relativePath == null)
            {
                KELog.Error("[LaunchVMAttack] ConfigPath required (relative to the extension folder, e.g. VMATK/MyAttack.xml).");
                return;
            }

            // 配置加载路径：扩展根目录 + 相对路径（越界则拒绝）
            string configPath = KEPath.ResolveInsideExtension(relativePath);
            if (configPath == null)
            {
                KELog.Error($"[LaunchVMAttack] ConfigPath escapes the extension folder: {relativePath}");
                return;
            }
            if (!File.Exists(configPath))
            {
                KELog.Error($"[LaunchVMAttack] Config not found: {configPath}");
                return;
            }

            VMAttackConfig config;
            var serializer = new System.Xml.Serialization.XmlSerializer(typeof(VMAttackConfig));
            using (var fs = new FileStream(configPath, FileMode.Open))
            {
                config = (VMAttackConfig)serializer.Deserialize(fs);
            }

            // 记录来源路径：感染 flag 与引导标记都以它作标识
            config.SourcePath = relativePath;
            // 新增：立即保存至 CurrentConfig，覆盖旧配置
            VMInfectionManager.CurrentConfig = config;

            // 生成虚假文件（路径均需落在各自的根目录内，越界则跳过不写）
            foreach (var f in config.FakeFiles)
            {
                // 如果 Path 为空则跳过
                if (string.IsNullOrEmpty(f.Path)) continue;
                string filePath = KEPath.ResolveInsideSaveBase(f.Path);
                if (filePath == null)
                {
                    KELog.Warn($"[LaunchVMAttack] FakeFiles Path escapes the save directory, skipped: {f.Path}");
                    continue;
                }
                // 确保目录存在
                string dir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                string source = string.IsNullOrEmpty(f.Source) ? null : KEPath.ResolveInsideExtension(f.Source);
                if (source != null && File.Exists(source))
                {
                    File.Copy(source, filePath, true);
                }
                else
                {
                    if (!string.IsNullOrEmpty(f.Source) && source == null)
                        KELog.Warn($"[LaunchVMAttack] FakeFiles Source escapes the extension folder, writing an empty file instead: {f.Source}");
                    File.WriteAllBytes(filePath, new byte[f.Size]);
                }
            }

            // 添加 Flag（由相对路径推导，可无损反解回配置文件）
            os.Flags.AddFlag(VMInfectionManager.BuildInfectionFlag(relativePath));
            // 保存
            os.threadedSaveExecute(true);
            // ====== 模拟原版 systakeover 的崩溃前特效 ======
            PostProcessor.EndingSequenceFlashOutActive = true;
            PostProcessor.EndingSequenceFlashOutPercentageComplete = 1f;

            // 计算延迟时间，与原版一致
            var now = DateTime.Now;
            // 注意：原版用 now 记录了 Execute 开始时间，我们用同样的方法
            // 实际上，可以直接使用一个固定的小延迟（原版约 1.5~4秒）
            double num = (DateTime.Now - now).TotalSeconds;
            if (num > 3.0)
                num = 1.5;
            else
                num = 4.0 - num;

            os.delayer.Post(ActionDelayer.Wait(num), () =>
            {
                PostProcessor.EndingSequenceFlashOutActive = false;
                PostProcessor.EndingSequenceFlashOutPercentageComplete = 0f;
                // 触发崩溃（而不是 rebootThisComputer）
                os.thisComputer.crash(os.thisComputer.ip);
            });
        }
    }
}