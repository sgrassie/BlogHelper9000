using BlogHelper9000.Core.Models;

namespace BlogHelper9000.Core.Services;

public interface IBlogService
{
    /// <summary>
    /// Creates a new post or draft and returns the file path.
    /// </summary>
    string AddPost(string title, bool isDraft, bool isFeatured = false, bool isHidden = false, string? featuredImage = null);

    /// <summary>
    /// Publishes a draft post. Returns the new file path, or null if the post was not found.
    /// </summary>
    string? PublishPost(string postName);

    /// <summary>
    /// Batch-fixes metadata across all posts.
    /// </summary>
    void FixMetadata(bool fixStatus, bool fixDescription, bool fixTags);

    /// <summary>
    /// Gets aggregated blog information/statistics.
    /// </summary>
    BlogMetaInformation GetBlogInfo();

    /// <summary>
    /// Lists draft file paths.
    /// </summary>
    IReadOnlyList<string> ListDrafts();
}
