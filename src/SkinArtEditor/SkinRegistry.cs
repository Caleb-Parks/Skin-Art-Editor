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

    public static IEnumerable<string> KnownCharacterIds()
    {
        // v1: Regent only in UI; folders may still be scanned for load.
        yield return "regent";
    }
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
    }

    public static ImageTexture? Load(string absolutePath)
    {
        if (string.IsNullOrWhiteSpace(absolutePath) || !File.Exists(absolutePath))
            return null;

        if (Cache.TryGetValue(absolutePath, out var existing) && GodotObject.IsInstanceValid(existing))
            return existing;

        try
        {
            var image = Image.LoadFromFile(absolutePath);
            if (image == null)
            {
                Log.Warn($"Image.LoadFromFile returned null for {absolutePath}");
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
        var tex = TextureCache.LoadAsset(profile, AssetKeys.CharSelectBg);
        if (tex == null)
            return;

        if (bgRoot.HasMethod("apply_skin"))
            bgRoot.Call("apply_skin", tex);
        else
        {
            var bg = bgRoot.GetNodeOrNull("Bg") as TextureRect;
            if (bg != null)
                bg.Texture = tex;
        }
    }

    private static Vector2 ToVec2(float[]? arr)
    {
        if (arr == null || arr.Length < 2)
            return Vector2.Zero;
        return new Vector2(arr[0], arr[1]);
    }
}
