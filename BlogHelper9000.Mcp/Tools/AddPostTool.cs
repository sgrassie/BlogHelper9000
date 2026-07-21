using BlogHelper9000.Core.Services;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace BlogHelper9000.Mcp.Tools;

[McpServerToolType]
public static class AddPostTool
{
    [McpServerTool(Name = "add_post", Title = "Create a post or draft", UseStructuredContent = true, ReadOnly = false, Destructive = false, Idempotent = false),
     Description("Creates a new Jekyll blog post or draft with the given title and metadata.")]
    public static ToolResponse<AddPostResult> AddPost(
        IBlogService blogService,
        [Description("The title of the new post. Slugified to a lowercase-hyphenated filename, e.g. 'My Post!' -> my-post.md")] string title,
        [Description("If true, creates a draft in _drafts/; otherwise creates in _posts/")] bool isDraft = true,
        [Description("Whether the post should be marked as featured")] bool isFeatured = false,
        [Description("Whether the post should be hidden")] bool isHidden = false,
        [Description("Optional path to a featured image")] string? featuredImage = null,
        [Description("Optional comma-separated list of tags, e.g. 'csharp, dotnet'")] string? tags = null,
        [Description("Optional markdown body for the post, written below the front matter")] string? content = null)
    {
        return ToolGate.RunExclusive(() =>
        {
            var tagList = string.IsNullOrWhiteSpace(tags)
                ? null
                : tags.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

            var filePath = blogService.AddPost(title, isDraft, isFeatured, isHidden, featuredImage, tagList, content);

            return filePath is null
                ? ToolResponse<AddPostResult>.Fail($"Could not create {(isDraft ? "draft" : "post")} '{title}' — a post already exists at the target path. Choose a different title or edit the existing post.")
                : ToolResponse<AddPostResult>.Ok(new AddPostResult(filePath, isDraft));
        });
    }
}
