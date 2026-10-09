using KernelExtensions.Managers;
using KernelExtensions.Utilities;
using Pathfinder.Meta.Load;
using Pathfinder.Replacements;
using Pathfinder.Util.XML;

namespace KernelExtensions.Saving
{
    /// <summary>
    /// 读取存档中的 <c>&lt;RamDisplayData Multiplier="..." Unit="..." /&gt;</c>，
    /// 恢复 RAM 显示设置。
    /// <para>
    /// 只有两个值，直接填 <see cref="RamDisplayManager"/> 的读档暂存，
    /// 由 <c>OSLoaded</c> 侧的 <c>ApplyOnLoaded()</c> 消费
    /// （与 <c>Reset()</c> 拉开顺序，避免被后者抹掉）。
    /// </para>
    /// </summary>
    [SaveExecutor("RamDisplayData", ParseOption.None)]
    public class RamDisplaySaveExecutor : SaveLoader.SaveExecutor
    {
        public override void Execute(EventExecutor exec, ElementInfo info)
        {
            string mult = info.Attributes.ContainsKey("Multiplier") ? info.Attributes["Multiplier"] : null;
            string unit = info.Attributes.ContainsKey("Unit") ? info.Attributes["Unit"] : null;

            if (float.TryParse(mult, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float m))
                RamDisplayManager.PendingMultiplier = m;
            else
                RamDisplayManager.PendingMultiplier = RamDisplayManager.DefaultMultiplier;

            RamDisplayManager.PendingUnit = unit;
            RamDisplayManager.PendingRestore = true;

            KELog.Debug($"[RamDisplay] staged restore: Multiplier={RamDisplayManager.PendingMultiplier}, Unit='{unit}'.");
        }
    }
}
