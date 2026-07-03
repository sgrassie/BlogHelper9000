namespace BlogHelper9000.Mcp;

/// <summary>
/// A single response shape for every tool, so a calling model can always branch on
/// <see cref="Success"/> instead of inferring outcome from prose or a changing JSON shape.
/// </summary>
public sealed record ToolResponse<T>(bool Success, string? Error, T? Data)
{
    public static ToolResponse<T> Ok(T data) => new(true, null, data);
    public static ToolResponse<T> Fail(string error) => new(false, error, default);
}

public sealed record AddPostResult(string FilePath, bool IsDraft);

public sealed record PublishResult(string? PublishedPath);

public sealed record FixMetadataToolResult(IReadOnlyList<string> Updated, IReadOnlyList<FixMetadataSkipDto> Skipped, bool DryRun);

public sealed record FixMetadataSkipDto(string FilePath, string Reason);

public sealed record DraftListResult(IReadOnlyList<string> Drafts);

public sealed record AddImageResult(string PostTitle, string ImagePath);

public sealed record BlogInfoResult(
    string BaseDirectory,
    bool LooksLikeJekyllBlog,
    int PostCount,
    int UnPublishedCount,
    int? DaysSinceLastPost,
    IReadOnlyList<RecentPostDto> LatestPosts,
    IReadOnlyList<DraftSummaryDto> Unpublished);

public sealed record RecentPostDto(string? Title, DateTime? PublishedOn, IReadOnlyList<string> Tags);

public sealed record DraftSummaryDto(string? Title, string? OriginalFilename);

public sealed record GetPostResult(string FilePath, bool IsDraft, Dictionary<string, string?> FrontMatter, string Body);

public sealed record PostSummary(string FileName, string? Title, DateTime? PublishedOn, IReadOnlyList<string> Tags);

public sealed record ListPostsResult(IReadOnlyList<PostSummary> Posts);

public sealed record UpdatePostResult(string FilePath, IReadOnlyList<string> UpdatedFields);
