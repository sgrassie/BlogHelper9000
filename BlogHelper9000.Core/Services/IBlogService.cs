using BlogHelper9000.Core.Models;

namespace BlogHelper9000.Core.Services;

public interface IBlogService
{
    /// <summary>
    /// Creates a new post or draft and returns the file path, or null if a post already exists at the target path.
    /// </summary>
    string? AddPost(string title, bool isDraft, bool isFeatured = false, bool isHidden = false, string? featuredImage = null, IReadOnlyList<string>? tags = null, string? content = null);

    /// <summary>
    /// Publishes a draft post. Returns the new file path, or null if the post was not found,
    /// already published, or the target path collides with an existing file.
    /// </summary>
    string? PublishPost(string postName);

    /// <summary>
    /// Publishes a draft post, distinguishing why it did not publish when it fails.
    /// </summary>
    PublishPostResult PublishPostDetailed(string postName);

    /// <summary>
    /// Batch-fixes metadata across all posts. When <paramref name="dryRun"/> is true, computes
    /// and reports what would change without writing any files.
    /// </summary>
    FixMetadataResult FixMetadata(bool fixStatus, bool fixDescription, bool fixTags, bool dryRun = false);

    /// <summary>
    /// Gets aggregated blog information/statistics.
    /// </summary>
    BlogMetaInformation GetBlogInfo();

    /// <summary>
    /// Lists draft file paths.
    /// </summary>
    IReadOnlyList<string> ListDrafts();

    /// <summary>
    /// Per-draft triage metadata (title, word count, last modified, readiness flags),
    /// ordered by most recently modified first.
    /// </summary>
    IReadOnlyList<DraftDetail> GetDraftDetails();
}
