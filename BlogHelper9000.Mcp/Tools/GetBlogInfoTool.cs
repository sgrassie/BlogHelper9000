using BlogHelper9000.Core.Services;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace BlogHelper9000.Mcp.Tools;

[McpServerToolType]
public static class GetBlogInfoTool
{
    [McpServerTool(Name = "get_blog_info"), Description("Returns blog statistics: total post count, unpublished count, recent posts, and days since last post.")]
    public static string GetBlogInfo(IBlogService blogService)
    {
        var info = blogService.GetBlogInfo();
        return JsonSerializer.Serialize(new
        {
            info.PostCount,
            info.UnPublishedCount,
            DaysSinceLastPost = info.DaysSinceLastPost.Days,
            LatestPosts = info.LatestPosts?.Select(p => new
            {
                p.Title,
                p.PublishedOn,
                p.Tags
            }),
            Unpublished = info.Unpublished?.Select(p => new
            {
                p.Title,
                OriginalFilename = p.Extras.GetValueOrDefault("originalFilename")
            })
        }, new JsonSerializerOptions { WriteIndented = true });
    }
}
