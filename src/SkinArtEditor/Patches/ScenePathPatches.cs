using System.Collections;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.RestSite;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;

namespace SkinArtEditor.Patches;

[HarmonyPatch(typeof(CharacterModel), "get_MerchantAnimPath")]
internal static class MerchantAnimPathPatch
{
    private static void Postfix(CharacterModel __instance, ref string __result)
    {
        if (SkinRegistry.TryGet(__instance, out var profile) && profile.HasShop)
            __result = ScenePaths.Merchant;
    }
}

[HarmonyPatch(typeof(CharacterModel), "get_RestSiteAnimPath")]
internal static class RestSiteAnimPathPatch
{
    private static void Postfix(CharacterModel __instance, ref string __result)
    {
        if (SkinRegistry.TryGet(__instance, out var profile) && profile.HasRest)
            __result = ScenePaths.RestSite;
    }
}

[HarmonyPatch(typeof(CharacterModel), "get_CharacterSelectBg")]
internal static class CharacterSelectBgPathPatch
{
    private static void Postfix(CharacterModel __instance, ref string __result)
    {
        if (SkinRegistry.TryGet(__instance, out var profile) && profile.HasCharSelectBg)
            __result = ScenePaths.CharSelectBg;
    }
}

[HarmonyPatch(typeof(NRestSiteCharacter), nameof(NRestSiteCharacter.Create))]
internal static class RestSiteCreatePatch
{
    private static void Postfix(Player player, NRestSiteCharacter __result)
    {
        if (__result == null || player?.Character == null)
            return;
        if (!SkinRegistry.TryGet(player.Character, out var profile) || !profile.HasRest)
            return;
        SkinApplier.ApplyRest(__result, profile);
    }
}

[HarmonyPatch(typeof(NMerchantRoom), "AfterRoomIsLoaded")]
internal static class MerchantRoomLoadedPatch
{
    private static void Postfix(NMerchantRoom __instance)
    {
        try
        {
            var playersField = AccessTools.Field(typeof(NMerchantRoom), "_players");
            var visualsField = AccessTools.Field(typeof(NMerchantRoom), "_playerVisuals");
            if (playersField == null || visualsField == null)
                return;

            var players = playersField.GetValue(__instance) as IList;
            var visuals = visualsField.GetValue(__instance) as IList;
            if (players == null || visuals == null)
                return;

            var count = Math.Min(players.Count, visuals.Count);
            for (var i = 0; i < count; i++)
            {
                if (players[i] is not Player player || visuals[i] is not Node merchant)
                    continue;
                if (!SkinRegistry.TryGet(player.Character, out var profile) || !profile.HasShop)
                    continue;
                SkinApplier.ApplyMerchant(merchant, profile);
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Merchant apply failed: {ex.Message}");
        }
    }
}
