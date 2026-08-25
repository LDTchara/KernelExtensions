using KernelExtensions.Managers;
using Pathfinder.Meta.Load;
using Pathfinder.Replacements;
using Pathfinder.Util.XML;

namespace KernelExtensions.Saving
{
    /// <summary>
    /// 读取存档中的 &lt;ClockData&gt; 节点，恢复运行中的 Clock。
    /// 存入 ClockManager.PendingRestore 供 OSLoaded 后重建。
    /// </summary>
    // ParseInterior：必须解析子元素（&lt;Clock&gt;），否则 info.Children 恒为空
    [SaveExecutor("ClockData", ParseOption.ParseInterior)]
    public class ClockSaveExecutor : SaveLoader.SaveExecutor
    {
        private static string GetAttr(ElementInfo info, string key, string fallback)
        {
            return info.Attributes.ContainsKey(key) ? info.Attributes[key] : fallback;
        }

        public override void Execute(EventExecutor exec, ElementInfo info)
        {
            var states = new List<ClockPersistentState>();
            foreach (var child in info.Children)
            {
                if (child.Name != "Clock") continue;

                string id = GetAttr(child, "Id", "");
                string src = GetAttr(child, "SourcePath", "");
                // Id/SourcePath 缺失 → 无法重载定义，跳过
                if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(src)) continue;

                int.TryParse(GetAttr(child, "TimesElapsed", "0"), out int times);
                float.TryParse(GetAttr(child, "Elapsed", "0"), out float elapsed);
                float.TryParse(GetAttr(child, "Timer", "0"), out float timer);

                states.Add(new ClockPersistentState
                {
                    Id = id,
                    SourcePath = src,
                    ExtensionRoot = GetAttr(child, "ExtensionRoot", ""),
                    TimesElapsed = times,
                    Elapsed = elapsed,
                    Timer = timer
                });
            }

            if (states.Count > 0)
                ClockManager.PendingRestore = states;
        }
    }
}
