using System.Globalization;
using System.Reflection;
using Godot;

namespace SkinArtEditor.Config;

/// <summary>
/// Optional ModConfig integration via reflection (no hard dependency).
/// Source of truth remains characters/*/config.json.
/// </summary>
public static class ModConfigIntegration
{
    private const string ModId = ModPaths.ModId;
    private static string _selectedCharacter = "regent";
    private static Node? _host;

    public static bool TryRegister(Node host)
    {
        _host = host;

        var apiType = Type.GetType("ModConfig.ModConfigApi, ModConfig", throwOnError: false);
        var entryType = Type.GetType("ModConfig.ConfigEntry, ModConfig", throwOnError: false);
        var configType = Type.GetType("ModConfig.ConfigType, ModConfig", throwOnError: false);
        if (apiType == null || entryType == null || configType == null)
        {
            Log.Info("ModConfig not found — using fallback settings UI");
            return false;
        }

        try
        {
            var entries = BuildEntries(entryType, configType);
            var register = apiType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .First(m => m.Name == "Register" && m.GetParameters().Length == 3
                            && m.GetParameters()[2].ParameterType.IsArray);

            var array = Array.CreateInstance(entryType, entries.Count);
            for (var i = 0; i < entries.Count; i++)
                array.SetValue(entries[i], i);

            register.Invoke(null, [ModId, "Skin Art Editor", array]);
            Log.Info("Registered settings with ModConfig");
            return true;
        }
        catch (Exception ex)
        {
            Log.Warn($"ModConfig registration failed: {ex.Message}");
            return false;
        }
    }

    private static List<object> BuildEntries(Type entryType, Type configType)
    {
        var list = new List<object>();
        var dto = SkinProfileLoader.LoadDtoOrDefault(_selectedCharacter);

        list.Add(Entry(entryType, configType, "header_main", "Header", "Skin Art Editor",
            "Per-character PNG skins. Missing slots keep vanilla. Restart after Save."));
        list.Add(Entry(entryType, configType, "character", "Dropdown", "Character",
            "Character to configure (v1: Regent only).", "Regent", options: ["Regent"]));
        list.Add(Entry(entryType, configType, "enabled", "Toggle", "Enabled",
            "Enable custom skins for this character.", dto.Enabled));
        list.Add(Entry(entryType, configType, "knockout", "Toggle", "Knock out pose backdrops",
            "Combat/shop/rest: clear edge-connected near-black backgrounds when copying (Cassiopeia-style). UI images are never knocked out.",
            dto.KnockoutBackdrop));
        list.Add(Entry(entryType, configType, "knockout_threshold", "TextInput", "Knockout threshold",
            "Max r+g+b (0–255) treated as backdrop. Default 18.",
            dto.KnockoutThreshold.ToString(CultureInfo.InvariantCulture)));
        list.Add(Entry(entryType, configType, "sep_assets", "Separator", "", ""));

        foreach (var key in AssetKeys.All)
        {
            dto.Assets.TryGetValue(key, out var current);
            list.Add(Entry(entryType, configType, $"asset_{key}", "TextInput", key,
                $"Path for {key}. Empty = vanilla.", current ?? ""));
            list.Add(Entry(entryType, configType, $"browse_{key}", "Button", $"Browse {key}",
                $"Pick a PNG for {key}.", onChanged: _ => BrowseAndSet(key)));
            list.Add(Entry(entryType, configType, $"clear_{key}", "Button", $"Clear {key}",
                $"Remove custom {key}.", onChanged: _ => ClearKey(key)));
        }

        list.Add(Entry(entryType, configType, "sep_offsets", "Separator", "", ""));
        list.Add(Entry(entryType, configType, "header_offsets", "Header", "Offsets",
            "Applied only when that context is overridden."));
        list.Add(Entry(entryType, configType, "combat_pos", "TextInput", "Combat Position X,Y",
            "", FormatVec(dto.Offsets.CombatVisualsPosition)));
        list.Add(Entry(entryType, configType, "combat_scale", "TextInput", "Combat Scale",
            "", dto.Offsets.CombatVisualsScale.ToString(CultureInfo.InvariantCulture)));
        list.Add(Entry(entryType, configType, "combat_pad", "TextInput", "Combat Bottom Padding",
            "", dto.Offsets.CombatBottomPaddingPx.ToString(CultureInfo.InvariantCulture)));
        list.Add(Entry(entryType, configType, "form_vfx", "TextInput", "FormVfx Position X,Y",
            "", FormatVec(dto.Offsets.FormVfxPosition)));
        list.Add(Entry(entryType, configType, "shop_offset", "TextInput", "Shop Offset X,Y",
            "", FormatVec(dto.Offsets.ShopSpriteOffset)));
        list.Add(Entry(entryType, configType, "shop_scale", "TextInput", "Shop Scale",
            "", dto.Offsets.ShopSpriteScale.ToString(CultureInfo.InvariantCulture)));
        list.Add(Entry(entryType, configType, "rest_offset", "TextInput", "Rest Offset X,Y",
            "", FormatVec(dto.Offsets.RestDisplayOffset)));
        list.Add(Entry(entryType, configType, "rest_scale", "TextInput", "Rest Scale",
            "", dto.Offsets.RestSpriteScale.ToString(CultureInfo.InvariantCulture)));
        list.Add(Entry(entryType, configType, "rest_anchor", "TextInput", "Rest Seat Anchor X,Y",
            "", FormatVec(dto.Offsets.RestSeatAnchor)));
        list.Add(Entry(entryType, configType, "rest_bounds", "TextInput", "Rest Visible Bounds X,Y,W,H",
            "", FormatVec(dto.Offsets.RestVisibleBounds)));

        list.Add(Entry(entryType, configType, "save", "Button", "Save / Apply",
            "Write config + copy absolute paths into the mod folder. Restart to apply.",
            onChanged: _ => SaveFromModConfig()));

        return list;
    }

