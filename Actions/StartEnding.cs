using Hacknet;
using KernelExtensions.Configs;
using KernelExtensions.Modules;
using KernelExtensions.Utilities;
using Pathfinder.Action;
using Pathfinder.Util;

namespace KernelExtensions.Actions
{
    /// <summary>
    /// 触发自定义结局序列（EndingConfig 版）。
    ///
    /// XML 用法：
    ///   A. 独立结局文件（推荐）：&lt;StartEnding File="Endings/myEnding.xml" /&gt;
    ///      File 相对扩展根，指向 &lt;Ending&gt; 根元素配置文件（属性见 Configs/EndingConfig）。
    ///      File 为唯一配置源——需要调整请直接改结局 XML（结局期间无其他操作，临时覆盖无意义）。
    ///   B. 旧属性用法（兼容）：&lt;StartEnding SpeechTime="-1" Title="..." SpeechFile="Docs/EndingSpeech.ogg"
    ///      TextFile="..." CreditsFile="..." AfterAction="..." /&gt;
    ///
    /// SpeechTime 语义（仅 B 用法 / Ending XML 属性，默认 -1）：
    ///   -1/缺省 —— 有语音跟随音频时长；无语音静默 30s 兜底
    ///    0      —— 跳过演讲直接报幕
    ///    &gt;0    —— 演讲上限 N 秒（音频先完提前进报幕）
    ///
    /// 语音格式：.wav（SoundEffect）或 .ogg（NVorbis 解码，体积 ~1/10，波形自绘）。
    /// 注册名与属性名冻结（XML 兼容）。
    /// </summary>
    public class StartEnding : PathfinderAction
    {
        // ---- 新用法：结局配置文件（相对扩展根；NONE/空 = 用下方旧属性）----
        [XMLStorage] public string File = "";

        // ---- 旧用法兼容（File 为空时生效；与 Ending XML 属性同名同语义）----
        [XMLStorage] public float SpeechTime = -1f;
        [XMLStorage] public string Title = "Hacknet";
        [XMLStorage] public string EndingText = "Thanks For Playing";
        [XMLStorage] public string OnCreditMusic = "";
        [XMLStorage] public string AfterMusic = "";
        [XMLStorage] public string AfterAction = "";
        [XMLStorage] public string SpeechFile = "";
        [XMLStorage] public string TextFile = "";
        [XMLStorage] public string CreditsFile = "";

        /// <summary>由 CustomEndingModule 在报幕完成后调用。</summary>
        internal Action OnCompleteCallback;

        public override void Trigger(object os_obj)
        {
            OS os = (OS)os_obj;

            // ---- 配置来源：File 文件（唯一源）或旧属性（兼容）----
            EndingConfig cfg = ConfigValue.IsNone(File)
                ? EndingConfig.FromAction(this)
                : EndingConfig.Load(File);
            if (cfg == null)
            {
                KELog.Error($"[StartEnding] failed to load ending config (File='{File}').");
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
