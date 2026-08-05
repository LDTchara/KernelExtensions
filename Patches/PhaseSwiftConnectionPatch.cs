using Hacknet;
using HarmonyLib;
using KernelExtensions.Modules;

namespace KernelExtensions.Patches
{
    [HarmonyPatch(typeof(Programs), nameof(Programs.connect))]
    public static class PhaseSwiftConnectionPatch
    {
        [HarmonyPrefix]
        static bool Prefix(string[] args, OS os)
        {
            // PS 运行时才拦截连接（exe 或 InitAction 启动均生效）。
            // 旧版用 CurrentExe 判断但从未被赋值，导致配置黑名单从未生效。
            if (!PhaseSwiftManager.IsRunning) return true;
            if (args.Length < 2) return true;
            var comp = Programs.getComputer(os, args[1]);
            if (comp == null) return true;
            if (!PhaseSwiftManager.IsNodeAllowed(comp.idName))
            {
                os.write("Connection Failed: Cannot Find Target Node");
                return false;
            }
            return true;
        }
    }
}