using System.Globalization;
using Godot;

namespace SkinArtEditor.Config;

/// <summary>
/// Fallback in-game settings panel when ModConfig is not installed.
/// Open with F8 (or the on-screen toggle button injected at boot).
/// </summary>
public partial class NativeSettingsUi : CanvasLayer
{
    private OptionButton _character = null!;
    private CheckButton _enabled = null!;
    private readonly Dictionary<string, LineEdit> _assetFields = new();
    private readonly Dictionary<string, LineEdit> _offsetFields = new();
    private Label _status = null!;
    private Control _panel = null!;
    private string _characterId = "regent";

    public static NativeSettingsUi? Instance { get; private set; }

    public override void _Ready()
    {
        Instance = this;
        Layer = 100;
        BuildUi();
        Visible = false;
        LoadIntoUi();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Keycode: Key.F8 })
        {
            Visible = !Visible;
            if (Visible)
                LoadIntoUi();
            GetViewport().SetInputAsHandled();
        }
    }

    public void Toggle()
    {
        Visible = !Visible;
        if (Visible)
            LoadIntoUi();
    }

    private void BuildUi()
    {
        var dim = new ColorRect
        {
            Color = new Color(0, 0, 0, 0.55f),
            AnchorRight = 1,
            AnchorBottom = 1
        };
        dim.GuiInput += _ => { /* block clicks through */ };
        AddChild(dim);

        _panel = new PanelContainer
        {
            AnchorLeft = 0.5f,
            AnchorTop = 0.5f,
            AnchorRight = 0.5f,
            AnchorBottom = 0.5f,
            OffsetLeft = -420,
            OffsetTop = -320,
            OffsetRight = 420,
            OffsetBottom = 320
        };
        AddChild(_panel);

        var margin = new MarginContainer();
        foreach (var s in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" })
            margin.AddThemeConstantOverride(s, 12);
        _panel.AddChild(margin);

        var root = new VBoxContainer();
        margin.AddChild(root);

        root.AddChild(new Label { Text = "Skin Art Editor", HorizontalAlignment = HorizontalAlignment.Center });
        root.AddChild(new Label
        {
            Text = "Missing assets keep vanilla. Combat needs all 5 poses. Restart after Save. (F8)",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        });

        var charRow = new HBoxContainer();
        charRow.AddChild(new Label { Text = "Character" });
        _character = new OptionButton();
        _character.AddItem("Regent");
        _character.ItemSelected += _ =>
        {
            _characterId = "regent";
            LoadIntoUi();
        };
        charRow.AddChild(_character);
        root.AddChild(charRow);

        _enabled = new CheckButton { Text = "Enabled" };
        root.AddChild(_enabled);

        var scroll = new ScrollContainer
        {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 380)
        };
        root.AddChild(scroll);
        var list = new VBoxContainer();
        scroll.AddChild(list);

        list.AddChild(new Label { Text = "Assets" });
        foreach (var key in AssetKeys.All)
        {
            var row = new HBoxContainer();
            row.AddChild(new Label { Text = key, CustomMinimumSize = new Vector2(160, 0) });
            var field = new LineEdit { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            _assetFields[key] = field;
            row.AddChild(field);

            var browse = new Button { Text = "Browse" };
            var captured = key;
            browse.Pressed += () => FileBrowseHelper.BrowsePng(this, $"Select {captured}.png", path =>
            {
                field.Text = path;
            });
            row.AddChild(browse);

            var clear = new Button { Text = "Clear" };
            clear.Pressed += () => { field.Text = ""; };
            row.AddChild(clear);
            list.AddChild(row);
        }

        list.AddChild(new Label { Text = "Offsets" });
        AddOffsetField(list, "combat_pos", "Combat Pos X,Y");
        AddOffsetField(list, "combat_scale", "Combat Scale");
        AddOffsetField(list, "combat_pad", "Combat Padding");
        AddOffsetField(list, "form_vfx", "FormVfx X,Y");
        AddOffsetField(list, "shop_offset", "Shop Offset X,Y");
        AddOffsetField(list, "shop_scale", "Shop Scale");
        AddOffsetField(list, "rest_offset", "Rest Offset X,Y");
        AddOffsetField(list, "rest_scale", "Rest Scale");
        AddOffsetField(list, "rest_anchor", "Rest Anchor X,Y");
        AddOffsetField(list, "rest_bounds", "Rest Bounds X,Y,W,H");

        var buttons = new HBoxContainer();
        var save = new Button { Text = "Save / Apply" };
        save.Pressed += SaveFromUi;
        buttons.AddChild(save);
        var close = new Button { Text = "Close" };
        close.Pressed += () => Visible = false;
        buttons.AddChild(close);
        root.AddChild(buttons);

        _status = new Label { Text = "" };
        root.AddChild(_status);
    }

    private void AddOffsetField(VBoxContainer parent, string key, string label)
    {
        var row = new HBoxContainer();
        row.AddChild(new Label { Text = label, CustomMinimumSize = new Vector2(160, 0) });
        var field = new LineEdit { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _offsetFields[key] = field;
        row.AddChild(field);
        parent.AddChild(row);
    }

    private void LoadIntoUi()
    {
        var dto = SkinProfileLoader.LoadDtoOrDefault(_characterId);
        _enabled.ButtonPressed = dto.Enabled;
        foreach (var key in AssetKeys.All)
        {
            dto.Assets.TryGetValue(key, out var val);
            _assetFields[key].Text = val ?? "";
        }

        var o = dto.Offsets;
        _offsetFields["combat_pos"].Text = Format(o.CombatVisualsPosition);
        _offsetFields["combat_scale"].Text = o.CombatVisualsScale.ToString(CultureInfo.InvariantCulture);
        _offsetFields["combat_pad"].Text = o.CombatBottomPaddingPx.ToString(CultureInfo.InvariantCulture);
        _offsetFields["form_vfx"].Text = Format(o.FormVfxPosition);
        _offsetFields["shop_offset"].Text = Format(o.ShopSpriteOffset);
        _offsetFields["shop_scale"].Text = o.ShopSpriteScale.ToString(CultureInfo.InvariantCulture);
        _offsetFields["rest_offset"].Text = Format(o.RestDisplayOffset);
        _offsetFields["rest_scale"].Text = o.RestSpriteScale.ToString(CultureInfo.InvariantCulture);
        _offsetFields["rest_anchor"].Text = Format(o.RestSeatAnchor);
        _offsetFields["rest_bounds"].Text = Format(o.RestVisibleBounds);
        _status.Text = $"Editing {_characterId} — restart after save to apply.";
    }

    private void SaveFromUi()
    {
        try
        {
            var dto = SkinProfileLoader.LoadDtoOrDefault(_characterId);
            dto.Enabled = _enabled.ButtonPressed;
            dto.Assets.Clear();

            foreach (var key in AssetKeys.All)
            {
                var text = _assetFields[key].Text.Trim();
                if (string.IsNullOrEmpty(text))
                    continue;

                if (Path.IsPathRooted(text) && File.Exists(text))
                    dto.Assets[key] = AssetCopier.CopyAsset(_characterId, key, text);
                else
                    dto.Assets[key] = text;
            }

            var o = dto.Offsets;
            o.CombatVisualsPosition = ParseVec(_offsetFields["combat_pos"].Text, o.CombatVisualsPosition);
            o.CombatVisualsScale = ParseFloat(_offsetFields["combat_scale"].Text, o.CombatVisualsScale);
            o.CombatBottomPaddingPx = ParseFloat(_offsetFields["combat_pad"].Text, o.CombatBottomPaddingPx);
            o.FormVfxPosition = ParseVec(_offsetFields["form_vfx"].Text, o.FormVfxPosition);
            o.ShopSpriteOffset = ParseVec(_offsetFields["shop_offset"].Text, o.ShopSpriteOffset);
            o.ShopSpriteScale = ParseFloat(_offsetFields["shop_scale"].Text, o.ShopSpriteScale);
            o.RestDisplayOffset = ParseVec(_offsetFields["rest_offset"].Text, o.RestDisplayOffset);
            o.RestSpriteScale = ParseFloat(_offsetFields["rest_scale"].Text, o.RestSpriteScale);
            o.RestSeatAnchor = ParseVec(_offsetFields["rest_anchor"].Text, o.RestSeatAnchor);
            o.RestVisibleBounds = ParseBounds(_offsetFields["rest_bounds"].Text, o.RestVisibleBounds);

            SkinProfileLoader.Save(dto);
            SkinRegistry.ReloadAll();
            _status.Text = "Saved. Restart the game to apply scene/texture overrides.";
            Log.Info(_status.Text);
        }
        catch (Exception ex)
        {
            _status.Text = "Save failed: " + ex.Message;
            Log.Warn(_status.Text);
        }
    }

    private static string Format(float[] v) =>
        string.Join(", ", v.Select(f => f.ToString(CultureInfo.InvariantCulture)));

    private static float[] ParseVec(string s, float[] fallback)
    {
        var parts = s.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) return fallback;
        if (!float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x)) return fallback;
        if (!float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y)) return fallback;
        return [x, y];
    }

    private static float[] ParseBounds(string s, float[] fallback)
    {
        var parts = s.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 4) return fallback;
        var arr = new float[4];
        for (var i = 0; i < 4; i++)
            if (!float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out arr[i]))
                return fallback;
        return arr;
    }

    private static float ParseFloat(string s, float fallback) =>
        float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : fallback;
}
