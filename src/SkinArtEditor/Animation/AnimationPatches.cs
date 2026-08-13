using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Combat;
using SkinArtEditor.Animation;

namespace SkinArtEditor.Patches;

[HarmonyPatch(typeof(NCreature), nameof(NCreature.SetAnimationTrigger))]
internal static class SetAnimationTriggerPatch
{
    private static bool Prefix(NCreature __instance, string trigger) =>
        !PngAnimationBridge.TryPlayTrigger(__instance, trigger);
}

[HarmonyPatch(typeof(NCreature), nameof(NCreature.StartDeathAnim))]
internal static class StartDeathAnimPatch
{
    private static void Postfix(NCreature __instance) =>
        PngAnimationBridge.TryPlayTrigger(__instance, "Dead");
}

[HarmonyPatch(typeof(NCreature), nameof(NCreature.StartReviveAnim))]
internal static class StartReviveAnimPatch
{
    private static void Postfix(NCreature __instance) =>
        PngAnimationBridge.TryPlayTrigger(__instance, "Idle");
}
