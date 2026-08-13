# Skin Art Editor

Slay the Spire 2 mod that lets you configure **custom static-PNG character skins** from files on disk. v1 is a **Regent** vertical slice (Cassiopeia-style Wishkeeper poses).

## Features

- Per-character folder under `mods/SkinArtEditor/characters/<id>/`
- Optional assets: missing slots keep **vanilla** art
- Combat is all-or-nothing (needs all five poses); shop/rest/UI are independent
- Configurable offsets (combat, FormVfx, shop, rest)
- **ModConfig** integration when installed; otherwise press **F8** for the fallback UI
- Restart the game after saving to apply scene overrides

## Asset slots

| Key | Context | Notes |
|---|---|---|
| `idle_loop`, `attack`, `cast`, `hurt`, `die` | Combat | All five required to replace Spine combat |
| `relaxed_loop` | Shop | Optional |
| `rest_loop` | Campfire / rest site | Optional |
| `char_select`, `char_select_locked` | Character select portraits | Optional each |
| `char_select_bg` | Character select background | Optional |
| `character_icon`, `character_icon_outline` | Top panel | Optional each |
| `map_marker` | Map marker | Optional |

## Offsets (`config.json`)

Defaults match Cassiopeia’s tuned Regent values. They only apply when that context is overridden.

- **Combat:** `combatVisualsPosition`, `combatVisualsScale`, `combatBottomPaddingPx`, `formVfxPosition`
- **Shop:** `shopSpriteOffset`, `shopSpriteScale`
- **Rest:** `restDisplayOffset`, `restSpriteScale`, `restSeatAnchor`, `restVisibleBounds`

## Build / deploy

Requires .NET 9, Godot 4.5.1 Mono, and GDRE (defaults point at the Cassiopeia `tools/bin` copies).

```powershell
# Build DLL + PCK
.\tools\build.ps1

# Build and install into STS2 mods folder
.\tools\build.ps1 -Deploy
```

Sample Regent art (from Cassiopeia) is shipped under `characters/regent/` so the mod works out of the box after deploy.

## In-game config

1. With **ModConfig**: open its settings panel for “Skin Art Editor”, browse/clear assets, edit offsets, click **Save / Apply**.
2. Without ModConfig: press **F8** for the built-in panel.
3. Restart STS2.

## Partial overrides

- Empty / cleared / missing file → that slot stays vanilla
- Incomplete combat set → entire combat stays vanilla Spine
- You can customize only the map marker, or only rest, etc.

## Layout after install

```text
mods/SkinArtEditor/
  SkinArtEditor.json
  SkinArtEditor.dll
  SkinArtEditor.pck
  characters/regent/
    config.json
    *.png
```
