using Godot;
using MegaCrit.Sts2.Core.Models;

namespace SkinArtEditor;

public static class SkinRegistry
{
    private static readonly Dictionary<string, SkinProfile> Profiles = new(StringComparer.OrdinalIgnoreCase);

    public static void ReloadAll()
    {
        Profiles.Clear();
        TextureCache.Clear();

        if (!Directory.Exists(ModPaths.CharactersRoot))
        {
            Log.Info($"No characters folder at {ModPaths.CharactersRoot}");
            return;
        }

        foreach (var dir in Directory.GetDirectories(ModPaths.CharactersRoot))
        {
            var id = Path.GetFileName(dir);
            var profile = SkinProfileLoader.Load(id);
            if (profile == null || !profile.Enabled)
                continue;
            Profiles[profile.CharacterId] = profile;
        }

        Log.Info($"Registry ready: {Profiles.Count} enabled profile(s)");
    }

    public static bool TryGet(string characterId, out SkinProfile profile) =>
        Profiles.TryGetValue(characterId.ToLowerInvariant(), out profile!);

    public static bool TryGet(CharacterModel model, out SkinProfile profile)
    {
        profile = null!;
        if (model?.Id?.Entry == null)
            return false;
        return TryGet(model.Id.Entry.ToLowerInvariant(), out profile);
    }

    public static string Slug(CharacterModel model) =>
        model.Id.Entry.ToLowerInvariant();

    public static IEnumerable<string> KnownCharacterIds() =>
        CharacterCatalog.List().Select(e => e.Slug);
}

public static class TextureCache
{
    private static readonly Dictionary<string, ImageTexture> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static void Clear()
    {
        foreach (var tex in Cache.Values)
        {
            if (GodotObject.IsInstanceValid(tex))
                tex.Dispose();
        }
        Cache.Clear();
        CharSelectBgFramer.Clear();
    }

    public static ImageTexture? Load(string absolutePath)
    {
        if (string.IsNullOrWhiteSpace(absolutePath) || !File.Exists(absolutePath))
            return null;

        if (Cache.TryGetValue(absolutePath, out var existing) && GodotObject.IsInstanceValid(existing))
            return existing;

        try
        {
            // Godot file APIs prefer forward slashes on all platforms.
            var godotPath = absolutePath.Replace('\\', '/');
            var image = Image.LoadFromFile(godotPath);
            if (image == null)
            {
                Log.Warn($"Image.LoadFromFile returned null for {godotPath}");
                return null;
            }

            var tex = ImageTexture.CreateFromImage(image);
            Cache[absolutePath] = tex;
            return tex;
        }
        catch (Exception ex)
        {
            Log.Warn($"Failed to load texture {absolutePath}: {ex.Message}");
            return null;
        }
    }

    public static ImageTexture? LoadAsset(SkinProfile profile, string key)
    {
        if (!profile.TryGetPath(key, out var path))
            return null;
        return Load(path);
    }
}

public static class ScenePaths
{
    public const string Combat = "res://scenes/creature_visuals/png_combat.tscn";
    public const string Merchant = "res://scenes/merchant/characters/png_merchant.tscn";
    public const string RestSite = "res://scenes/rest_site/characters/png_rest_site.tscn";
    public const string CharSelectBg = "res://scenes/screens/char_select/png_char_select_bg.tscn";
}

