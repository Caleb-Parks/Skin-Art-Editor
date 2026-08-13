using Godot;

namespace SkinArtEditor.Config;

/// <summary>Shared Godot FileDialog helper for PNG browsing.</summary>
public static class FileBrowseHelper
{
    public static void BrowsePng(Node host, string title, Action<string> onSelected)
    {
        var dialog = new FileDialog
        {
            FileMode = FileDialog.FileModeEnum.OpenFile,
            Access = FileDialog.AccessEnum.Filesystem,
            Title = title,
            Filters = ["*.png ; PNG Images"]
        };

        dialog.FileSelected += path =>
        {
            onSelected(path);
            dialog.QueueFree();
        };
        dialog.Canceled += () => dialog.QueueFree();

        host.GetTree().Root.AddChild(dialog);
        dialog.PopupCentered(new Vector2I(900, 600));
    }
}
