using Hacknet;
using HarmonyLib;
using KernelExtensions.Modules;
using KernelExtensions.Executables;

namespace KernelExtensions.Patches
{
    [HarmonyPatch(typeof(Programs), nameof(Programs.connect))]
    public static class PhaseSwiftConnectionPatch
    {
        public static PhaseSwiftExe CurrentExe;

        [HarmonyPrefix]
        static bool Prefix(string[] args, OS os)
        {
            if (CurrentExe == null) return true;
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