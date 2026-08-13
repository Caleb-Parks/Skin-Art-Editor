using System.Collections.Generic;
using Godot;

namespace SkinArtEditor;

/// <summary>
/// Cassiopeia-style edge-connected near-black backdrop knockout for combat/shop/rest poses.
/// Only pixels connected to the image border with (r+g+b) &lt;= threshold are cleared.
/// </summary>
public static class BackdropKnockout
{
    public const int DefaultThreshold = 18;

    private static readonly HashSet<string> PoseKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        AssetKeys.IdleLoop,
        AssetKeys.Attack,
        AssetKeys.Cast,
        AssetKeys.Hurt,
        AssetKeys.Die,
        AssetKeys.RelaxedLoop,
        AssetKeys.RestLoop
    };

    public static bool ShouldProcess(string assetKey) => PoseKeys.Contains(assetKey);

    /// <summary>
    /// Load <paramref name="srcPath"/>, knock out edge-connected near-black backdrop, write PNG to <paramref name="dstPath"/>.
    /// </summary>
    public static void ProcessFile(string srcPath, string dstPath, int threshold = DefaultThreshold)
    {
        if (threshold < 0)
            threshold = 0;

        var godotSrc = srcPath.Replace('\\', '/');
        var image = Image.LoadFromFile(godotSrc);
        if (image == null)
            throw new InvalidOperationException($"Failed to load image for knockout: {srcPath}");

        if (image.GetFormat() != Image.Format.Rgba8)
            image.Convert(Image.Format.Rgba8);

        var cleared = KnockOut(image, threshold);

        var dir = Path.GetDirectoryName(dstPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var err = image.SavePng(dstPath.Replace('\\', '/'));
        if (err != Error.Ok)
            throw new InvalidOperationException($"Failed to save knocked-out PNG ({err}): {dstPath}");

        Log.Info($"Knockout wrote {Path.GetFileName(dstPath)} (cleared {cleared} backdrop pixels, threshold={threshold})");
    }

    internal static int KnockOut(Image image, int threshold)
    {
        var w = image.GetWidth();
        var h = image.GetHeight();
        if (w <= 0 || h <= 0)
            return 0;

        var visited = new bool[w * h];
        var queue = new Queue<(int x, int y)>();

        void TryEnqueue(int x, int y)
        {
            if ((uint)x >= (uint)w || (uint)y >= (uint)h)
                return;
            var i = y * w + x;
            if (visited[i])
                return;
            if (!IsBackdrop(image, x, y, threshold))
                return;
            visited[i] = true;
            queue.Enqueue((x, y));
        }

        for (var x = 0; x < w; x++)
        {
            TryEnqueue(x, 0);
            TryEnqueue(x, h - 1);
        }
        for (var y = 0; y < h; y++)
        {
            TryEnqueue(0, y);
            TryEnqueue(w - 1, y);
        }

        var cleared = 0;
        while (queue.Count > 0)
        {
            var (x, y) = queue.Dequeue();
            var c = image.GetPixel(x, y);
            image.SetPixel(x, y, new Color(c.R, c.G, c.B, 0f));
            cleared++;

            TryEnqueue(x - 1, y);
            TryEnqueue(x + 1, y);
            TryEnqueue(x, y - 1);
            TryEnqueue(x, y + 1);
        }

        return cleared;
    }

    private static bool IsBackdrop(Image image, int x, int y, int threshold)
    {
        var c = image.GetPixel(x, y);
        if (c.A <= 0f)
            return false;

        // Match Pillow 0–255 channel math: (r+g+b) <= threshold
        var r = (int)Math.Round(c.R * 255f);
        var g = (int)Math.Round(c.G * 255f);
        var b = (int)Math.Round(c.B * 255f);
        return r + g + b <= threshold;
    }
}
