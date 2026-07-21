using System.Text.RegularExpressions;

namespace BlogHelper9000.Core.Helpers;

/// <summary>
/// Scans post/draft body text for crude "not finished yet" markers (TODO, FIXME, etc.).
/// Dependency-free so it can be reused by other features (e.g. a future validation tool).
/// </summary>
public static class ContentMarkers
{
    // Case-sensitive, word-boundary matches for the all-caps markers avoid false positives
    // like "toDo" in prose or "TODOS" inside a URL slug.
    private static readonly (string Marker, Regex Pattern)[] WordMarkers =
    [
        ("TODO", new Regex(@"\bTODO\b", RegexOptions.Compiled)),
        ("FIXME", new Regex(@"\bFIXME\b", RegexOptions.Compiled)),
        ("XXX", new Regex(@"\bXXX\b", RegexOptions.Compiled)),
        ("TBD", new Regex(@"\bTBD\b", RegexOptions.Compiled)),
    ];

    private const string PlaceholderMarker = "[placeholder";

    public static IReadOnlyList<string> FindMarkers(string content)
    {
        if (string.IsNullOrEmpty(content))
            return [];

        var found = new List<string>();

        foreach (var (marker, pattern) in WordMarkers)
        {
            if (pattern.IsMatch(content))
                found.Add(marker);
        }

        if (content.Contains(PlaceholderMarker, StringComparison.OrdinalIgnoreCase))
            found.Add(PlaceholderMarker);

        return found;
    }
}
