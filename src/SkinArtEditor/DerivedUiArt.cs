using Godot;

namespace SkinArtEditor;

/// <summary>
/// Cassiopeia-style derived UI art: locked char-select portrait and character icon outline.
/// </summary>
public static class DerivedUiArt
{
    private const float LockedLumaScale = 0.22f;
    private const float OutlineAlphaThreshold = 32f / 255f;

    public static string? DerivedKeyForParent(string parentKey)
    {
        if (string.Equals(parentKey, AssetKeys.CharSelect, StringComparison.OrdinalIgnoreCase))
            return AssetKeys.CharSelectLocked;
        if (string.Equals(parentKey, AssetKeys.CharacterIcon, StringComparison.OrdinalIgnoreCase))
            return AssetKeys.CharacterIconOutline;
        return null;
    }

    /// <summary>
    /// After copying a parent asset, write the derived PNG and record it on <paramref name="dto"/>.
    /// </summary>
    public static void WriteDerivedFromParent(
        string characterId,
        string parentKey,
        string parentDestPath,
        SkinProfileDto dto)
    {
        var derivedKey = DerivedKeyForParent(parentKey);
        if (derivedKey == null)
            return;

        if (!File.Exists(parentDestPath))
        {
            Log.Warn($"DerivedUiArt: parent missing at {parentDestPath}");
            return;
        }

        var godotPath = parentDestPath.Replace('\\', '/');
        var source = Image.LoadFromFile(godotPath);
        if (source == null)
        {
            Log.Warn($"DerivedUiArt: failed to load parent {godotPath}");
            return;
        }

        if (source.GetFormat() != Image.Format.Rgba8)
            source.Convert(Image.Format.Rgba8);

        var derived = string.Equals(derivedKey, AssetKeys.CharSelectLocked, StringComparison.OrdinalIgnoreCase)
            ? MakeLocked(source)
            : MakeOutline(source);

        var dir = ModPaths.CharacterDir(characterId);
        Directory.CreateDirectory(dir);
        var destName = derivedKey + ".png";
        var destPath = Path.Combine(dir, destName);
        var err = derived.SavePng(destPath);
        if (err != Error.Ok)
            throw new InvalidOperationException($"Failed to save derived {derivedKey}: {err}");

        dto.Assets[derivedKey] = destName;
        Log.Info($"Derived {derivedKey} from {parentKey} -> {destPath}");
    }

    /// <summary>
    /// Ensure derived locked/outline entries exist for any parent already present on disk.
    /// </summary>
    public static void EnsureAll(string characterId, SkinProfileDto dto)
    {
        EnsureFromParent(characterId, AssetKeys.CharSelect, dto);
        EnsureFromParent(characterId, AssetKeys.CharacterIcon, dto);
    }

    private static void EnsureFromParent(string characterId, string parentKey, SkinProfileDto dto)
    {
        if (!dto.Assets.TryGetValue(parentKey, out var relative) || string.IsNullOrWhiteSpace(relative))
        {
            var derived = DerivedKeyForParent(parentKey);
            if (derived != null)
            {
                // Parent gone — drop derived config entry and file.
                var dir = ModPaths.CharacterDir(characterId);
                if (dto.Assets.TryGetValue(derived, out var derRel) && !string.IsNullOrWhiteSpace(derRel))
                {
                    var full = Path.Combine(dir, derRel);
                    if (File.Exists(full))
                    {
                        try { File.Delete(full); }
                        catch (Exception ex) { Log.Warn($"Could not delete {full}: {ex.Message}"); }
                    }
                }
                dto.Assets.Remove(derived);
            }
            return;
        }

        var parentPath = Path.IsPathRooted(relative)
            ? relative
            : Path.Combine(ModPaths.CharacterDir(characterId), relative);
        if (!File.Exists(parentPath))
            return;

        WriteDerivedFromParent(characterId, parentKey, parentPath, dto);
    }

    /// <summary>Dark grayscale locked portrait (keeps alpha).</summary>
    public static Image MakeLocked(Image source)
    {
        var w = source.GetWidth();
        var h = source.GetHeight();
        var result = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);

        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                var p = source.GetPixel(x, y);
                var gray = p.R * 0.299f + p.G * 0.587f + p.B * 0.114f;
                gray *= LockedLumaScale;
                result.SetPixel(x, y, new Color(gray, gray, gray, p.A));
            }
        }

        return result;
    }

    /// <summary>White opaque silhouette (Necro-style outline) with 3×3 max dilate on alpha.</summary>
    public static Image MakeOutline(Image source)
    {
        var w = source.GetWidth();
        var h = source.GetHeight();
        var mask = new byte[w * h];

        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                var a = source.GetPixel(x, y).A;
                mask[y * w + x] = a >= OutlineAlphaThreshold ? (byte)255 : (byte)0;
            }
        }

        var dilated = DilateMax3x3(mask, w, h);
        var result = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);

        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                var a = dilated[y * w + x] / 255f;
                result.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }

        return result;
    }

    private static byte[] DilateMax3x3(byte[] src, int w, int h)
    {
        var dst = new byte[src.Length];
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                byte max = 0;
                for (var dy = -1; dy <= 1; dy++)
                {
                    var ny = y + dy;
                    if (ny < 0 || ny >= h)
                        continue;
                    for (var dx = -1; dx <= 1; dx++)
                    {
                        var nx = x + dx;
                        if (nx < 0 || nx >= w)
                            continue;
                        var v = src[ny * w + nx];
                        if (v > max)
                            max = v;
                    }
                }
                dst[y * w + x] = max;
            }
        }
        return dst;
    }
}
