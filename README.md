# Skin Art Editor

Slay the Spire 2 mod that lets you configure **custom static-PNG character skins** from files on disk. Supports all five vanilla playables; ships with a **Regent** sample profile (Cassiopeia-style Wishkeeper poses).

## Supported characters

| Slug | Display name | Sample art |
|---|---|---|
| `ironclad` | Ironclad | — (add your PNGs) |
| `silent` | Silent | — |
| `defect` | Defect | — |
| `regent` | Regent | Yes (`characters/regent/`) |
| `necrobinder` | Necrobinder | — |

Folder name under `mods/SkinArtEditor/characters/<slug>/` must match the game character id. The ModConfig / F8 dropdown lists every folder that has a `config.json`.

**Caveats**

- **Necrobinder Osty:** vanilla rest/combat include companion Osty skeletons. The generic PNG scenes replace those; Osty is **not** separately skinned in this version.
- **Defect orbs / character VFX:** body PNG overrides leave orb UI and most vanilla VFX alone.
- **Offsets:** non-Regent profiles start with neutral defaults — tune after you add art. Regent’s tuned values are the reference in [`characters/regent/config.json`](characters/regent/config.json).

## Features

- Per-character folder under `mods/SkinArtEditor/characters/<id>/`
- Optional assets: missing slots keep **vanilla** art
- Combat is all-or-nothing (needs all five poses); shop/rest/UI are independent
- Configurable offsets (combat, FormVfx, shop, rest, char-select background framing)
- **Backdrop knockout** (Cassiopeia-style): solid near-black backgrounds on combat/shop/rest, icon, and map-marker PNGs are cleared when copying
- **ModConfig** integration when installed; otherwise press **F8** for the fallback UI
- Restart the game after saving to apply scene overrides

## Asset slots

| Key | Context | Notes |
|---|---|---|
| `idle_loop`, `attack`, `cast`, `hurt`, `die` | Combat | All five required to replace Spine combat; backdrop knockout on copy |
| `relaxed_loop` | Shop | Optional; backdrop knockout on copy |
| `rest_loop` | Campfire / rest site | Optional; backdrop knockout on copy |
| `char_select` | Character select portrait | Optional (no knockout). Auto-generates `char_select_locked` |
| `char_select_bg` | Character select background | Optional (no knockout). Framed at runtime: Cassiopeia contain → 1.2× zoom → top + 10% left |
| `character_icon` | Top panel, stats, bestiary, card-library filters | Backdrop knockout on copy; auto-generates `character_icon_outline` |
| `map_marker` | Map marker | Optional; backdrop knockout on copy |

`char_select_locked` and `character_icon_outline` are **not** browsable — they are derived on Browse/Save (Cassiopeia dark-grayscale locked portrait and white silhouette outline).

## Backdrop knockout

When browsing/saving eligible art, the mod can clear an **edge-connected near-black** backdrop (same algorithm as Cassiopeia’s `make-custom-png-skin` / `knockout_backdrop.py`):

- Applies to combat/shop/rest poses, `character_icon`, and `map_marker`
- Does **not** apply to char-select portraits or `char_select_bg` (those should stay opaque)
- `character_icon_outline` is derived from the knocked-out `character_icon` (not knocked out separately)
- Only pixels connected to the image border with `r+g+b <= threshold` (default **18**) become transparent
- Interior dark pixels stay opaque
- Already-transparent art is effectively unchanged
- Cassiopeia’s icon master prep sometimes used threshold **36**; raise `knockoutThreshold` if icon backs don’t clear enough

Config fields in `config.json`:

```json
"knockoutBackdrop": true,
"knockoutThreshold": 18
```

Toggle these in ModConfig or the F8 panel. Changing threshold does not reprocess files already on disk — re-browse the source PNG to re-run knockout.

## Offsets (`config.json`)

DTO defaults are **neutral** (scale 1, zero offsets, char-select BG uses Cassiopeia contain framing). Character-specific tuning belongs in each profile — Regent’s Cassiopeia values live in [`characters/regent/config.json`](characters/regent/config.json).

- **Combat:** `combatVisualsPosition`, `combatVisualsScale`, `combatBottomPaddingPx`, `formVfxPosition`
- **Shop:** `shopSpriteOffset`, `shopSpriteScale`
- **Rest:** `restDisplayOffset`, `restSpriteScale`, `restSeatAnchor`, `restVisibleBounds`
- **Char select BG:** `charSelectBgZoom` (default `1.2` after contain-fit), `charSelectBgOffsetX` / `charSelectBgOffsetY` (defaults `-0.1`, `0`). Browsing a new `char_select_bg` resets these.

They only apply when that context is overridden.

## Shared scene limits (still global)

PNG merchant/rest/combat templates are shared across characters. Per-profile offsets cover sprite placement/scale; these remain **scene-global** until a later pass:

- Rest-site hitbox / selection reticle / thought-bubble positions and root transform in `png_rest_site.tscn`
- Combat intent/marker layout baked into `png_combat.tscn`
- Animator trigger aliases (e.g. Regent `Attack_Sovereign` → attack) in `png_animator.gd`
- Card-library pools are mapped for the five vanilla characters in code

## Build / deploy

Requires .NET 9, Godot 4.5.1 Mono, and GDRE (defaults point at the Cassiopeia `tools/bin` copies).

```powershell
# Build DLL + PCK
.\tools\build.ps1

# Build and install into STS2 mods folder
.\tools\build.ps1 -Deploy
```

Sample Regent art (from Cassiopeia) is shipped under `characters/regent/` so the mod works out of the box after deploy. Other characters seed as empty configs (vanilla until you Browse art).

## In-game config

1. With **ModConfig**: open its settings panel for “Skin Art Editor”, pick a character, browse/clear assets, edit offsets, click **Save / Apply**.
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
  characters/
    ironclad/config.json
    silent/config.json
    defect/config.json
    regent/config.json + *.png
    necrobinder/config.json
```
