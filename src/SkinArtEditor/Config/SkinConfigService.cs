using System.Globalization;

namespace SkinArtEditor.Config;

/// <summary>
/// Shared load/save/browse helpers for ModConfig and the F8 UI.
/// </summary>
public static class SkinConfigService
{
    /// <summary>Currently selected character slug for ModConfig browse/clear actions.</summary>
    public static string SelectedCharacterId { get; set; } = "regent";

    public static string FormatVec(float[] v) =>
        string.Join(", ", v.Select(f => f.ToString(CultureInfo.InvariantCulture)));

    public static float[] ParseVec(string? s, float[] fallback)
    {
        if (string.IsNullOrWhiteSpace(s))
            return fallback;
        var parts = s.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
            return fallback;
        if (!float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x))
            return fallback;
        if (!float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
            return fallback;
        return [x, y];
    }

    public static float[] ParseBounds(string? s, float[] fallback)
    {
        if (string.IsNullOrWhiteSpace(s))
            return fallback;
        var parts = s.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 4)
            return fallback;
        var arr = new float[4];
        for (var i = 0; i < 4; i++)
        {
            if (!float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out arr[i]))
                return fallback;
        }
        return arr;
    }

    public static float ParseFloat(string? s, float fallback) =>
        float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : fallback;

    public static int ParseThreshold(string? s, int fallback)
    {
        if (int.TryParse(s?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var t) && t >= 0)
            return t;
        return fallback;
    }

    public static void ApplyOffsetFields(SkinOffsetsDto o, IReadOnlyDictionary<string, string> fields)
    {
        o.CombatVisualsPosition = ParseVec(Get(fields, "combat_pos"), o.CombatVisualsPosition);
        o.CombatVisualsScale = ParseFloat(Get(fields, "combat_scale"), o.CombatVisualsScale);
        o.CombatBottomPaddingPx = ParseFloat(Get(fields, "combat_pad"), o.CombatBottomPaddingPx);
        o.FormVfxPosition = ParseVec(Get(fields, "form_vfx"), o.FormVfxPosition);
        o.ShopSpriteOffset = ParseVec(Get(fields, "shop_offset"), o.ShopSpriteOffset);
        o.ShopSpriteScale = ParseFloat(Get(fields, "shop_scale"), o.ShopSpriteScale);
        o.RestDisplayOffset = ParseVec(Get(fields, "rest_offset"), o.RestDisplayOffset);
        o.RestSpriteScale = ParseFloat(Get(fields, "rest_scale"), o.RestSpriteScale);
        o.RestSeatAnchor = ParseVec(Get(fields, "rest_anchor"), o.RestSeatAnchor);
        o.RestVisibleBounds = ParseBounds(Get(fields, "rest_bounds"), o.RestVisibleBounds);
        o.CharSelectBgZoom = ParseFloat(Get(fields, "char_select_bg_zoom"), o.CharSelectBgZoom);
        var bgOffset = ParseVec(
            Get(fields, "char_select_bg_offset"),
            [o.CharSelectBgOffsetX, o.CharSelectBgOffsetY]);
        o.CharSelectBgOffsetX = bgOffset[0];
        o.CharSelectBgOffsetY = bgOffset[1];
    }

    public static Dictionary<string, string> ReadOffsetFields(SkinOffsetsDto o) => new()
    {
        ["combat_pos"] = FormatVec(o.CombatVisualsPosition),
        ["combat_scale"] = o.CombatVisualsScale.ToString(CultureInfo.InvariantCulture),
        ["combat_pad"] = o.CombatBottomPaddingPx.ToString(CultureInfo.InvariantCulture),
        ["form_vfx"] = FormatVec(o.FormVfxPosition),
        ["shop_offset"] = FormatVec(o.ShopSpriteOffset),
        ["shop_scale"] = o.ShopSpriteScale.ToString(CultureInfo.InvariantCulture),
        ["rest_offset"] = FormatVec(o.RestDisplayOffset),
        ["rest_scale"] = o.RestSpriteScale.ToString(CultureInfo.InvariantCulture),
        ["rest_anchor"] = FormatVec(o.RestSeatAnchor),
        ["rest_bounds"] = FormatVec(o.RestVisibleBounds),
        ["char_select_bg_zoom"] = o.CharSelectBgZoom.ToString(CultureInfo.InvariantCulture),
        ["char_select_bg_offset"] = FormatVec([o.CharSelectBgOffsetX, o.CharSelectBgOffsetY])
    };

    /// <summary>
    /// Apply a browsed or typed asset path: copy if absolute, else store relative; seed BG framing defaults.
    /// </summary>
    public static void SetUserAsset(string characterId, SkinProfileDto dto, string key, string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            AssetCopier.ClearAsset(characterId, key, dto);
            return;
        }

        path = path.Trim();
        if (Path.IsPathRooted(path) && File.Exists(path))
        {
            dto.Assets[key] = AssetCopier.CopyAsset(
                characterId, key, path, dto.KnockoutBackdrop, dto.KnockoutThreshold, dto);
            if (string.Equals(key, AssetKeys.CharSelectBg, StringComparison.OrdinalIgnoreCase))
                CharSelectBgFramer.ApplyCassiopeiaDefaults(dto.Offsets);
        }
        else
        {
            dto.Assets[key] = path;
        }
    }

    public static void SaveAndReload(SkinProfileDto dto)
    {
        DerivedUiArt.EnsureAll(dto.CharacterId, dto);
        SkinProfileLoader.Normalize(dto, dto.CharacterId);
        SkinProfileLoader.Save(dto);
        SkinRegistry.ReloadAll();
    }

    private static string Get(IReadOnlyDictionary<string, string> fields, string key) =>
        fields.TryGetValue(key, out var v) ? v : "";
}
