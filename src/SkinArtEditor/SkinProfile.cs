using System.Text.Json.Serialization;

namespace SkinArtEditor;

public static class AssetKeys
{
    public const string IdleLoop = "idle_loop";
    public const string Attack = "attack";
    public const string Cast = "cast";
    public const string Hurt = "hurt";
    public const string Die = "die";
    public const string RelaxedLoop = "relaxed_loop";
    public const string RestLoop = "rest_loop";
    public const string CharSelect = "char_select";
    public const string CharSelectLocked = "char_select_locked";
    public const string CharSelectBg = "char_select_bg";
    public const string CharacterIcon = "character_icon";
    public const string CharacterIconOutline = "character_icon_outline";
    public const string MapMarker = "map_marker";

    public static readonly string[] CombatRequired =
    [
        IdleLoop, Attack, Cast, Hurt, Die
    ];

    public static readonly string[] All =
    [
        IdleLoop, Attack, Cast, Hurt, Die,
        RelaxedLoop, RestLoop,
        CharSelect, CharSelectLocked, CharSelectBg,
        CharacterIcon, CharacterIconOutline, MapMarker
    ];
}

public sealed class SkinProfileDto
{
    [JsonPropertyName("characterId")]
    public string CharacterId { get; set; } = "";

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    /// <summary>Clear edge-connected near-black backdrop on combat/shop/rest poses when copying.</summary>
    [JsonPropertyName("knockoutBackdrop")]
    public bool KnockoutBackdrop { get; set; } = true;

    /// <summary>Max r+g+b (0–255 scale) treated as backdrop black. Cassiopeia default: 18.</summary>
    [JsonPropertyName("knockoutThreshold")]
    public int KnockoutThreshold { get; set; } = BackdropKnockout.DefaultThreshold;

    [JsonPropertyName("assets")]
    public Dictionary<string, string?> Assets { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonPropertyName("offsets")]
    public SkinOffsetsDto Offsets { get; set; } = new();
}

public sealed class SkinOffsetsDto
{
    [JsonPropertyName("combatVisualsPosition")]
    public float[] CombatVisualsPosition { get; set; } = [0f, 3.91293f];

    [JsonPropertyName("combatVisualsScale")]
    public float CombatVisualsScale { get; set; } = 0.31011f;

    [JsonPropertyName("combatBottomPaddingPx")]
    public float CombatBottomPaddingPx { get; set; } = 32f;

    [JsonPropertyName("formVfxPosition")]
    public float[] FormVfxPosition { get; set; } = [-17.5f, 0f];

    [JsonPropertyName("shopSpriteOffset")]
    public float[] ShopSpriteOffset { get; set; } = [0f, -428.4f];

    [JsonPropertyName("shopSpriteScale")]
    public float ShopSpriteScale { get; set; } = 0.456134f;

    [JsonPropertyName("restDisplayOffset")]
    public float[] RestDisplayOffset { get; set; } = [-173.983f, 150.047f];

    [JsonPropertyName("restSpriteScale")]
    public float RestSpriteScale { get; set; } = 0.792f;

    [JsonPropertyName("restSeatAnchor")]
    public float[] RestSeatAnchor { get; set; } = [0.5f, 0.6f];

    [JsonPropertyName("restVisibleBounds")]
    public float[] RestVisibleBounds { get; set; } = [153f, 125f, 767f, 1077f];
}

/// <summary>Resolved runtime profile: only present/readable assets are kept.</summary>
public sealed class SkinProfile
{
    public required string CharacterId { get; init; }
    public required string Directory { get; init; }
    public bool Enabled { get; init; }
    public SkinOffsetsDto Offsets { get; init; } = new();

    /// <summary>Asset key → absolute file path for present files only.</summary>
    public Dictionary<string, string> ResolvedPaths { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public bool HasFullCombat =>
        AssetKeys.CombatRequired.All(k => ResolvedPaths.ContainsKey(k));

    public bool HasShop => ResolvedPaths.ContainsKey(AssetKeys.RelaxedLoop);
    public bool HasRest => ResolvedPaths.ContainsKey(AssetKeys.RestLoop);
    public bool HasCharSelectBg => ResolvedPaths.ContainsKey(AssetKeys.CharSelectBg);

    public bool HasUi(string key) => ResolvedPaths.ContainsKey(key);

    public bool TryGetPath(string key, out string path) => ResolvedPaths.TryGetValue(key, out path!);
}
