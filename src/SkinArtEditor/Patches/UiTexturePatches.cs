using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.Bestiary;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.StatsScreen;
using MegaCrit.sts2.Core.Nodes.TopBar;

namespace SkinArtEditor.Patches;

/// <summary>
/// Top bar + stats use <see cref="CharacterModel.Icon"/> (scene).
/// Bestiary / some menus use <see cref="CharacterModel.IconTexture"/>.
/// Card library pool filters bake icon textures into the scene (no runtime Icon/IconTexture call).
/// </summary>
[HarmonyPatch(typeof(CharacterModel), "get_Icon")]
internal static class CharacterIconScenePatch
{
    private static void Postfix(CharacterModel __instance, ref Control __result)
    {
        if (__result == null || !GodotObject.IsInstanceValid(__result))
            return;
        if (!SkinRegistry.TryGet(__instance, out var profile))
            return;
        if (!profile.HasUi(AssetKeys.CharacterIcon) && !profile.HasUi(AssetKeys.CharacterIconOutline))
            return;

        SkinApplier.ApplyCharacterIconScene(__result, profile);
    }
}

[HarmonyPatch(typeof(CharacterModel), nameof(CharacterModel.IconTexture), MethodType.Getter)]
internal static class IconTexturePatch
{
    private static bool Prefix(CharacterModel __instance, ref Texture2D __result)
    {
        if (!TryLoad(__instance, AssetKeys.CharacterIcon, out var tex))
            return true;
        __result = tex;
        return false;
    }

    internal static bool TryLoad(CharacterModel model, string key, out ImageTexture tex)
    {
        tex = null!;
        if (!SkinRegistry.TryGet(model, out var profile) || !profile.HasUi(key))
            return false;
        var loaded = TextureCache.LoadAsset(profile, key);
        if (loaded == null)
            return false;
        tex = loaded;
        return true;
    }
}

[HarmonyPatch(typeof(CharacterModel), nameof(CharacterModel.IconOutlineTexture), MethodType.Getter)]
internal static class IconOutlineTexturePatch
{
    private static bool Prefix(CharacterModel __instance, ref Texture2D __result)
    {
        if (!IconTexturePatch.TryLoad(__instance, AssetKeys.CharacterIconOutline, out var tex))
            return true;
        __result = tex;
        return false;
    }
}

/// <summary>
/// Catch shared texture loads (IconTexture path + any caller of AssetCache.GetTexture2D).
/// Does not cover textures already baked into instantiated PackedScenes — those need node Postfixes.
/// </summary>
[HarmonyPatch(typeof(AssetCache), nameof(AssetCache.GetTexture2D))]
internal static class AssetCacheGetTexture2DPatch
{
    private static bool Prefix(string path, ref Texture2D __result)
    {
        if (!SkinApplier.TryOverrideTopPanelIconPath(path, out var tex))
            return true;
        __result = tex;
        return false;
    }
}

[HarmonyPatch(typeof(NTopBarPortrait), nameof(NTopBarPortrait.Initialize))]
internal static class TopBarPortraitInitPatch
{
    private static void Postfix(NTopBarPortrait __instance, Player player)
    {
        if (player?.Character == null)
            return;
        if (!SkinRegistry.TryGet(player.Character, out var profile))
            return;
        if (!profile.HasUi(AssetKeys.CharacterIcon) && !profile.HasUi(AssetKeys.CharacterIconOutline))
            return;

        foreach (var child in __instance.GetChildren())
        {
            if (child is Node node)
                SkinApplier.ApplyCharacterIconScene(node, profile);
        }
    }
}

[HarmonyPatch(typeof(NCharacterStats), "_Ready")]
internal static class CharacterStatsReadyPatch
{
    private static void Postfix(NCharacterStats __instance)
    {
        try
        {
            var statsField = AccessTools.Field(typeof(NCharacterStats), "_characterStats");
            var stats = statsField?.GetValue(__instance);
            var id = stats?.GetType().GetProperty("Id")?.GetValue(stats);
            var entry = id?.GetType().GetProperty("Entry")?.GetValue(id)?.ToString()
                        ?? id?.ToString();
            if (string.IsNullOrEmpty(entry))
                return;

            var slug = entry!.Contains('.') ? entry.Split('.')[^1] : entry;
            if (!SkinRegistry.TryGet(slug, out var profile))
                return;
            if (!profile.HasUi(AssetKeys.CharacterIcon) && !profile.HasUi(AssetKeys.CharacterIconOutline))
                return;

            var host = __instance.GetNodeOrNull("%CharacterIcon");
            if (host == null)
                return;

            foreach (var child in host.GetChildren())
            {
                if (child is Node node)
                    SkinApplier.ApplyCharacterIconScene(node, profile);
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Stats character icon apply failed: {ex.Message}");
        }
    }
}

[HarmonyPatch(typeof(NCardLibrary), "_Ready")]
internal static class CardLibraryReadyPatch
{
    private static readonly (string NodePath, string Slug)[] CharacterPools =
    [
        ("%IroncladPool", "ironclad"),
        ("%SilentPool", "silent"),
        ("%DefectPool", "defect"),
        ("%RegentPool", "regent"),
        ("%NecrobinderPool", "necrobinder")
    ];

