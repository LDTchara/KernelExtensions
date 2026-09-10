# Custom Node Icon System

The **Custom Node Icon System** lets extension authors give any computer a custom icon — either **statically declared** in the node XML (zero Actions) or switched at runtime with the `SetNodeIcon` action.

It supports custom image files (loaded with the extension), all vanilla preset icons, and `#RESET#` to restore the original icon. Icon state persists with the save file.

!!! info "Applies to"
    This page covers KernelExtensions **0.7**. Related action: `SetNodeIcon`.

---

## Overview

- **Static declaration**: the node XML `icon` attribute, no Action needed (e.g. `<Computer id="testNode1" icon="@C" />`)
- **Runtime switching**: `<SetNodeIcon NodeID="dhs" Icon="@MyIcon" />`
- Custom textures are registered in `KE-Config.xml` under `<CustomImages>`
- Icon state is stored per node and restored automatically on load

---

## Zero-Action Static Declaration

The node XML `icon` attribute *is* a declaration site — **no Action required**; it is applied automatically when the node loads:

```xml
<Computer id="testNode1" name="Test Node1" type="empty" icon="@C">
    ...
</Computer>
```

- `icon` accepts every icon value: `@customTexture` (e.g. `@C`), vanilla preset names (e.g. `laptop`), `SEC_LEVEL_x`
- Mechanism: `NodeIconComputerExecutor` (a node-load hook, auto-registered) reads the `icon` attribute into storage (OrgIcon / CurrIcon) and the render pipeline applies it; when a node has **no** explicit `icon`, a default icon is mapped from its `security` level
- Coexistence with `SetNodeIcon`: static declaration uses the `icon` attribute (zero Actions), runtime switching during the story uses the Action

---

## Runtime Switching

```xml
<SetNodeIcon NodeID="dhs" Icon="@Mine" />
```

Node `dhs` immediately shows the custom icon `Mine.png`; the change is written to the save file and survives reloading.

---

## How It Works

Hacknet's `DisplayModule` resolves node icons through a private `compAltIcons` dictionary (`Dictionary<string, Texture2D>`): when a node has no icon or the dictionary has no entry, it falls back to the default icon for the security level (0–5); otherwise it uses the dictionary texture.

KernelExtensions adds three icon layers on top:

| Layer | Icon value | Implementation |
|-------|------------|----------------|
| **Vanilla presets** | `laptop`, `chip`, `tablet`, `ePhone`, `kellis`, ... | The vanilla `compAltIcons` dictionary |
| **Custom textures** | `@iconName` | A Harmony Postfix on `DisplayModule.GetComputerImage()`; textures come from a dedicated dictionary |
| **Security-level fallback** | `SEC_LEVEL_0` ~ `SEC_LEVEL_6` | Generated internally when a node has no explicit icon; also used for save restoration |

---

## Custom Images

### 1. Prepare the image files

Place images inside the extension directory, for example:

```
ExtensionRoot/
├── Images/
│   ├── Mine.png
│   └── Server.png
└── ...
```

!!! warning "Size and format"
    - Icons are **drawn at their original pixel size (no scaling)**, so stay close to the vanilla preset icon size. The connection header has roughly **160 px** of usable width — anything wider overlaps the adjacent "Connected to" text (which is drawn from `x + 160`).
    - **PNG is recommended** (full alpha transparency). Other formats decodable by SDL_image at runtime (JPG/BMP/GIF/TGA/TIFF/WebP/PCX/PNM) also work, but formats without an alpha channel (e.g. JPG) may show a black background.
    - The file extension **does not participate in decoding** (SDL_image detects by content); it is only used to build the `@name`. Avoid files sharing a name with different extensions (`Mine.png` and `Mine.jpg` both register as `@Mine`; the latter overwrites the former).

### 2. Register the icons (`KE-Config.xml`)

KE creates `KE-Config.xml` automatically if it does not exist. Uncomment `<CustomImages>` and fill in the paths:

```xml
<KEConfig>
    <CustomImages>
        <Image>Images/Mine.png</Image>
        <Image>Images/Server.png</Image>
    </CustomImages>
</KEConfig>
```

**Registration rule**: for each file, KE strips the extension and prepends `@`.

| File | Registered as |
|------|---------------|
| `Images/Mine.png` | `@Mine` |
| `Images/Server.png` | `@Server` |

### 3. Use it

```xml
<!-- Custom texture -->
<SetNodeIcon NodeID="dhs" Icon="@Mine" />

<!-- Vanilla preset -->
<SetNodeIcon NodeID="admin" Icon="laptop" />

<!-- Restore the original icon -->
<SetNodeIcon NodeID="compB" Icon="#RESET#" />
```

---

## Supported Icon Values

| Format | Example | Source |
|--------|---------|--------|
| `@fileName` | `@Mine` | Custom texture registered via `<CustomImages>` in `KE-Config.xml` |
| Vanilla preset name | `laptop`, `chip`, `tablet`, `ePhone`, `kellis` | The vanilla `compAltIcons` dictionary |
| Fixed security-level icon | `SEC_LEVEL_0` ~ `SEC_LEVEL_6` | Always uses that level's texture, regardless of the node's actual `security` |
| `#RESET#` | `#RESET#` | Restores the original icon recorded when the node was first created |

---

## Data Persistence

Icon state is stored as a `<NodeIcon>` child element inside each computer's save XML:

```xml
<Computer id="dhs">
  <NodeIcon org="SEC_LEVEL_2" curr="@Mine" />
  ...
</Computer>
```

- `org` = the original icon recorded when the node was first encountered
- `curr` = the current icon, set by the player or `SetNodeIcon`
- All computer icons are restored automatically on load; `#RESET#` always returns to the `org` value

---

## Known Limitations

- Loading custom textures requires an initialised graphics device: unavailable on the main menu, they load after OS initialisation completes

---

## Related Actions

### `SetNodeIcon`

```xml
<SetNodeIcon NodeID="dhs" Icon="@Mine" />
<SetNodeIcon NodeID="dhs" Icon="laptop" />
<SetNodeIcon NodeID="dhs" Icon="#RESET#" />
```

| Attribute | Required | Description |
|-----------|:--------:|-------------|
| `NodeID` | ✅ | Target computer ID (alias `TargetComp`; both are equivalent) |
| `Icon` | ✅ | Icon value, see [Supported Icon Values](#supported-icon-values) |
| `Delay` | ❌ | Seconds to wait before executing; `0` or omitted executes immediately |
| `DelayHost` | ❌ | Host node ID for the delay service (the host needs the `FastActionHost` daemon) |

---

## See Also

- [Home](./../index.md) – back to the main index
- [自定义节点图标系统（中文）](./../../zh/systems/node-icon.md) – Chinese version
- [Custom Actions](./../components/actions.md) – full list of custom actions
- [Misc](./../guides/misc.md) – other auxiliary features
