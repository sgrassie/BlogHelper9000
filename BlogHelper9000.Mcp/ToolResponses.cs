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

public sealed record PublishResult(string? PublishedPath, string? ScheduleOutcome);

public sealed record FixMetadataToolResult(IReadOnlyList<string> Updated, IReadOnlyList<FixMetadataSkipDto> Skipped, bool DryRun);

public sealed record FixMetadataSkipDto(string FilePath, string Reason);

public sealed record ListDraftsResult(IReadOnlyList<DraftDetailDto> Drafts, int Total);

public sealed record DraftDetailDto(string FileName, string? Title, int WordCount,
    DateTime LastModified, string? Series, string? ScheduleSlot,
    IReadOnlyList<string> ReadinessFlags);

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

public sealed record PatchPostResult(string FilePath, bool Applied);

public sealed record AppendToPostResult(string FilePath);

public sealed record SeriesStatsDto(
    string Series, int Planned, int Published, int Remaining, double PercentDone,
    string? LatestPostedTitle, string? NextPlannedTitle, string? NextSlot, DateOnly? LastPostedOn,
    int OverdueCount);

public sealed record ListSeriesResult(IReadOnlyList<SeriesStatsDto> Series);

public sealed record ScheduleEntryDto(
    int Position, int? Week, DateOnly? PublishDate, string? Topic, string Title,
    string DraftFilename, string? Tags, bool Published, DateOnly? PublishedOn, string? Notes);

public sealed record GetSeriesResult(string Series, IReadOnlyList<ScheduleEntryDto> Entries);

public sealed record ScheduleStatsResult(
    int BaselinePublished, DateOnly? BaselineDate, int PublishedViaSchedules, int TotalPublished,
    int TotalPlanned, int TotalRemaining, double Progress, IReadOnlyList<SeriesStatsDto> Series);

public sealed record AddToSeriesResult(string Series, int Position, string DraftFilename);

public sealed record MarkScheduledResult(string DraftFilename, string Outcome);

public sealed record NextScheduledResult(
    string Series, string Title, string DraftFilename, int? Week, DateOnly? PublishDate);

public sealed record SnippetDto(int Line, string Text);

public sealed record SearchMatchDto(string FileName, string? Title, bool IsDraft,
    DateTime? PublishedOn, IReadOnlyList<string> Tags, IReadOnlyList<SnippetDto> Snippets);

public sealed record SearchPostsResult(IReadOnlyList<SearchMatchDto> Matches, int TotalMatches);

public sealed record TagCountDto(string Tag, int Total, int Published, int Drafts,
    IReadOnlyList<string> Variants);

public sealed record GetTagsResult(IReadOnlyList<TagCountDto> Tags,
    IReadOnlyList<string> NormalisationRules);

public sealed record UpdateScheduleEntryResult(string Series, ScheduleEntryDto Entry);

public sealed record RemoveScheduleEntryResult(
    string DraftFilename, string Series, int Position, bool DryRun, bool Removed);

public sealed record RenameSeriesResult(string OldName, string NewName);

public sealed record DeleteSeriesResult(string Name, bool DryRun, bool Deleted);

public sealed record SetSeriesCadenceResult(string Series, string CadenceDay, string CadenceStart);

public sealed record DuePostDto(
    string Series, string Title, string DraftFilename, string DueDate,
    bool Overdue, int DaysOverdue, int? Week, int Position);

public sealed record GetDuePostsResult(IReadOnlyList<DuePostDto> Due);
