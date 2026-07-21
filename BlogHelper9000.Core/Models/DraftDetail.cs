namespace BlogHelper9000.Core.Models;

/// <summary>
/// Per-draft triage metadata: enough for an LLM to decide which drafts are worth picking up,
/// without the full body a <c>get_post</c> call would return.
/// </summary>
public sealed record DraftDetail(string FileName, string FilePath, string? Title,
    int WordCount, DateTime LastModified, bool HasFeaturedImage,
    IReadOnlyList<string> ReadinessFlags);
