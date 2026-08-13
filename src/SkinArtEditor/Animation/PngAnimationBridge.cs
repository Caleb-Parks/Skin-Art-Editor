using Godot;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace SkinArtEditor.Animation;

internal static class PngAnimationBridge
{
    public static bool TryPlayTrigger(NCreature creature, string trigger)
    {
        if (creature == null || string.IsNullOrEmpty(trigger))
            return false;

        var visuals = creature.Visuals;
        if (visuals == null || !GodotObject.IsInstanceValid(visuals))
            return false;

        var body = ResolveAnimator(visuals);
        if (body == null)
            return false;

        body.Call("play_trigger", trigger);
        return true;
    }

    private static Node? ResolveAnimator(NCreatureVisuals visuals)
    {
        if (visuals.HasNode("%Visuals"))
        {
            var named = visuals.GetNode("%Visuals");
            if (IsAnimator(named))
                return named;
        }

        if (visuals.HasNode("Visuals"))
        {
            var child = visuals.GetNode("Visuals");
            if (IsAnimator(child))
                return child;
        }

        if (visuals.HasMethod("play_trigger"))
            return visuals;

        return null;
    }

    private static bool IsAnimator(Node? node) =>
        node != null && GodotObject.IsInstanceValid(node) && node.HasMethod("play_trigger");
}
