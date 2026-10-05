using Hacknet;
using KernelExtensions.Managers;
using KernelExtensions.Utilities;
using Pathfinder.Action;
using Pathfinder.Util;

namespace KernelExtensions.Actions.Misc
{
    /// <summary>
    /// 调整 RAM 显示文字（9.12）。
    /// <para>
    /// 作用于原版 <c>RamModule</c> 的 <c>"USED RAM: x / y mb"</c>：
    /// <c>Multiplier</c> 统一缩放 <c>x</c> 与 <c>y</c>，<c>Unit</c> 替换单位串。其它一律不变。
    /// </para>
    /// <para>
    /// **只写显式给出的属性** —— 不写的项保持当前值（因此可以只改倍率、或只改单位）。
    /// 需要复位时显式写 <c>Multiplier="1" Unit="mb"</c>。
    /// 修改后立即存档，保证读档后仍是同一显示。
    /// </para>
    /// <example>
    /// <code>
    /// &lt;RamDisplay Multiplier="0.5" Unit="GB" /&gt;
    /// </code>
    /// </example>
    /// </summary>
    public class RamDisplayAction : KEAction
    {
        /// <summary>统一倍率。字符串类型以便区分「未写」与「写了 0」。</summary>
        [XMLStorage]
        public string Multiplier;

        /// <summary>单位串；<c>NONE</c>/空 = 回退默认 <c>mb</c>。字符串类型以便区分「未写」与「写了空」。</summary>
        [XMLStorage]
        public string Unit;

        public override void Trigger(OS os)
        {
            bool changed = false;

            if (!string.IsNullOrWhiteSpace(Multiplier))
            {
                if (float.TryParse(Multiplier.Trim(), System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out float m))
                {
                    RamDisplayManager.Multiplier = m;
                    changed = true;
                }
                else
                {
                    KELog.Warn($"[RamDisplay] Multiplier '{Multiplier}' is not a number; ignored.");
                }
            }

            // Unit 写了就算（含空串/空白：按 NONE 约定回退默认 mb）；只有“没写属性”才保持原值
            if (Unit != null)
            {
                RamDisplayManager.Unit = RamDisplayManager.NormalizeUnit(Unit);
                changed = true;
            }

            if (!changed)
            {
                // 两个属性都没写：不做事，也不白存一次档
                // 你光写个壳不填参数那你干嘛用这个啊？直接不写这个 Action 就行了
                // If you're not setting any attributes, why are you using this at all?
                KELog.Warn("[RamDisplay] ...? no attributes set; nothing to do.");
                return;
            }

            KELog.Info($"[RamDisplay] Multiplier={RamDisplayManager.Multiplier}, Unit='{RamDisplayManager.Unit}'.");

            // 立即落盘：显示状态属于「改了就应跨读档保持」的持久数据
            os?.threadedSaveExecute(true);
        }
    }
}