    private static object Entry(
        Type entryType,
        Type configType,
        string key,
        string typeName,
        string label,
        string description,
        object? defaultValue = null,
        Action<object>? onChanged = null,
        string[]? options = null)
    {
        var e = Activator.CreateInstance(entryType)!;
        entryType.GetProperty("Key")!.SetValue(e, key);
        entryType.GetProperty("Type")!.SetValue(e, Enum.Parse(configType, typeName));
        entryType.GetProperty("Label")!.SetValue(e, label);
        entryType.GetProperty("Description")!.SetValue(e, description);
        if (defaultValue != null)
            entryType.GetProperty("DefaultValue")?.SetValue(e, defaultValue);
        if (options != null)
            entryType.GetProperty("Options")?.SetValue(e, options);
        if (onChanged != null)
        {
            try
            {
                entryType.GetProperty("OnChanged")?.SetValue(e, onChanged);
            }
            catch
            {
                // ModConfig versions may differ slightly.
            }
        }
        return e;
    }

    private static void BrowseAndSet(string key)
    {
        if (_host == null || !GodotObject.IsInstanceValid(_host))
        {
            Log.Warn("No host node for file dialog");
            return;
        }

        FileBrowseHelper.BrowsePng(_host, $"Select {key}.png", path =>
        {
            try
            {
                var dto = SkinProfileLoader.LoadDtoOrDefault(_selectedCharacter);
                ApplyKnockoutSettingsFromModConfig(dto);
                var rel = AssetCopier.CopyAsset(
                    _selectedCharacter, key, path, dto.KnockoutBackdrop, dto.KnockoutThreshold);
                dto.Assets[key] = rel;
                SkinProfileLoader.Save(dto);
                SetModConfigValue($"asset_{key}", rel);
                Log.Info($"Set {key} = {rel}. Restart to apply.");
            }
            catch (Exception ex)
            {
                Log.Warn($"Browse failed: {ex.Message}");
            }
        });
    }

    private static void ClearKey(string key)
    {
        var dto = SkinProfileLoader.LoadDtoOrDefault(_selectedCharacter);
        AssetCopier.ClearAsset(_selectedCharacter, key, dto);
        SkinProfileLoader.Save(dto);
        SetModConfigValue($"asset_{key}", "");
        Log.Info($"Cleared {key}. Restart to restore vanilla for that slot.");
    }

