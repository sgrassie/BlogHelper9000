using BlogHelper9000.Core.Helpers;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace BlogHelper9000.Mcp.Tools;

[McpServerToolType]
public static class ListPostsTool
{
    [McpServerTool(Name = "list_posts", Title = "List published posts", UseStructuredContent = true, ReadOnly = true, Idempotent = true),
     Description("Lists published blog posts in _posts/, newest first.")]
    public static ToolResponse<ListPostsResult> ListPosts(
        PostManager postManager,
        [Description("Maximum number of posts to return, newest first")] int limit = 20)
    {
        return ToolGate.RunExclusive(() =>
        {
            var posts = postManager.LoadYamlHeaderForAllPosts()
                .Where(header => header.IsPublished == true)
                .OrderByDescending(header => header.PublishedOn)
                .Take(limit)
                .Select(header => new PostSummary(
                    header.Extras.GetValueOrDefault("originalFilename") ?? string.Empty,
                    header.Title,
                    header.PublishedOn,
                    header.Tags ?? []))
                .ToList();

            return ToolResponse<ListPostsResult>.Ok(new ListPostsResult(posts));
        });
    }
}
