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
    private CheckButton _knockout = null!;
    private LineEdit _knockoutThreshold = null!;
    private readonly Dictionary<string, LineEdit> _assetFields = new();
    private readonly Dictionary<string, LineEdit> _offsetFields = new();
    private readonly List<CharacterCatalog.Entry> _catalog = [];
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
        RefreshCharacterDropdown();
        _character.ItemSelected += idx =>
        {
            if (idx < 0 || idx >= _catalog.Count)
                return;
            _characterId = _catalog[(int)idx].Slug;
            // Avoid LoadIntoUi → Refresh/Select re-entrancy snapping to Defect (index 0).
            LoadProfileFieldsOnly();
        };
        charRow.AddChild(_character);
        root.AddChild(charRow);

        _enabled = new CheckButton { Text = "Enabled" };
        root.AddChild(_enabled);

        _knockout = new CheckButton
        {
            Text = "Knock out backdrops (poses + icon/map marker)"
        };
        root.AddChild(_knockout);

        var threshRow = new HBoxContainer();
        threshRow.AddChild(new Label { Text = "Knockout threshold", CustomMinimumSize = new Vector2(160, 0) });
        _knockoutThreshold = new LineEdit { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        threshRow.AddChild(_knockoutThreshold);
        root.AddChild(threshRow);
        root.AddChild(new Label
        {
            Text = "Knockout runs on Browse/Save for combat/shop/rest, character_icon, and map_marker. Locked portrait and icon outline are auto-derived. Char-select art is never knocked out.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        });

        var scroll = new ScrollContainer
        {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 380)
        };
        root.AddChild(scroll);
        var list = new VBoxContainer();
        scroll.AddChild(list);

        list.AddChild(new Label { Text = "Assets" });
        foreach (var key in AssetKeys.UserSelectable)
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
                if (string.Equals(captured, AssetKeys.CharSelectBg, StringComparison.OrdinalIgnoreCase))
                {
                    _offsetFields["char_select_bg_zoom"].Text =
                        CharSelectBgFramer.DefaultZoom.ToString(CultureInfo.InvariantCulture);
                    _offsetFields["char_select_bg_offset"].Text = SkinConfigService.FormatVec(
                    [
                        CharSelectBgFramer.DefaultOffsetX,
                        CharSelectBgFramer.DefaultOffsetY
                    ]);
                }
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
        AddOffsetField(list, "char_select_bg_zoom", "Char Select BG Zoom");
        AddOffsetField(list, "char_select_bg_offset", "Char Select BG Offset X,Y");

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

    private void RefreshCharacterDropdown()
    {
        _character.SetBlockSignals(true);
        _catalog.Clear();
        _catalog.AddRange(CharacterCatalog.List());
        _character.Clear();
        foreach (var entry in _catalog)
            _character.AddItem(entry.DisplayName);
        _character.SetBlockSignals(false);
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
        RefreshCharacterDropdown();
        var idx = _catalog.FindIndex(e => e.Slug.Equals(_characterId, StringComparison.OrdinalIgnoreCase));
        if (idx < 0)
        {
            idx = 0;
            _characterId = _catalog[0].Slug;
        }
        // Block signals so Select does not re-enter ItemSelected and overwrite _characterId.
        _character.SetBlockSignals(true);
        _character.Select(idx);
        _character.SetBlockSignals(false);

        LoadProfileFieldsOnly();
    }

    private void LoadProfileFieldsOnly()
    {
        var dto = SkinProfileLoader.LoadDtoOrDefault(_characterId);
        _enabled.ButtonPressed = dto.Enabled;
        _knockout.ButtonPressed = dto.KnockoutBackdrop;
        _knockoutThreshold.Text = dto.KnockoutThreshold.ToString(CultureInfo.InvariantCulture);
        foreach (var key in AssetKeys.UserSelectable)
        {
            dto.Assets.TryGetValue(key, out var val);
            _assetFields[key].Text = val ?? "";
        }

        foreach (var (key, value) in SkinConfigService.ReadOffsetFields(dto.Offsets))
        {
            if (_offsetFields.TryGetValue(key, out var field))
                field.Text = value;
        }

        _status.Text = $"Editing {_characterId} — restart after save to apply.";
    }

    private void SaveFromUi()
    {
        try
        {
            var dto = SkinProfileLoader.LoadDtoOrDefault(_characterId);
            dto.Enabled = _enabled.ButtonPressed;
            dto.KnockoutBackdrop = _knockout.ButtonPressed;
            dto.KnockoutThreshold = SkinConfigService.ParseThreshold(
                _knockoutThreshold.Text, BackdropKnockout.DefaultThreshold);

            var previousAssets = new Dictionary<string, string?>(dto.Assets, StringComparer.OrdinalIgnoreCase);
            dto.Assets.Clear();

            foreach (var key in AssetKeys.UserSelectable)
            {
                var text = _assetFields[key].Text.Trim();
                if (string.IsNullOrEmpty(text))
                {
                    if (previousAssets.TryGetValue(key, out var old) && !string.IsNullOrWhiteSpace(old))
                    {
                        dto.Assets[key] = old;
                        AssetCopier.ClearAsset(_characterId, key, dto);
                    }
                    continue;
                }

                SkinConfigService.SetUserAsset(_characterId, dto, key, text);
            }

            var offsetFields = _offsetFields.ToDictionary(kv => kv.Key, kv => kv.Value.Text);
            SkinConfigService.ApplyOffsetFields(dto.Offsets, offsetFields);

            SkinConfigService.SaveAndReload(dto);
            _status.Text = "Saved. Restart the game to apply scene/texture overrides.";
            Log.Info(_status.Text);
        }
        catch (Exception ex)
        {
            _status.Text = "Save failed: " + ex.Message;
            Log.Warn(_status.Text);
        }
    }
}
