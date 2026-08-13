using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;
using SkinArtEditor.Config;

namespace SkinArtEditor;

[ModInitializer(nameof(Initialize))]
public static class SkinArtEditorMod
{
    public const string HarmonyId = "SkinArtEditor";

    private static bool _uiBootstrapped;

    public static void Initialize()
    {
        Log.Info($"Initializing (mod root: {ModPaths.ModRoot})");
        SkinRegistry.ReloadAll();

        var harmony = new Harmony(HarmonyId);
        harmony.PatchAll(typeof(SkinArtEditorMod).Assembly);

        // Defer UI host until the scene tree exists.
        Callable.From(BootstrapUi).CallDeferred();
    }

    private static void BootstrapUi()
    {
        if (_uiBootstrapped)
            return;

        var tree = Engine.GetMainLoop() as SceneTree;
        if (tree?.Root == null)
        {
            Callable.From(BootstrapUi).CallDeferred();
            return;
        }

        _uiBootstrapped = true;
        var host = new Node { Name = "SkinArtEditorHost" };
        tree.Root.CallDeferred(Node.MethodName.AddChild, host);

        // Wait one frame so host is in the tree.
        Callable.From(() =>
        {
            if (!ModConfigIntegration.TryRegister(host))
            {
                var ui = new NativeSettingsUi { Name = "SkinArtEditorSettings" };
                host.AddChild(ui);
                Log.Info("Fallback settings UI ready (press F8)");
            }
            else
            {
                // Still provide F8 panel as a convenience for file browsing feedback.
                var ui = new NativeSettingsUi { Name = "SkinArtEditorSettings" };
                host.AddChild(ui);
                Log.Info("ModConfig registered; F8 fallback UI also available");
            }
        }).CallDeferred();
    }
}
