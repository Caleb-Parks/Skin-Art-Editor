namespace SkinArtEditor;

/// <summary>
/// Discovers configurable characters from <c>characters/*/</c> folders.
/// </summary>
public static class CharacterCatalog
{
    private static readonly Dictionary<string, string> DisplayOverrides = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ironclad"] = "Ironclad",
        ["silent"] = "Silent",
        ["defect"] = "Defect",
        ["regent"] = "Regent",
        ["necrobinder"] = "Necrobinder"
    };

    public readonly record struct Entry(string Slug, string DisplayName);

    public static IReadOnlyList<Entry> List()
    {
        var results = new List<Entry>();
        var root = ModPaths.CharactersRoot;
        if (Directory.Exists(root))
        {
            foreach (var dir in Directory.GetDirectories(root))
            {
                var slug = Path.GetFileName(dir).ToLowerInvariant();
                if (!IsValidSlug(slug))
                {
                    Log.Warn($"Skipping invalid character folder name '{slug}'");
                    continue;
                }

                results.Add(new Entry(slug, DisplayNameFor(slug)));
            }
        }

        if (results.Count == 0)
            results.Add(new Entry("regent", "Regent"));

        return results
            .OrderBy(e => e.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static string[] DisplayNames() =>
        List().Select(e => e.DisplayName).ToArray();

    public static string DisplayNameFor(string slug)
    {
        var key = slug.ToLowerInvariant();
        if (DisplayOverrides.TryGetValue(key, out var name))
            return name;
        if (string.IsNullOrEmpty(key))
            return key;
        return char.ToUpperInvariant(key[0]) + key[1..];
    }

    public static string SlugFromDisplay(string? displayOrSlug)
    {
        if (string.IsNullOrWhiteSpace(displayOrSlug))
            return List()[0].Slug;

        foreach (var entry in List())
        {
            if (entry.DisplayName.Equals(displayOrSlug, StringComparison.OrdinalIgnoreCase) ||
                entry.Slug.Equals(displayOrSlug, StringComparison.OrdinalIgnoreCase))
                return entry.Slug;
        }

        return displayOrSlug.Trim().ToLowerInvariant();
    }

    public static bool IsValidSlug(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
            return false;
        foreach (var c in slug)
        {
            if (char.IsAsciiLetterOrDigit(c) || c is '_' or '-')
                continue;
            return false;
        }
        return true;
    }
}
