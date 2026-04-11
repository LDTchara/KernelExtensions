using BepInEx;
using BepInEx.Hacknet;

namespace CustomTrial;

[BepInPlugin(ModGUID, ModName, ModVer)]
public class CustomTrial : HacknetPlugin
{
    public const string ModGUID = "com.LDTchara.CustomTrial";
    public const string ModName = "CustomTrial";
    public const string ModVer = "0.0.0";

    public override bool Load()
    {
        return true;
    }
}
