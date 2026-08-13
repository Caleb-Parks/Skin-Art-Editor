using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;

namespace SkinArtEditor.Patches;

[HarmonyPatch(typeof(CharacterModel), "get_IconTexture")]
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

[HarmonyPatch(typeof(CharacterModel), "get_IconOutlineTexture")]
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

        var children = container.GetChildren();
        if (children.Count == 0)
            return;

        var bg = children[^1] as Node;
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
