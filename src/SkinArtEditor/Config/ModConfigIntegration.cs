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
    private static Node? _host;

    public static bool TryRegister(Node host)
    {
        _host = host;
        SkinConfigService.SelectedCharacterId = CharacterCatalog.List()[0].Slug;

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
        var catalog = CharacterCatalog.List();
        var selected = SkinConfigService.SelectedCharacterId;
        var dto = SkinProfileLoader.LoadDtoOrDefault(selected);
        var displayNames = catalog.Select(e => e.DisplayName).ToArray();
        var selectedDisplay = CharacterCatalog.DisplayNameFor(selected);

        list.Add(Entry(entryType, configType, "header_main", "Header", "Skin Art Editor",
            "Per-character PNG skins. Missing slots keep vanilla. Restart after Save."));
        list.Add(Entry(entryType, configType, "character", "Dropdown", "Character",
            "Character folder under characters/. Changing this reloads fields for that profile.",
            selectedDisplay, options: displayNames,
            onChanged: v => OnCharacterChanged(v?.ToString())));
        list.Add(Entry(entryType, configType, "enabled", "Toggle", "Enabled",
            "Enable custom skins for this character.", dto.Enabled));
        list.Add(Entry(entryType, configType, "knockout", "Toggle", "Knock out backdrops",
            "Combat/shop/rest + icon/map marker: clear edge-connected near-black backgrounds when copying. Locked portrait and icon outline are auto-derived. Char-select portraits/BG are never knocked out.",
            dto.KnockoutBackdrop));
        list.Add(Entry(entryType, configType, "knockout_threshold", "TextInput", "Knockout threshold",
            "Max r+g+b (0–255) treated as backdrop. Default 18.",
            dto.KnockoutThreshold.ToString(CultureInfo.InvariantCulture)));
        list.Add(Entry(entryType, configType, "sep_assets", "Separator", "", ""));

        foreach (var key in AssetKeys.UserSelectable)
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
            "Applied only when that context is overridden. Tuned per character in config.json."));
        foreach (var (key, value) in SkinConfigService.ReadOffsetFields(dto.Offsets))
        {
            var label = OffsetLabel(key);
            var desc = key.StartsWith("char_select_bg", StringComparison.Ordinal)
                ? "Cassiopeia-style contain framing. Browsing a new BG resets zoom/offset defaults."
                : "";
            list.Add(Entry(entryType, configType, key, "TextInput", label, desc, value));
        }

        list.Add(Entry(entryType, configType, "save", "Button", "Save / Apply",
            "Write config + copy absolute paths into the mod folder. Restart to apply.",
            onChanged: _ => SaveFromModConfig()));

        return list;
    }

    private static string OffsetLabel(string key) => key switch
    {
        "combat_pos" => "Combat Position X,Y",
        "combat_scale" => "Combat Scale",
        "combat_pad" => "Combat Bottom Padding",
        "form_vfx" => "FormVfx Position X,Y",
        "shop_offset" => "Shop Offset X,Y",
        "shop_scale" => "Shop Scale",
        "rest_offset" => "Rest Offset X,Y",
        "rest_scale" => "Rest Scale",
        "rest_anchor" => "Rest Seat Anchor X,Y",
        "rest_bounds" => "Rest Visible Bounds X,Y,W,H",
        "char_select_bg_zoom" => "Char Select BG Zoom",
        "char_select_bg_offset" => "Char Select BG Offset X,Y",
        _ => key
    };

    private static void OnCharacterChanged(string? display)
    {
        SkinConfigService.SelectedCharacterId = CharacterCatalog.SlugFromDisplay(display);
        PushDtoToModConfig(SkinProfileLoader.LoadDtoOrDefault(SkinConfigService.SelectedCharacterId));
        Log.Info($"ModConfig character → {SkinConfigService.SelectedCharacterId}");
    }

    private static void PushDtoToModConfig(SkinProfileDto dto)
    {
        SetModConfigValue("enabled", dto.Enabled);
        SetModConfigValue("knockout", dto.KnockoutBackdrop);
        SetModConfigValue("knockout_threshold",
            dto.KnockoutThreshold.ToString(CultureInfo.InvariantCulture));
        foreach (var key in AssetKeys.UserSelectable)
        {
            dto.Assets.TryGetValue(key, out var val);
            SetModConfigValue($"asset_{key}", val ?? "");
        }

        foreach (var (key, value) in SkinConfigService.ReadOffsetFields(dto.Offsets))
            SetModConfigValue(key, value);
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

        var characterId = SkinConfigService.SelectedCharacterId;
        FileBrowseHelper.BrowsePng(_host, $"Select {key}.png", path =>
        {
            try
            {
                var dto = SkinProfileLoader.LoadDtoOrDefault(characterId);
                dto.KnockoutBackdrop = GetModConfigValue("knockout", dto.KnockoutBackdrop);
                dto.KnockoutThreshold = SkinConfigService.ParseThreshold(
                    GetModConfigValue("knockout_threshold",
                        dto.KnockoutThreshold.ToString(CultureInfo.InvariantCulture)),
                    BackdropKnockout.DefaultThreshold);

                SkinConfigService.SetUserAsset(characterId, dto, key, path);
                SkinConfigService.SaveAndReload(dto);
                SetModConfigValue($"asset_{key}", dto.Assets.GetValueOrDefault(key) ?? "");
                if (string.Equals(key, AssetKeys.CharSelectBg, StringComparison.OrdinalIgnoreCase))
                {
                    SetModConfigValue("char_select_bg_zoom",
                        dto.Offsets.CharSelectBgZoom.ToString(CultureInfo.InvariantCulture));
                    SetModConfigValue("char_select_bg_offset",
                        SkinConfigService.FormatVec([dto.Offsets.CharSelectBgOffsetX, dto.Offsets.CharSelectBgOffsetY]));
                }

                Log.Info($"Set {key} = {dto.Assets.GetValueOrDefault(key)}. Restart to apply.");
            }
            catch (Exception ex)
            {
                Log.Warn($"Browse failed: {ex.Message}");
            }
        });
    }

    private static void ClearKey(string key)
    {
        var characterId = SkinConfigService.SelectedCharacterId;
        var dto = SkinProfileLoader.LoadDtoOrDefault(characterId);
        AssetCopier.ClearAsset(characterId, key, dto);
        SkinConfigService.SaveAndReload(dto);
        SetModConfigValue($"asset_{key}", "");
        Log.Info($"Cleared {key}. Restart to restore vanilla for that slot.");
    }

    private static void SaveFromModConfig()
    {
        var display = GetModConfigValue("character", CharacterCatalog.DisplayNameFor(SkinConfigService.SelectedCharacterId));
        SkinConfigService.SelectedCharacterId = CharacterCatalog.SlugFromDisplay(display);
        var characterId = SkinConfigService.SelectedCharacterId;

        var dto = SkinProfileLoader.LoadDtoOrDefault(characterId);
        dto.Enabled = GetModConfigValue("enabled", dto.Enabled);
        dto.KnockoutBackdrop = GetModConfigValue("knockout", dto.KnockoutBackdrop);
        dto.KnockoutThreshold = SkinConfigService.ParseThreshold(
            GetModConfigValue("knockout_threshold",
                dto.KnockoutThreshold.ToString(CultureInfo.InvariantCulture)),
            BackdropKnockout.DefaultThreshold);

        foreach (var key in AssetKeys.UserSelectable)
        {
            var path = GetModConfigValue($"asset_{key}", dto.Assets.GetValueOrDefault(key) ?? "");
            if (string.IsNullOrWhiteSpace(path))
            {
                AssetCopier.ClearAsset(characterId, key, dto);
                continue;
            }

            try
            {
                SkinConfigService.SetUserAsset(characterId, dto, key, path);
            }
            catch (Exception ex)
            {
                Log.Warn($"Copy {key} failed: {ex.Message}");
            }
        }

        var offsetFields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in SkinConfigService.ReadOffsetFields(dto.Offsets).Keys)
            offsetFields[key] = GetModConfigValue(key, SkinConfigService.ReadOffsetFields(dto.Offsets)[key]);
        SkinConfigService.ApplyOffsetFields(dto.Offsets, offsetFields);

        SkinConfigService.SaveAndReload(dto);
        Log.Info("Saved skin config. Restart the game to fully apply scene overrides.");
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
}
