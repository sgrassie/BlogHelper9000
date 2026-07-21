using System.IO.Abstractions;
using BlogHelper9000.Core.Helpers;

namespace BlogHelper9000.Core.Services;

public class PostSearchService : IPostSearchService
{
    private const int MaxSnippetsPerFile = 3;
    private const int SnippetLength = 120;

    private readonly PostManager _postManager;
    private readonly IFileSystem _fileSystem;

    public PostSearchService(PostManager postManager)
    {
        _postManager = postManager;
        _fileSystem = postManager.FileSystem;
    }

    public PostSearchResults Search(string query, string? tag = null, bool includeDrafts = true, int limit = 20)
    {
        var trimmedQuery = query.Trim();
        var trimmedTag = tag?.Trim();

        if (trimmedQuery.Length == 0 && string.IsNullOrEmpty(trimmedTag))
            return new PostSearchResults([], 0);

        var matches = new List<PostSearchMatch>();

        foreach (var (path, isDraft) in EnumerateCandidates(includeDrafts))
        {
            var match = TryMatch(path, isDraft, trimmedQuery, trimmedTag);
            if (match is not null)
                matches.Add(match);
        }

        // Most recently published first; drafts have no PublishedOn and, under the default
        // nullable-DateTime comparer, null sorts lowest — so OrderByDescending naturally
        // pushes drafts to the end without special-casing them.
        var ordered = matches.OrderByDescending(m => m.PublishedOn).ToList();

        return new PostSearchResults(ordered.Take(limit).ToList(), ordered.Count);
    }

    private IEnumerable<(string Path, bool IsDraft)> EnumerateCandidates(bool includeDrafts)
    {
        if (includeDrafts && _fileSystem.Directory.Exists(_postManager.Drafts))
        {
            foreach (var path in _fileSystem.Directory.EnumerateFiles(_postManager.Drafts, "*.md", SearchOption.AllDirectories))
                yield return (path, true);
        }

        if (_fileSystem.Directory.Exists(_postManager.Posts))
        {
            foreach (var path in _fileSystem.Directory.EnumerateFiles(_postManager.Posts, "*.md", SearchOption.AllDirectories))
                yield return (path, false);
        }
    }

    private PostSearchMatch? TryMatch(string path, bool isDraft, string trimmedQuery, string? trimmedTag)
    {
        var header = _postManager.Markdown.LoadFile(path).Metadata;
        var tags = header.Tags ?? [];

        if (!string.IsNullOrEmpty(trimmedTag) &&
            !tags.Any(t => TrimQuotes(t).Equals(trimmedTag, StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        IReadOnlyList<SearchSnippet> snippets = [];

        if (trimmedQuery.Length > 0)
        {
            snippets = BuildSnippets(path, trimmedQuery);
            if (snippets.Count == 0)
                return null;
        }

        return new PostSearchMatch(
            _fileSystem.Path.GetFileName(path),
            path,
            header.Title,
            isDraft,
            header.PublishedOn,
            tags,
            snippets);
    }

    private List<SearchSnippet> BuildSnippets(string path, string query)
    {
        var snippets = new List<SearchSnippet>();
        var lines = _fileSystem.File.ReadAllLines(path);

        for (var i = 0; i < lines.Length && snippets.Count < MaxSnippetsPerFile; i++)
        {
            var hitIndex = lines[i].IndexOf(query, StringComparison.OrdinalIgnoreCase);
            if (hitIndex < 0)
                continue;

            snippets.Add(new SearchSnippet(i + 1, TrimAroundHit(lines[i], hitIndex)));
        }

        return snippets;
    }

    private static string TrimAroundHit(string line, int hitIndex)
    {
        if (line.Length <= SnippetLength)
            return line;

        var start = Math.Max(0, hitIndex - SnippetLength / 2);
        start = Math.Min(start, line.Length - SnippetLength);
        return line.Substring(start, SnippetLength);
    }

    private static string TrimQuotes(string value) => value.Trim('\'', '"');
}
