using Hacknet;
using HarmonyLib;

namespace KernelExtensions.Patches
{
    /// <summary>
    /// 拦截 ThemeManager.switchThemeLayout，实现"只换主题颜色、不换面板布局"（ChangeLayout=false）。
    /// 使用"过期时间戳"而非 bool 标志：
    ///   - 切换主题时调用 SkipLayoutChange(duration) 设置拦截截止时刻
    ///   - switchThemeLayout 被调用时，若当前时间未到截止时刻则跳过（布局保持）
    ///   - 到期后自动放行，无需 delayer 回调，也不会被"下一帧强制复位"抢跑
    /// </summary>
    [HarmonyPatch(typeof(ThemeManager), "switchThemeLayout")]
    public static class PhaseSwiftLayoutPatch
    {
        /// <summary>拦截截止时间（OS.currentElapsedTime 时间戳），0 = 不拦截</summary>
        internal static double _skipLayoutUntil = 0;

        [HarmonyPrefix]
        static bool Prefix()
        {
            // 当前时间已过截止 → 放行（执行原方法）；未过 → 跳过布局切换
            return OS.currentElapsedTime >= _skipLayoutUntil;
        }

        /// <summary>
        /// 开始拦截布局切换，持续 duration 秒。
        /// 多次调用取最晚截止（后一次不会缩短前一次的拦截窗口）。
        /// </summary>
        public static void SkipLayoutChange(float duration)
        {
            double until = OS.currentElapsedTime + duration;
            if (until > _skipLayoutUntil)
                _skipLayoutUntil = until;
        }

        /// <summary>立即取消拦截（Stop / 新会话兜底）。</summary>
        public static void Clear()
        {
            _skipLayoutUntil = 0;
        }
    }

    /// <summary>
    /// 新 OS 会话（新游戏/读档/重开）第一帧兜底清理，防止上次会话异常退出残留拦截。
    /// 只在新 OS 实例出现时执行一次，不参与运行中切换的复位。
    /// </summary>
    [HarmonyPatch(typeof(OS), "Update")]
    public static class PhaseSwiftLayoutResetPatch
    {
        internal static OS _trackedOs = null;

        [HarmonyPrefix]
        static void Prefix(OS __instance)
        {
            if (!ReferenceEquals(_trackedOs, __instance))
            {
                _trackedOs = __instance;
                PhaseSwiftLayoutPatch.Clear();
            }
        }
    }
}