    private static void Postfix(NCardLibrary __instance)
    {
        foreach (var (nodePath, slug) in CharacterPools)
        {
            if (!SkinRegistry.TryGet(slug, out var profile))
                continue;
            if (!profile.HasUi(AssetKeys.CharacterIcon) && !profile.HasUi(AssetKeys.CharacterIconOutline))
                continue;

            var filter = __instance.GetNodeOrNull(nodePath);
            if (filter == null)
                continue;

            // Image child is a Control with baked character icon TextureRects.
            var image = filter.GetNodeOrNull("Image") ?? filter.GetNodeOrNull("%Image");
            if (image != null)
                SkinApplier.ApplyCharacterIconToNode(image, profile);
            else
                SkinApplier.ApplyCharacterIconScene(filter, profile);
        }
    }
}

[HarmonyPatch(typeof(NBestiaryCharacterFilter), "_Ready")]
internal static class BestiaryCharacterFilterReadyPatch
{
    private static void Postfix(NBestiaryCharacterFilter __instance)
    {
        var characterField = AccessTools.Field(typeof(NBestiaryCharacterFilter), "character");
        if (characterField?.GetValue(__instance) is not CharacterModel character)
            return;
        if (!SkinRegistry.TryGet(character, out var profile) || !profile.HasUi(AssetKeys.CharacterIcon))
            return;

        var tex = TextureCache.LoadAsset(profile, AssetKeys.CharacterIcon);
        if (tex == null)
            return;

        if (__instance.GetNodeOrNull("%Image") is TextureRect image)
            image.Texture = tex;
        if (__instance.GetNodeOrNull("%Shadow") is TextureRect shadow)
            shadow.Texture = tex;
    }
}

[HarmonyPatch(typeof(NBestiary), "UpdateDialogueBubbleStyle")]
internal static class BestiaryDialogueIconPatch
{
    private static void Postfix(NBestiary __instance)
    {
        try
        {
            var filterField = AccessTools.Field(typeof(NBestiary), "_currentFilter");
            var filter = filterField?.GetValue(__instance);
            var character = filter?.GetType().GetField("character")?.GetValue(filter) as CharacterModel
                            ?? filter?.GetType().GetProperty("character")?.GetValue(filter) as CharacterModel;
            if (character == null || !SkinRegistry.TryGet(character, out var profile))
                return;

            var icon = TextureCache.LoadAsset(profile, AssetKeys.CharacterIcon);
            var outline = TextureCache.LoadAsset(profile, AssetKeys.CharacterIconOutline);

            var iconField = AccessTools.Field(typeof(NBestiary), "_iconTexture");
            var outlineField = AccessTools.Field(typeof(NBestiary), "_iconOutlineTexture");
            if (icon != null && iconField?.GetValue(__instance) is TextureRect iconRect)
                iconRect.Texture = icon;
            if (outline != null && outlineField?.GetValue(__instance) is TextureRect outlineRect)
                outlineRect.Texture = outline;
        }
        catch (Exception ex)
        {
            Log.Warn($"Bestiary dialogue icon apply failed: {ex.Message}");
        }
    }
}

[HarmonyPatch(typeof(NCharacterSelectButton), nameof(NCharacterSelectButton.Init))]
internal static class CharacterSelectButtonInitPatch
{
    private static void Postfix(NCharacterSelectButton __instance, CharacterModel character)
    {
        if (!SkinRegistry.TryGet(character, out var profile))
            return;

        var iconField = AccessTools.Field(typeof(NCharacterSelectButton), "_icon");
        var lockedField = AccessTools.Field(typeof(NCharacterSelectButton), "_isLocked");
        if (iconField?.GetValue(__instance) is not TextureRect icon)
            return;

        var isLocked = lockedField != null && lockedField.GetValue(__instance) is true;
        var key = isLocked ? AssetKeys.CharSelectLocked : AssetKeys.CharSelect;
        if (!profile.HasUi(key))
            return;

        var tex = TextureCache.LoadAsset(profile, key);
        if (tex != null)
            icon.Texture = tex;
    }
}

[HarmonyPatch(typeof(NCharacterSelectScreen), nameof(NCharacterSelectScreen.SelectCharacter))]
internal static class CharacterSelectScreenBgPatch
{
    private static void Postfix(NCharacterSelectScreen __instance, CharacterModel characterModel)
    {
        if (!SkinRegistry.TryGet(characterModel, out var profile) || !profile.HasCharSelectBg)
            return;

        var containerField = AccessTools.Field(typeof(NCharacterSelectScreen), "_bgContainer");
        if (containerField?.GetValue(__instance) is not Node container)
            return;

        ApplyToContainer(container, profile);

        // Retry next idle frame in case the BG scene finishes instantiating after SelectCharacter returns.
        Callable.From(() => ApplyToContainer(container, profile)).CallDeferred();
    }

    private static void ApplyToContainer(Node container, SkinProfile profile)
    {
        if (!GodotObject.IsInstanceValid(container))
            return;

        var children = container.GetChildren();
        if (children.Count == 0)
            return;

        // Prefer our PNG template if present; otherwise last child (vanilla swap target).
        Node? bg = null;
        foreach (var child in children)
        {
            if (child is Node n && (n.Name.ToString().Contains("PngCharSelectBg", StringComparison.OrdinalIgnoreCase)
                                    || n.HasMethod("apply_skin")))
            {
                bg = n;
                break;
            }
        }

        bg ??= children[^1] as Node;
        if (bg != null)
            SkinApplier.ApplyCharSelectBg(bg, profile);
    }
}

[HarmonyPatch(typeof(NMapMarker), nameof(NMapMarker.Initialize))]
internal static class MapMarkerInitPatch
{
    private static void Postfix(NMapMarker __instance, Player player)
    {
        if (player?.Character == null)
            return;
        if (!SkinRegistry.TryGet(player.Character, out var profile) || !profile.HasUi(AssetKeys.MapMarker))
            return;

        var tex = TextureCache.LoadAsset(profile, AssetKeys.MapMarker);
        if (tex != null)
            __instance.Texture = tex;
    }
}
