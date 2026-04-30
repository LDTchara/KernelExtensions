using Hacknet;
using Hacknet.Extensions;
using KernelExtensions.Config;
using KernelExtensions.Modules;
using Pathfinder.Action;
using Pathfinder.Util;
using Pathfinder.Util.XML;
using System.IO;

namespace KernelExtensions.Actions
{
    public class LaunchVMAttackAction : DelayablePathfinderAction
    {
        [XMLStorage]
        public string ConfigName;

        public override void Trigger(OS os)
        {
            if (string.IsNullOrEmpty(ConfigName))
            {
                Console.WriteLine("[KernelExtensions] LaunchVMAttack: ConfigName required.");
                return;
            }

            // 配置加载路径
            string configPath = Path.Combine(ExtensionLoader.ActiveExtensionInfo.FolderPath, "VMATK", ConfigName + ".xml");
            if (!File.Exists(configPath))
            {
                Console.WriteLine($"[KernelExtensions] LaunchVMAttack: Config not found: {configPath}");
                return;
            }

            VMAttackConfig config;
            var serializer = new System.Xml.Serialization.XmlSerializer(typeof(VMAttackConfig));
            using (var fs = new FileStream(configPath, FileMode.Open))
            {
                config = (VMAttackConfig)serializer.Deserialize(fs);
            }

            // 新增：立即保存至 CurrentConfig，覆盖旧配置
            VMInfectionManager.CurrentConfig = config;

            // 生成虚假文件
            string baseDir = HostileHackerBreakinSequence.GetBaseDirectory();
            foreach (var f in config.FakeFiles)
            {
                // 如果 Path 为空则跳过
                if (string.IsNullOrEmpty(f.Path)) continue;
                string filePath = Path.Combine(baseDir, f.Path);
                // 确保目录存在
                string dir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                if (!string.IsNullOrEmpty(f.Source) && File.Exists(Path.Combine(ExtensionLoader.ActiveExtensionInfo.FolderPath, f.Source)))
                    File.Copy(Path.Combine(ExtensionLoader.ActiveExtensionInfo.FolderPath, f.Source), filePath, true);
                else
                    File.WriteAllBytes(filePath, new byte[f.Size]);
            }

            // 添加 Flag
            os.Flags.AddFlag("Kernel_VMInfected_" + config.ConfigName);
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