    private static void SaveFromModConfig()
    {
        var charName = GetModConfigValue("character", "Regent");
        _selectedCharacter = charName.Equals("Regent", StringComparison.OrdinalIgnoreCase)
            ? "regent"
            : charName.ToLowerInvariant();

        var dto = SkinProfileLoader.LoadDtoOrDefault(_selectedCharacter);
        dto.Enabled = GetModConfigValue("enabled", dto.Enabled);
        ApplyKnockoutSettingsFromModConfig(dto);

        foreach (var key in AssetKeys.All)
        {
            var path = GetModConfigValue($"asset_{key}", dto.Assets.GetValueOrDefault(key) ?? "");
            if (string.IsNullOrWhiteSpace(path))
            {
                dto.Assets.Remove(key);
                continue;
            }

            if (Path.IsPathRooted(path) && File.Exists(path))
            {
                try
                {
                    dto.Assets[key] = AssetCopier.CopyAsset(
                        _selectedCharacter, key, path, dto.KnockoutBackdrop, dto.KnockoutThreshold);
                }
                catch (Exception ex) { Log.Warn($"Copy {key} failed: {ex.Message}"); }
            }
            else
            {
                dto.Assets[key] = path;
            }
        }

        dto.Offsets.CombatVisualsPosition = ParseVec(GetModConfigValue("combat_pos", FormatVec(dto.Offsets.CombatVisualsPosition)), dto.Offsets.CombatVisualsPosition);
        dto.Offsets.CombatVisualsScale = ParseFloat(GetModConfigValue("combat_scale", dto.Offsets.CombatVisualsScale.ToString(CultureInfo.InvariantCulture)), dto.Offsets.CombatVisualsScale);
        dto.Offsets.CombatBottomPaddingPx = ParseFloat(GetModConfigValue("combat_pad", dto.Offsets.CombatBottomPaddingPx.ToString(CultureInfo.InvariantCulture)), dto.Offsets.CombatBottomPaddingPx);
        dto.Offsets.FormVfxPosition = ParseVec(GetModConfigValue("form_vfx", FormatVec(dto.Offsets.FormVfxPosition)), dto.Offsets.FormVfxPosition);
        dto.Offsets.ShopSpriteOffset = ParseVec(GetModConfigValue("shop_offset", FormatVec(dto.Offsets.ShopSpriteOffset)), dto.Offsets.ShopSpriteOffset);
        dto.Offsets.ShopSpriteScale = ParseFloat(GetModConfigValue("shop_scale", dto.Offsets.ShopSpriteScale.ToString(CultureInfo.InvariantCulture)), dto.Offsets.ShopSpriteScale);
        dto.Offsets.RestDisplayOffset = ParseVec(GetModConfigValue("rest_offset", FormatVec(dto.Offsets.RestDisplayOffset)), dto.Offsets.RestDisplayOffset);
        dto.Offsets.RestSpriteScale = ParseFloat(GetModConfigValue("rest_scale", dto.Offsets.RestSpriteScale.ToString(CultureInfo.InvariantCulture)), dto.Offsets.RestSpriteScale);
        dto.Offsets.RestSeatAnchor = ParseVec(GetModConfigValue("rest_anchor", FormatVec(dto.Offsets.RestSeatAnchor)), dto.Offsets.RestSeatAnchor);
        dto.Offsets.RestVisibleBounds = ParseBounds(GetModConfigValue("rest_bounds", FormatVec(dto.Offsets.RestVisibleBounds)), dto.Offsets.RestVisibleBounds);

        SkinProfileLoader.Save(dto);
        SkinRegistry.ReloadAll();
        Log.Info("Saved skin config. Restart the game to fully apply scene overrides.");
    }

    private static void ApplyKnockoutSettingsFromModConfig(SkinProfileDto dto)
    {
        dto.KnockoutBackdrop = GetModConfigValue("knockout", dto.KnockoutBackdrop);
        var thresholdText = GetModConfigValue(
            "knockout_threshold",
            dto.KnockoutThreshold.ToString(CultureInfo.InvariantCulture));
        if (int.TryParse(thresholdText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var t) && t >= 0)
            dto.KnockoutThreshold = t;
        else
            dto.KnockoutThreshold = BackdropKnockout.DefaultThreshold;
    }

    private static void SetModConfigValue(string key, object value)
    {
        var apiType = Type.GetType("ModConfig.ModConfigApi, ModConfig", false);
        apiType?.GetMethod("SetValue", BindingFlags.Public | BindingFlags.Static)
            ?.Invoke(null, [ModId, key, value]);
    }

    private static T GetModConfigValue<T>(string key, T fallback)
    {
        try
        {
            var apiType = Type.GetType("ModConfig.ModConfigApi, ModConfig", false);
            var get = apiType?.GetMethod("GetValue", BindingFlags.Public | BindingFlags.Static);
            if (get == null)
                return fallback;
            var result = get.MakeGenericMethod(typeof(T)).Invoke(null, [ModId, key]);
            if (result is T t)
                return t;
            if (result != null)
                return (T)Convert.ChangeType(result, typeof(T), CultureInfo.InvariantCulture);
        }
        catch { /* ignore */ }
        return fallback;
    }

    private static string FormatVec(float[] v) =>
        string.Join(", ", v.Select(f => f.ToString(CultureInfo.InvariantCulture)));

    private static float[] ParseVec(string s, float[] fallback)
    {
        var parts = s.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
            return fallback;
        if (!float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x))
            return fallback;
        if (!float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
            return fallback;
        return [x, y];
    }

    private static float[] ParseBounds(string s, float[] fallback)
    {
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

    private static float ParseFloat(string s, float fallback) =>
        float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : fallback;
}
