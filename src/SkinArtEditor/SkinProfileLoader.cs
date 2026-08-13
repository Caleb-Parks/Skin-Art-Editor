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
        var dir = ModPaths.CharacterDir(characterId);
        var configPath = ModPaths.ConfigPath(characterId);
        if (!File.Exists(configPath))
        {
            Log.Info($"No config for '{characterId}' at {configPath}");
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
            Log.Warn($"Failed to read config for '{characterId}': {ex.Message}");
            return null;
        }

        if (string.IsNullOrWhiteSpace(dto.CharacterId))
            dto.CharacterId = characterId.ToLowerInvariant();

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
                Log.Warn($"[{characterId}] asset '{key}' missing at {full} — using vanilla for that slot");
                continue;
            }

            resolved[key] = full;
        }

        var profile = new SkinProfile
        {
            CharacterId = dto.CharacterId.ToLowerInvariant(),
            Directory = dir,
            Enabled = dto.Enabled,
            Offsets = dto.Offsets ?? new SkinOffsetsDto(),
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
        var id = dto.CharacterId.ToLowerInvariant();
        dto.CharacterId = id;
        var dir = ModPaths.CharacterDir(id);
        Directory.CreateDirectory(dir);

        // Drop null asset entries for a clean file.
        var cleaned = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in dto.Assets)
        {
            if (!string.IsNullOrWhiteSpace(kv.Value))
                cleaned[kv.Key] = kv.Value;
        }
        dto.Assets = cleaned;

        var path = ModPaths.ConfigPath(id);
        var json = JsonSerializer.Serialize(dto, JsonOptions);
        File.WriteAllText(path, json);
        Log.Info($"Wrote config {path}");
    }

    public static SkinProfileDto LoadDtoOrDefault(string characterId)
    {
        var path = ModPaths.ConfigPath(characterId);
        if (!File.Exists(path))
        {
            return new SkinProfileDto
            {
                CharacterId = characterId.ToLowerInvariant(),
                Enabled = true,
                Assets = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase),
                Offsets = new SkinOffsetsDto()
            };
        }

        try
        {
            var json = File.ReadAllText(path);
            var dto = JsonSerializer.Deserialize<SkinProfileDto>(json, JsonOptions)
                      ?? new SkinProfileDto { CharacterId = characterId };
            dto.CharacterId = characterId.ToLowerInvariant();
            dto.Assets ??= new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            dto.Offsets ??= new SkinOffsetsDto();
            return dto;
        }
        catch
        {
            return new SkinProfileDto
            {
                CharacterId = characterId.ToLowerInvariant(),
                Enabled = true,
                Assets = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase),
                Offsets = new SkinOffsetsDto()
            };
        }
    }
}

public static class AssetCopier
{
    public static string CopyAsset(string characterId, string assetKey, string sourcePath)
    {
        var dto = SkinProfileLoader.LoadDtoOrDefault(characterId);
        return CopyAsset(characterId, assetKey, sourcePath, dto.KnockoutBackdrop, dto.KnockoutThreshold);
    }

    public static string CopyAsset(
        string characterId,
        string assetKey,
        string sourcePath,
        bool knockoutBackdrop,
        int knockoutThreshold)
    {
        if (!File.Exists(sourcePath))
            throw new FileNotFoundException("Selected art file not found", sourcePath);

        var dir = ModPaths.CharacterDir(characterId);
        Directory.CreateDirectory(dir);

        var shouldKnock =
            knockoutBackdrop &&
            BackdropKnockout.ShouldProcess(assetKey);

        // Processed poses always land as .png; UI/other assets keep source extension.
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

        return destName;
    }

    public static void ClearAsset(string characterId, string assetKey, SkinProfileDto dto)
    {
        if (dto.Assets.TryGetValue(assetKey, out var relative) && !string.IsNullOrWhiteSpace(relative))
        {
            var full = Path.Combine(ModPaths.CharacterDir(characterId), relative);
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
