using BepInEx;
using BepInEx.Hacknet;

namespace CustomTrial;

[BepInPlugin(ModGUID, ModName, ModVer)]
public class KernelExtensions : HacknetPlugin
{
    public const string ModGUID = "com.LDTchara.KernelExtensions";
    public const string ModName = "KernelExtensions";
    public const string ModVer = "0.0.0";

    public override bool Load()
    {
        return true;
    }
}
