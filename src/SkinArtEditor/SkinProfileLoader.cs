using System.Text.Json;
using Godot;

namespace SkinArtEditor;

public static class ModPaths
{
    public const string ModId = "SkinArtEditor";

    private static string? _modRoot;

    public static string ModRoot
    {
        get
        {
            if (_modRoot != null)
                return _modRoot;

            var asm = typeof(ModPaths).Assembly.Location;
            if (!string.IsNullOrEmpty(asm))
            {
                var dir = Path.GetDirectoryName(asm);
                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                {
                    _modRoot = dir;
                    return _modRoot;
                }
            }

            _modRoot = Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile),
                "SkinArtEditor");
            return _modRoot;
        }
    }

    public static string CharactersRoot => Path.Combine(ModRoot, "characters");

    public static string CharacterDir(string characterId) =>
        Path.Combine(CharactersRoot, characterId.ToLowerInvariant());

    public static string ConfigPath(string characterId) =>
        Path.Combine(CharacterDir(characterId), "config.json");
}

public static class SkinProfileLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static SkinProfile? Load(string characterId)
    {
        var slug = characterId.ToLowerInvariant();
        var dir = ModPaths.CharacterDir(slug);
        var configPath = ModPaths.ConfigPath(slug);
        if (!File.Exists(configPath))
        {
            Log.Info($"No config for '{slug}' at {configPath}");
            return null;
        }

        SkinProfileDto dto;
        try
        {
            var json = File.ReadAllText(configPath);
            dto = JsonSerializer.Deserialize<SkinProfileDto>(json, JsonOptions) ?? new SkinProfileDto();
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to read config for '{slug}': {ex.Message}");
            return null;
        }

        Normalize(dto, slug);

        var resolved = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in AssetKeys.All)
        {
            if (!dto.Assets.TryGetValue(key, out var relative) || string.IsNullOrWhiteSpace(relative))
                continue;

            var full = Path.IsPathRooted(relative)
                ? relative
                : Path.Combine(dir, relative);

            if (!File.Exists(full))
            {
                Log.Warn($"[{slug}] asset '{key}' missing at {full} — using vanilla for that slot");
                continue;
            }

            resolved[key] = full;
        }

        var profile = new SkinProfile
        {
            CharacterId = slug,
            Directory = dir,
            Enabled = dto.Enabled,
            Offsets = dto.Offsets,
            ResolvedPaths = resolved
        };

        Log.Info(
            $"Loaded '{profile.CharacterId}' enabled={profile.Enabled} " +
            $"combat={profile.HasFullCombat} shop={profile.HasShop} rest={profile.HasRest} " +
            $"assets={profile.ResolvedPaths.Count}");
        return profile;
    }

    public static void Save(SkinProfileDto dto)
    {
        Normalize(dto, dto.CharacterId);
        var id = dto.CharacterId;
        var dir = ModPaths.CharacterDir(id);
        Directory.CreateDirectory(dir);

        var cleaned = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in dto.Assets)
        {
            if (!string.IsNullOrWhiteSpace(kv.Value))
                cleaned[kv.Key] = kv.Value;
        }
        dto.Assets = cleaned;

        var path = ModPaths.ConfigPath(id);
        var json = JsonSerializer.Serialize(dto, JsonOptions);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, json);
        File.Copy(tmp, path, overwrite: true);
        try { File.Delete(tmp); }
        catch { /* ignore */ }
        Log.Info($"Wrote config {path}");
    }

    public static SkinProfileDto LoadDtoOrDefault(string characterId)
    {
        var slug = characterId.ToLowerInvariant();
        var path = ModPaths.ConfigPath(slug);
        if (!File.Exists(path))
            return NewDefault(slug);

        try
        {
            var json = File.ReadAllText(path);
            var dto = JsonSerializer.Deserialize<SkinProfileDto>(json, JsonOptions);
            if (dto == null)
            {
                Log.Warn($"Config at {path} deserialized to null — using defaults (file left unchanged)");
                return NewDefault(slug);
            }

            Normalize(dto, slug);
            return dto;
        }
        catch (Exception ex)
        {
            // Preserve the broken file so Save cannot silently clobber it without a backup.
            var backup = path + ".corrupt." + DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            try
            {
                File.Copy(path, backup, overwrite: true);
                Log.Warn($"Corrupt config for '{slug}': {ex.Message}. Backed up to {backup}. Using defaults.");
            }
            catch (Exception backupEx)
            {
                Log.Warn(
                    $"Corrupt config for '{slug}': {ex.Message}. " +
                    $"Could not backup ({backupEx.Message}). Using defaults; fix or delete {path} before Save.");
            }

            return NewDefault(slug);
        }
    }

    /// <summary>
    /// Directory slug is authoritative. Null-safe assets; repair short/null offset arrays.
    /// </summary>
    public static void Normalize(SkinProfileDto dto, string directorySlug)
    {
        var slug = directorySlug.ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(dto.CharacterId) &&
            !dto.CharacterId.Equals(slug, StringComparison.OrdinalIgnoreCase))
        {
            Log.Warn(
                $"config characterId '{dto.CharacterId}' does not match folder '{slug}' — using folder slug");
        }

        dto.CharacterId = slug;
        dto.Assets ??= new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        // Re-wrap to ensure case-insensitive comparer after JSON deserialize.
        dto.Assets = new Dictionary<string, string?>(dto.Assets, StringComparer.OrdinalIgnoreCase);
        dto.Offsets ??= new SkinOffsetsDto();
        NormalizeOffsets(dto.Offsets, slug);

        if (dto.KnockoutThreshold < 0)
            dto.KnockoutThreshold = BackdropKnockout.DefaultThreshold;
    }

    private static void NormalizeOffsets(SkinOffsetsDto o, string slug)
    {
        var defaults = new SkinOffsetsDto();
        o.CombatVisualsPosition = RequireLen(o.CombatVisualsPosition, 2, defaults.CombatVisualsPosition, slug, "combatVisualsPosition");
        o.FormVfxPosition = RequireLen(o.FormVfxPosition, 2, defaults.FormVfxPosition, slug, "formVfxPosition");
        o.ShopSpriteOffset = RequireLen(o.ShopSpriteOffset, 2, defaults.ShopSpriteOffset, slug, "shopSpriteOffset");
        o.RestDisplayOffset = RequireLen(o.RestDisplayOffset, 2, defaults.RestDisplayOffset, slug, "restDisplayOffset");
        o.RestSeatAnchor = RequireLen(o.RestSeatAnchor, 2, defaults.RestSeatAnchor, slug, "restSeatAnchor");
        o.RestVisibleBounds = RequireLen(o.RestVisibleBounds, 4, defaults.RestVisibleBounds, slug, "restVisibleBounds");

        if (!IsFinite(o.CombatVisualsScale) || o.CombatVisualsScale == 0f)
        {
            Log.Warn($"[{slug}] invalid combatVisualsScale — using {defaults.CombatVisualsScale}");
            o.CombatVisualsScale = defaults.CombatVisualsScale;
        }
        if (!IsFinite(o.ShopSpriteScale) || o.ShopSpriteScale == 0f)
        {
            Log.Warn($"[{slug}] invalid shopSpriteScale — using {defaults.ShopSpriteScale}");
            o.ShopSpriteScale = defaults.ShopSpriteScale;
        }
        if (!IsFinite(o.RestSpriteScale) || o.RestSpriteScale == 0f)
        {
            Log.Warn($"[{slug}] invalid restSpriteScale — using {defaults.RestSpriteScale}");
            o.RestSpriteScale = defaults.RestSpriteScale;
        }
        if (!IsFinite(o.CharSelectBgZoom) || o.CharSelectBgZoom <= 0f)
            o.CharSelectBgZoom = CharSelectBgFramer.DefaultZoom;
        if (!IsFinite(o.CharSelectBgOffsetX))
            o.CharSelectBgOffsetX = CharSelectBgFramer.DefaultOffsetX;
        if (!IsFinite(o.CharSelectBgOffsetY))
            o.CharSelectBgOffsetY = CharSelectBgFramer.DefaultOffsetY;
        if (!IsFinite(o.CombatBottomPaddingPx))
            o.CombatBottomPaddingPx = defaults.CombatBottomPaddingPx;
    }

    private static float[] RequireLen(float[]? arr, int len, float[] fallback, string slug, string name)
    {
        if (arr != null && arr.Length >= len && arr.Take(len).All(IsFinite))
        {
            if (arr.Length == len)
                return arr;
            return arr.Take(len).ToArray();
        }

        Log.Warn($"[{slug}] invalid {name} — using defaults [{string.Join(", ", fallback)}]");
        return (float[])fallback.Clone();
    }

    private static bool IsFinite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);

    private static SkinProfileDto NewDefault(string slug) => new()
    {
        CharacterId = slug,
        Enabled = true,
        Assets = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase),
        Offsets = new SkinOffsetsDto()
    };
}