public static class SkinApplier
{
    public static void ApplyCombat(Node visualsRoot, SkinProfile profile)
    {
        var offsets = profile.Offsets;
        var formVfx = visualsRoot.GetNodeOrNull("%FormVfx") as Control
                      ?? visualsRoot.GetNodeOrNull("FormVfx") as Control;
        if (formVfx != null)
        {
            formVfx.Position = ToVec2(offsets.FormVfxPosition);
        }

        var body = visualsRoot.GetNodeOrNull("%Visuals")
                   ?? visualsRoot.GetNodeOrNull("Visuals");
        if (body == null)
        {
            Log.Warn("Combat visuals missing Visuals node");
            return;
        }

        if (body is Node2D body2d)
        {
            body2d.Position = ToVec2(offsets.CombatVisualsPosition);
            var scale = offsets.CombatVisualsScale;
            body2d.Scale = new Vector2(scale, scale);
        }

        var idle = TextureCache.LoadAsset(profile, AssetKeys.IdleLoop);
        var attack = TextureCache.LoadAsset(profile, AssetKeys.Attack);
        var cast = TextureCache.LoadAsset(profile, AssetKeys.Cast);
        var hurt = TextureCache.LoadAsset(profile, AssetKeys.Hurt);
        var die = TextureCache.LoadAsset(profile, AssetKeys.Die);

        if (idle == null || attack == null || cast == null || hurt == null || die == null)
        {
            Log.Warn($"[{profile.CharacterId}] incomplete combat textures after load");
            return;
        }

        if (body.HasMethod("apply_skin"))
        {
            body.Call(
                "apply_skin",
                idle, attack, cast, hurt, die,
                offsets.CombatBottomPaddingPx);
        }
    }

    public static void ApplyMerchant(Node merchantRoot, SkinProfile profile)
    {
        var sprite = merchantRoot.GetNodeOrNull("RelaxedSprite");
        if (sprite == null)
        {
            Log.Warn("Merchant scene missing RelaxedSprite");
            return;
        }

        var tex = TextureCache.LoadAsset(profile, AssetKeys.RelaxedLoop);
        if (tex == null)
            return;

        var offsets = profile.Offsets;
        if (sprite.HasMethod("apply_skin"))
        {
            sprite.Call(
                "apply_skin",
                tex,
                ToVec2(offsets.ShopSpriteOffset),
                offsets.ShopSpriteScale);
        }
        else if (sprite is Sprite2D s)
        {
            s.Texture = tex;
            s.Offset = ToVec2(offsets.ShopSpriteOffset);
            s.Scale = new Vector2(offsets.ShopSpriteScale, offsets.ShopSpriteScale);
        }
    }

    public static void ApplyRest(Node restRoot, SkinProfile profile)
    {
        var visuals = restRoot.GetNodeOrNull("Visuals");
        if (visuals == null)
        {
            Log.Warn("Rest scene missing Visuals");
            return;
        }

        var tex = TextureCache.LoadAsset(profile, AssetKeys.RestLoop);
        if (tex == null)
            return;

        var o = profile.Offsets;
        var bounds = o.RestVisibleBounds;
        var rect = new Rect2(bounds[0], bounds[1], bounds[2], bounds[3]);
        var anchor = ToVec2(o.RestSeatAnchor);
        var offset = ToVec2(o.RestDisplayOffset);

        if (visuals.HasMethod("apply_skin"))
        {
            visuals.Call("apply_skin", tex, rect, anchor, offset, o.RestSpriteScale);
        }
    }

    public static void ApplyCharSelectBg(Node bgRoot, SkinProfile profile)
    {
        var tex = CharSelectBgFramer.LoadFramed(profile)
                  ?? TextureCache.LoadAsset(profile, AssetKeys.CharSelectBg);
        if (tex == null)
            return;

        if (!GodotObject.IsInstanceValid(bgRoot))
            return;

        if (bgRoot.HasMethod("apply_skin"))
        {
            bgRoot.Call("apply_skin", tex);
            return;
        }

        var bg = bgRoot.GetNodeOrNull("Bg") as TextureRect
                 ?? FindTextureRect(bgRoot);
        if (bg != null)
            bg.Texture = tex;
    }

    private static TextureRect? FindTextureRect(Node root)
    {
        if (root is TextureRect self)
            return self;
        foreach (var child in root.GetChildren())
        {
            if (child is TextureRect tr)
                return tr;
            if (child is Node n)
            {
                var nested = FindTextureRect(n);
                if (nested != null)
                    return nested;
            }
        }
        return null;
    }

