using BepInEx;
using BepInEx.Hacknet;
using Hacknet;
using Microsoft.Xna.Framework.Graphics;
using Pathfinder.Executable;
using KernelExtensions.Executables;

namespace KernelExtensions
{
    [BepInPlugin(ModGUID, ModName, ModVer)]
    public class KernelExtensions : HacknetPlugin
    {
        public const string ModGUID = "com.LDTchara.KernelExtensions";
        public const string ModName = "KernelExtensions";
        public const string ModVer = "0.3.4";

        public override bool Load()
        {
            ExecutableManager.RegisterExecutable<CustomTrialExe>("#CUSTOMTRIAL#");
            Console.WriteLine("[KernelExtensions] CustomTrial registered.");

            Console.WriteLine("[KernelExtensions] All is well ** SUCCESS!!");
            return true;
        }
    }
}