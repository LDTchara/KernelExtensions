# Installation & Uninstallation

*LDTchara: Does this page really need to exist? But anyway, it's kind of useful so let's keep it.*

## Prerequisites

- **Hacknet** with the **Labyrinths** DLC installed.
- **Pathfinder** framework 5.3.4 or higher.
- Ensure your extension folder (e.g. `Extensions/YourExtension/`) already exists and contains a basic `extension.xml`.

## Installing KernelExtensions

1. Download the latest `KernelExtensions.dll` from [GitHub Releases](https://github.com/LDTchara/KernelExtensions/releases).
2. Place `KernelExtensions.dll` into the `Plugins` subfolder of your extension:  
   `Extensions/YourExtension/Plugins/KernelExtensions.dll`
3. (Optional but recommended) Create the following folders in your extension root for configuration files:  
   - `Trial/` – for trial configuration XMLs  
   - `VMATK/` – for VM attack configuration XMLs  
   - `Actions/` – for action files  
4. Launch Hacknet and load your extension. If you see a green `[KernelExtensions] All is well ** SUCCESS!!` in the console, the installation was successful.

## Uninstalling KernelExtensions

- If KernelExtensions is running as an **extension plugin** (the correct way), uninstalling the extension will automatically remove KernelExtensions. All Harmony patches will be revoked and the watermark will disappear.
- If you need a complete manual removal, simply delete the `Plugins/KernelExtensions.dll` file and restart the game.
- The mod **does not support global plugin mode** (i.e. it cannot be placed under `BepInEx/plugins/`). Please ensure it always stays inside an extension's `Plugins` directory.

## FAQ

**Q: The success message does not appear in the console.**  
A: Check that Pathfinder and BepInEx are correctly installed, and that `KernelExtensions.dll` is in the right location.

**Q: There is no watermark on the main menu after entering the game.**  
A: Make sure KernelExtensions is loaded as part of an extension. If no extension is loaded, the watermark will not appear.

**Q: How can I verify the loaded version?**  
A: The main menu watermark displays `+ KernelExtensions x.x.x`, matching the DLL version.

---

## See Also

- [Home](./../index.md) – Return to main index
- [安装与卸载 (中文)](./../../zh/guides/installation.md) – Chinese version