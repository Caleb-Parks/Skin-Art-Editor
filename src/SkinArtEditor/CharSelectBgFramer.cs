using System.Globalization;
using Godot;

namespace SkinArtEditor;

/// <summary>
/// Frames char_select_bg into a 2560×1200 canvas using Cassiopeia's pipeline:
/// contain → zoom → top-align → horizontal center + offset.
/// </summary>
public static class CharSelectBgFramer
{
    public const int CanvasWidth = 2560;
    public const int CanvasHeight = 1200;

    /// <summary>Cassiopeia master-prep defaults (contain zoom + 10% left shift).</summary>
    public const float DefaultZoom = 1.2f;
    public const float DefaultOffsetX = -0.1f;
    public const float DefaultOffsetY = 0f;

    private static readonly Dictionary<string, ImageTexture> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static void Clear()
    {
        foreach (var tex in Cache.Values)
        {
            if (GodotObject.IsInstanceValid(tex))
                tex.Dispose();
        }
        Cache.Clear();
    }

    public static void ApplyCassiopeiaDefaults(SkinOffsetsDto offsets)
    {
        offsets.CharSelectBgZoom = DefaultZoom;
        offsets.CharSelectBgOffsetX = DefaultOffsetX;
        offsets.CharSelectBgOffsetY = DefaultOffsetY;
    }

    public static ImageTexture? LoadFramed(SkinProfile profile)
    {
        if (!profile.TryGetPath(AssetKeys.CharSelectBg, out var path))
            return null;

        var o = profile.Offsets;
        var zoom = o.CharSelectBgZoom <= 0f ? DefaultZoom : o.CharSelectBgZoom;
        var offsetX = o.CharSelectBgOffsetX;
        var offsetY = o.CharSelectBgOffsetY;
        var key = CacheKey(path, zoom, offsetX, offsetY);

        if (Cache.TryGetValue(key, out var existing) && GodotObject.IsInstanceValid(existing))
            return existing;

        try
        {
            var godotPath = path.Replace('\\', '/');
            var source = Image.LoadFromFile(godotPath);
            if (source == null)
            {
                Log.Warn($"CharSelectBgFramer: failed to load {godotPath}");
                return null;
            }

            if (source.GetFormat() != Image.Format.Rgba8)
                source.Convert(Image.Format.Rgba8);

            var framed = Frame(source, zoom, offsetX, offsetY);
            var tex = ImageTexture.CreateFromImage(framed);
            Cache[key] = tex;
            Log.Info(
                $"[{profile.CharacterId}] Framed char_select_bg " +
                $"{source.GetWidth()}x{source.GetHeight()} → {CanvasWidth}x{CanvasHeight} " +
                $"(contain zoom={zoom.ToString(CultureInfo.InvariantCulture)}, " +
                $"offset={offsetX.ToString(CultureInfo.InvariantCulture)}," +
                $"{offsetY.ToString(CultureInfo.InvariantCulture)})");
            return tex;
        }
        catch (Exception ex)
        {
            Log.Warn($"CharSelectBgFramer: {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    public static Image Frame(Image source, float zoom, float offsetX, float offsetY)
    {
        var srcW = source.GetWidth();
        var srcH = source.GetHeight();
        if (srcW <= 0 || srcH <= 0)
            throw new InvalidOperationException("char_select_bg image has invalid size");

        // ImageOps.contain into 2560×1200, then zoom.
        var safeZoom = zoom <= 0f ? DefaultZoom : zoom;
        var fitScale = Math.Min(CanvasWidth / (float)srcW, CanvasHeight / (float)srcH);
        var fittedW = Math.Max(1, (int)Math.Round(srcW * fitScale));
        var fittedH = Math.Max(1, (int)Math.Round(srcH * fitScale));
        var drawnW = Math.Max(1, (int)Math.Round(fittedW * safeZoom));
        var drawnH = Math.Max(1, (int)Math.Round(fittedH * safeZoom));

        var scaled = new Image();
        scaled.CopyFrom(source);
        if (scaled.GetWidth() != drawnW || scaled.GetHeight() != drawnH)
            scaled.Resize(drawnW, drawnH, Image.Interpolation.Lanczos);

        var corner = source.GetPixel(0, 0);
        var fill = new Color(corner.R, corner.G, corner.B, 1f);

        var canvas = Image.CreateEmpty(CanvasWidth, CanvasHeight, false, Image.Format.Rgba8);
        canvas.Fill(fill);

        // Center horizontally, then apply offset; top-align vertically + offsetY.
        var ox = (CanvasWidth - drawnW) / 2 + (int)Math.Round(offsetX * CanvasWidth);
        var oy = (int)Math.Round(offsetY * CanvasHeight);

        var srcX = Math.Max(0, -ox);
        var srcY = Math.Max(0, -oy);
        var dstX = Math.Max(0, ox);
        var dstY = Math.Max(0, oy);
        var copyW = Math.Min(drawnW - srcX, CanvasWidth - dstX);
        var copyH = Math.Min(drawnH - srcY, CanvasHeight - dstY);

        if (copyW > 0 && copyH > 0)
        {
            canvas.BlitRect(
                scaled,
                new Rect2I(srcX, srcY, copyW, copyH),
                new Vector2I(dstX, dstY));
        }

        return canvas;
    }

    private static string CacheKey(string path, float zoom, float offsetX, float offsetY) =>
        string.Create(CultureInfo.InvariantCulture,
            $"{path}|z={zoom:R}|x={offsetX:R}|y={offsetY:R}");
}
