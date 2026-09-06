using Hacknet;
using KernelExtensions.Configs;
using KernelExtensions.Modules;
using KernelExtensions.Utilities;
using Pathfinder.Action;
using Pathfinder.Util;

namespace KernelExtensions.Actions
{
    /// <summary>
    /// 触发自定义结局序列（EndingConfig 文件模式）。
    ///
    /// XML 用法：
    ///   &lt;StartEnding File="Endings/myEnding.xml" /&gt;
    /// File 必填，相对扩展根，指向 &lt;Ending&gt; 根元素配置文件（属性见 Configs/EndingConfig）。
    /// 路径不限定文件夹（任意相对子目录均可）。需要调整配置请直接改结局 XML
    /// （结局期间无其他操作，不做属性临时覆盖）。
    ///
    /// 配置内容（Ending XML 属性，全部可选带默认）：
    ///   Title / EndingText / OnCreditMusic / AfterMusic / AfterAction /
    ///   SpeechFile / TextFile / CreditsFile / SpeechTime
    /// SpeechTime 语义（默认 -1）：-1 跟随音频时长（无语音 30s 兜底）；0 跳过演讲直接报幕；
    ///   N&gt;0 演讲上限 N 秒。语音支持 .wav 与 .ogg（NVorbis 解码，体积 ~1/10，波形自绘）。
    /// </summary>
    public class StartEnding : PathfinderAction
    {
        /// <summary>结局配置文件路径（必填，相对扩展根）。</summary>
        [XMLStorage] public string File = "";

        /// <summary>由 CustomEndingModule 在报幕完成后调用。</summary>
        internal Action OnCompleteCallback;

        public override void Trigger(object os_obj)
        {
            OS os = (OS)os_obj;

            if (ConfigValue.IsNone(File))
            {
                KELog.Error("[StartEnding] File attribute is required (path to an <Ending> config XML, relative to the extension root).");
                return;
            }

            EndingConfig cfg = EndingConfig.Load(File);
            if (cfg == null)
            {
                KELog.Error($"[StartEnding] failed to load ending config: {File}");
                return;
            }

            // ---- 构造结束回调（先构建，再赋值给 sequence）----
            string afterActionPath = cfg.AfterAction;
            OnCompleteCallback = () =>
            {
                if (ConfigValue.IsNone(afterActionPath))
                {
                    KELog.Info("[StartEnding] No AfterAction specified.");
                    return;
                }
                try { RunnableConditionalActions.LoadIntoOS(afterActionPath, os); }
                catch (Exception ex) { KELog.Warn($"[StartEnding] LoadIntoOS failed: {ex.Message}"); }
            };

            // ---- 创建并挂载结局 Module —— 直接设置 os.endingSequence ----
            var module = new CustomEndingModule(os.fullscreen, os, cfg)
            {
                OnCompleteCallback = OnCompleteCallback
            };
            module.StartEnding();
            os.endingSequence = module;
            KELog.Info($"[StartEnding] os.endingSequence set, IsActive={module.IsActive} (File='{File}')");
        }
    }
}
