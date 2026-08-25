using Hacknet;
using KernelExtensions.Modules;
using KernelExtensions.Utilities;
using Pathfinder.Action;
using Pathfinder.Util;

namespace KernelExtensions.Actions
{
    /// <summary>
    /// 触发自定义结局序列（dev1 移植版）。
    /// XML 用法：
    ///   &lt;StartEnding SpeechTime="30" AfterAction="Actions/AfterCredits.xml"
    ///               SpeechFile="Docs/EndingSpeech.wav" TextFile="Docs/Speech.txt"
    ///               CreditsFile="Docs/CreditsData.txt" /&gt;
    /// SpeechTime：无 WAV 时静默计时秒数（可选，默认 30）；AfterAction：报幕后执行的
    /// ConditionalActions XML（可选）；资源路径相对扩展根（可选，默认 Docs/）。
    /// 空串/NONE 的资源字段 = 用默认路径（NONE 约定）。
    /// </summary>
    public class StartEnding : PathfinderAction
    {
        [XMLStorage] public float SpeechTime = 30f;
        [XMLStorage] public string Title = "Hacknet";
        [XMLStorage] public string EndingText = "Thanks For Playing";
        [XMLStorage] public string OnCreditMusic = "";
        [XMLStorage] public string AfterMusic = "";
        [XMLStorage] public string AfterAction = "";
        [XMLStorage] public string SpeechFile = "";
        [XMLStorage] public string TextFile = "";
        [XMLStorage] public string CreditsFile = "";

        /// <summary>由 CustomEndingModule 在报幕完成后调用。</summary>
        internal static Action OnCompleteCallback;

        public override void Trigger(object os_obj)
        {
            OS os = (OS)os_obj;

            // ---- 构造结束回调（先构建，再赋值给 sequence） ----
            OnCompleteCallback = () =>
            {
                if (ConfigValue.IsNone(AfterAction))
                {
                    KELog.Info("[StartEnding] No AfterAction specified.");
                    return;
                }
                string path = AfterAction.Trim();
                try { RunnableConditionalActions.LoadIntoOS(path, os); }
                catch (Exception ex) { KELog.Warn($"[StartEnding] LoadIntoOS failed: {ex.Message}"); }
            };

            // ---- 创建并挂载结局 Module —— 直接设置 os.endingSequence ----
            var module = new CustomEndingModule(os.fullscreen, os)
            {
                Titletext = Title,
                endingText = EndingText,
                onCreditMusic = OnCreditMusic,
                afterMusic = AfterMusic,
                // 资源路径：NONE/空=默认（Docs/...）
                SpeechFile = ConfigValue.IsNone(SpeechFile) ? "Docs/EndingSpeech.wav" : SpeechFile,
                TextFile = ConfigValue.IsNone(TextFile) ? "Docs/Speech.txt" : TextFile,
                CreditsFile = ConfigValue.IsNone(CreditsFile) ? "Docs/CreditsData.txt" : CreditsFile
            };
            module.StartEnding(SpeechTime);
            module.OnCompleteCallback = OnCompleteCallback;
            os.endingSequence = module;
            KELog.Info($"[StartEnding] os.endingSequence set, IsActive={module.IsActive}");
        }
    }
}
