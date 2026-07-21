namespace BlogHelper9000.Core.Services;

public interface IPostSearchService
{
    /// <summary>
    /// Searches drafts and posts for <paramref name="query"/> (case-insensitive substring match
    /// against the title, front matter, and body) and/or <paramref name="tag"/>. Either may be
    /// supplied alone for a tag-only or query-only search. This method does not itself require
    /// at least one of them: if both are left blank it simply returns an empty result. Rejecting
    /// a request where both are blank is the caller's responsibility (see <c>SearchPostsTool</c>).
    /// </summary>
    PostSearchResults Search(string query, string? tag = null, bool includeDrafts = true, int limit = 20);
}

public sealed record PostSearchResults(IReadOnlyList<PostSearchMatch> Matches, int TotalMatches);

public sealed record PostSearchMatch(string FileName, string FilePath, string? Title,
    bool IsDraft, DateTime? PublishedOn, IReadOnlyList<string> Tags,
    IReadOnlyList<SearchSnippet> Snippets);

public sealed record SearchSnippet(int Line, string Text);
