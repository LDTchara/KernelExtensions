using BepInEx.Hacknet;

namespace KernelExtensions.Compat.ZeroDayToolKit
{
    /// <summary>
    /// ZeroDayToolKit 主菜单水印位置兼容。
    ///
    /// ZDTK 会在主菜单标题同排绘制 <c>+ ZeroDayToolKit &lt;version&gt;</c>。
    /// KE 的水印需要知道它是否已经加载、以及它实际使用的版本文字宽度。
    /// 这里只负责“读取 ZDTK 实际水印文字”，具体偏移与水印绘制仍由
    /// <c>Patches/MainMenuWatermarkPatch</c> 负责。
    ///
    /// 不缓存失败结果：调用方只应在主菜单首次绘制时调用一次；若调用过早，
    /// 后续仍可重新探测，不会把“尚未加载”永久记成“没有安装”。
    /// </summary>
    internal static class ZdtkWatermarkCompat
    {
        private const string ZdtkGuid = "kr.o_r.prodzpod.zerodaytoolkit";

        /// <summary>
        /// 读取 ZDTK 实际绘制的水印文字；未安装或尚未加载时返回 false。
        /// </summary>
        internal static bool TryGetWatermark(out string text)
        {
            text = null;
            var plugins = HacknetChainloader.Instance?.Plugins;
            if (plugins == null || !plugins.TryGetValue(ZdtkGuid, out var info))
                return false;

            // BepInEx.Core 的 PluginInfo.Metadata.Version 来自 SemanticVersioning；
            // KE 不直接引用该程序集，这里用反射取字符串，避免新增运行时依赖。
            var metadata = info?.Metadata;
            string version = metadata?.GetType().GetProperty("Version")?.GetValue(metadata)?.ToString();
            if (string.IsNullOrEmpty(version))
                return false;

            text = "+ ZeroDayToolKit " + version;
            return true;
        }
    }
}