public static class AssetCopier
{
    public static string CopyAsset(string characterId, string assetKey, string sourcePath)
    {
        var dto = SkinProfileLoader.LoadDtoOrDefault(characterId);
        return CopyAsset(characterId, assetKey, sourcePath, dto.KnockoutBackdrop, dto.KnockoutThreshold, dto);
    }

    public static string CopyAsset(
        string characterId,
        string assetKey,
        string sourcePath,
        bool knockoutBackdrop,
        int knockoutThreshold,
        SkinProfileDto? dto = null)
    {
        if (!File.Exists(sourcePath))
            throw new FileNotFoundException("Selected art file not found", sourcePath);

        var dir = ModPaths.CharacterDir(characterId);
        Directory.CreateDirectory(dir);

        var shouldKnock =
            knockoutBackdrop &&
            BackdropKnockout.ShouldProcess(assetKey);

        var ext = shouldKnock ? ".png" : Path.GetExtension(sourcePath);
        if (string.IsNullOrWhiteSpace(ext))
            ext = ".png";

        var destName = assetKey + ext.ToLowerInvariant();
        var destPath = Path.Combine(dir, destName);

        if (shouldKnock)
        {
            var threshold = knockoutThreshold > 0 ? knockoutThreshold : BackdropKnockout.DefaultThreshold;
            BackdropKnockout.ProcessFile(sourcePath, destPath, threshold);
        }
        else
        {
            File.Copy(sourcePath, destPath, overwrite: true);
            Log.Info($"Copied {sourcePath} -> {destPath}");
        }

        if (DerivedUiArt.DerivedKeyForParent(assetKey) != null)
        {
            dto ??= SkinProfileLoader.LoadDtoOrDefault(characterId);
            DerivedUiArt.WriteDerivedFromParent(characterId, assetKey, destPath, dto);
        }

        return destName;
    }

    public static void ClearAsset(string characterId, string assetKey, SkinProfileDto dto)
    {
        ClearOne(characterId, assetKey, dto);

        var derived = DerivedUiArt.DerivedKeyForParent(assetKey);
        if (derived != null)
            ClearOne(characterId, derived, dto);
    }

    private static void ClearOne(string characterId, string assetKey, SkinProfileDto dto)
    {
        if (dto.Assets.TryGetValue(assetKey, out var relative) && !string.IsNullOrWhiteSpace(relative))
        {
            var full = Path.IsPathRooted(relative)
                ? relative
                : Path.Combine(ModPaths.CharacterDir(characterId), relative);
            if (File.Exists(full))
            {
                try { File.Delete(full); }
                catch (Exception ex) { Log.Warn($"Could not delete {full}: {ex.Message}"); }
            }
        }

        dto.Assets.Remove(assetKey);
    }
}

internal static class Log
{
    public static void Info(string message) => GD.Print($"[SkinArtEditor] {message}");
    public static void Warn(string message) => GD.PushWarning($"[SkinArtEditor] {message}");
}
