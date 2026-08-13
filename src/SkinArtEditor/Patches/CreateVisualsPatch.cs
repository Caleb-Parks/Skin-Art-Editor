using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace SkinArtEditor.Patches;

[HarmonyPatch(typeof(CharacterModel), nameof(CharacterModel.CreateVisuals))]
internal static class CreateVisualsPatch
{
    private static bool Prefix(CharacterModel __instance, ref NCreatureVisuals __result)
    {
        if (!SkinRegistry.TryGet(__instance, out var profile) || !profile.HasFullCombat)
            return true;

        var packed = ResourceLoader.Load<PackedScene>(ScenePaths.Combat);
        if (packed == null)
        {
            Log.Warn($"Failed to load {ScenePaths.Combat}");
            return true;
        }

        var visuals = packed.Instantiate<NCreatureVisuals>(PackedScene.GenEditState.Disabled);
        SkinApplier.ApplyCombat(visuals, profile);
        __result = visuals;
        return false;
    }
}
