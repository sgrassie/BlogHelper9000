using BlogHelper9000.Core.Helpers;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace BlogHelper9000.Mcp.Tools;

[McpServerToolType]
public static class GetTagsTool
{
    // Derived from BlogHelper9000.Core.Services.BlogService.FixTagsOnFile — keep in sync if that
    // method's behaviour changes.
    private static readonly IReadOnlyList<string> NormalisationRules =
    [
        "A legacy 'category' front-matter key replaces the tag list: a single value becomes one tag wrapped in single quotes (e.g. 'csharp'); a comma-separated value is split into several single-quoted tags.",
        "A legacy 'categories' front-matter key (checked after 'category', so it wins if both are present) replaces the tag list with its comma-separated, bracket-stripped values, each wrapped in single quotes.",
        "Tags are de-duplicated only on exact, case-sensitive equality before title-casing — tags that differ only by case (e.g. 'csharp' and 'Csharp') survive as separate entries.",
        "Every surviving tag is passed through TextInfo.ToTitleCase, capitalising each word; words that are entirely upper-case are treated as acronyms and left unchanged, and any quotes carried over from category migration remain part of the tag text.",
    ];

    [McpServerTool(Name = "get_tags", Title = "Get tag taxonomy", UseStructuredContent = true, ReadOnly = true, Idempotent = true),
     Description("Lists every tag in use across drafts and published posts, with counts and the normalisation rules fix_metadata applies to tags.")]
    public static ToolResponse<GetTagsResult> GetTags(PostManager postManager)
    {
        var aggregates = new Dictionary<string, TagAggregate>(StringComparer.OrdinalIgnoreCase);

        foreach (var header in postManager.LoadYamlHeaderForAllPosts())
        {
            var isPublished = header.IsPublished == true;

            // Dedupe case-insensitively within this header first, so a post carrying the
            // same tag twice (even under different casing/quoting) counts once.
            var casingsInHeader = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var rawTag in header.Tags ?? [])
            {
                var trimmed = rawTag.Trim().Trim('\'', '"');
                if (string.IsNullOrWhiteSpace(trimmed)) continue;
                if (!casingsInHeader.ContainsKey(trimmed))
                {
                    casingsInHeader[trimmed] = trimmed;
                }
            }

            foreach (var casing in casingsInHeader.Values)
            {
                if (!aggregates.TryGetValue(casing, out var aggregate))
                {
                    aggregate = new TagAggregate();
                    aggregates[casing] = aggregate;
                }

                aggregate.Total++;
                if (isPublished) aggregate.Published++;
                aggregate.CasingCounts[casing] = aggregate.CasingCounts.GetValueOrDefault(casing) + 1;
            }
        }

        var tags = aggregates.Values
            .Select(aggregate =>
            {
                var canonical = aggregate.CasingCounts
                    .OrderByDescending(kv => kv.Value)
                    .ThenBy(kv => kv.Key, StringComparer.Ordinal)
                    .First().Key;

                var variants = aggregate.CasingCounts.Keys
                    .Where(casing => !string.Equals(casing, canonical, StringComparison.Ordinal))
                    .OrderBy(casing => casing, StringComparer.Ordinal)
                    .ToList();

                return new TagCountDto(canonical, aggregate.Total, aggregate.Published,
                    aggregate.Total - aggregate.Published, variants);
            })
            .OrderByDescending(tag => tag.Total)
            .ThenBy(tag => tag.Tag, StringComparer.Ordinal)
            .ToList();

        return ToolResponse<GetTagsResult>.Ok(new GetTagsResult(tags, NormalisationRules));
    }

    private sealed class TagAggregate
    {
        public int Total;
        public int Published;
        public Dictionary<string, int> CasingCounts { get; } = new(StringComparer.Ordinal);
    }
}
