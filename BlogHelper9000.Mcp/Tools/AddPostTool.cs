using BlogHelper9000.Core.Services;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace BlogHelper9000.Mcp.Tools;

[McpServerToolType]
public static class AddPostTool
{
    [McpServerTool(Name = "add_post"), Description("Creates a new Jekyll blog post or draft with the given title and metadata.")]
    public static string AddPost(
        IBlogService blogService,
        [Description("The title of the new post")] string title,
        [Description("If true, creates a draft in _drafts/; otherwise creates in _posts/")] bool isDraft = true,
        [Description("Whether the post should be marked as featured")] bool isFeatured = false,
        [Description("Whether the post should be hidden")] bool isHidden = false,
        [Description("Optional path to a featured image")] string? featuredImage = null)
    {
        var filePath = blogService.AddPost(title, isDraft, isFeatured, isHidden, featuredImage);
        return $"Created {(isDraft ? "draft" : "post")} at: {filePath}";
    }
}