    /// <summary>
    /// Map a vanilla res:// character_icon path to a loaded custom texture, if configured.
    /// </summary>
    public static bool TryOverrideTopPanelIconPath(string? path, out Texture2D texture)
    {
        texture = null!;
        if (string.IsNullOrEmpty(path))
            return false;

        var normalized = path.Replace('\\', '/');
        const string marker = "character_icon_";
        var idx = normalized.LastIndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
            return false;

        var file = Path.GetFileNameWithoutExtension(normalized);
        // character_icon_regent or character_icon_regent_outline
        if (!file.StartsWith(marker, StringComparison.OrdinalIgnoreCase))
            return false;

        var rest = file.Substring(marker.Length);
        var isOutline = rest.EndsWith("_outline", StringComparison.OrdinalIgnoreCase);
        var slug = isOutline ? rest[..^"_outline".Length] : rest;
        if (string.IsNullOrEmpty(slug))
            return false;

        if (!SkinRegistry.TryGet(slug, out var profile))
            return false;

        var key = isOutline ? AssetKeys.CharacterIconOutline : AssetKeys.CharacterIcon;
        var loaded = TextureCache.LoadAsset(profile, key);
        if (loaded == null)
            return false;

        texture = loaded;
        return true;
    }

    /// <summary>Retexture a Control or TextureRect that displays a character top-panel icon.</summary>
    public static void ApplyCharacterIconToNode(Node node, SkinProfile profile)
    {
        if (node is TextureRect tr)
        {
            var icon = TextureCache.LoadAsset(profile, AssetKeys.CharacterIcon);
            if (icon != null)
                tr.Texture = icon;
            return;
        }

        ApplyCharacterIconScene(node, profile);
    }

    /// <summary>
    /// Retexture a vanilla character Icon Control (top bar / stats).
    /// Those scenes bake TextureRects that reference character_icon_*.png — IconTexture alone is unused there.
    /// </summary>
    public static bool ApplyCharacterIconScene(Node iconRoot, SkinProfile profile)
    {
        var icon = TextureCache.LoadAsset(profile, AssetKeys.CharacterIcon);
        var outline = TextureCache.LoadAsset(profile, AssetKeys.CharacterIconOutline);
        if (icon == null && outline == null)
            return false;

        var rects = new List<TextureRect>();
        CollectTextureRects(iconRoot, rects);
        if (rects.Count == 0)
        {
            Log.Warn($"[{profile.CharacterId}] Icon control has no TextureRect children to retexture");
            return false;
        }

        var replaced = 0;
        foreach (var tr in rects)
        {
            var path = tr.Texture?.ResourcePath ?? "";
            var name = tr.Name.ToString();
            var isOutline =
                path.Contains("outline", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("outline", StringComparison.OrdinalIgnoreCase);

            if (isOutline)
            {
                if (outline == null)
                    continue;
                tr.Texture = outline;
                replaced++;
                continue;
            }

            if (icon == null)
                continue;

            // Match baked top-panel character icons, or small icon scenes with only 1–2 rects.
            if (path.Contains("character_icon", StringComparison.OrdinalIgnoreCase) ||
                path.Contains("top_panel", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrEmpty(path) ||
                rects.Count <= 2)
            {
                tr.Texture = icon;
                replaced++;
            }
        }

        if (replaced == 0 && icon != null)
        {
            rects[^1].Texture = icon;
            replaced++;
            if (outline != null && rects.Count >= 2)
            {
                rects[0].Texture = outline;
                replaced++;
            }
        }

        if (replaced > 0)
            Log.Info($"[{profile.CharacterId}] Applied character icon textures to Icon scene ({replaced} rects)");
        return replaced > 0;
    }

    private static void CollectTextureRects(Node node, List<TextureRect> into)
    {
        if (node is TextureRect tr)
            into.Add(tr);
        foreach (var child in node.GetChildren())
        {
            if (child is Node n)
                CollectTextureRects(n, into);
        }
    }

    private static Vector2 ToVec2(float[]? arr)
    {
        if (arr == null || arr.Length < 2)
            return Vector2.Zero;
        return new Vector2(arr[0], arr[1]);
    }
}
