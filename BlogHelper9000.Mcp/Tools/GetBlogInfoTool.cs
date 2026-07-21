using BlogHelper9000.Core;
using BlogHelper9000.Core.Services;
using ModelContextProtocol.Server;
using Microsoft.Extensions.Options;
using System.ComponentModel;
using System.IO.Abstractions;

namespace BlogHelper9000.Mcp.Tools;

[McpServerToolType]
public static class GetBlogInfoTool
{
    [McpServerTool(Name = "get_blog_info", Title = "Get blog statistics", UseStructuredContent = true, ReadOnly = true, Idempotent = true),
     Description("Returns blog statistics: base directory, total post count, unpublished count, recent posts, and days since last post. " +
                 "Call this first in a session to confirm the server is pointed at the right blog.")]
    public static ToolResponse<BlogInfoResult> GetBlogInfo(IBlogService blogService, IOptions<BlogHelperOptions> options, IFileSystem fileSystem)
    {
        return ToolGate.RunExclusive(() =>
        {
            var baseDirectory = options.Value.BaseDirectory;
            var looksLikeJekyllBlog = fileSystem.Directory.Exists(fileSystem.Path.Combine(baseDirectory, "_posts"))
                || fileSystem.Directory.Exists(fileSystem.Path.Combine(baseDirectory, "_drafts"));

            var info = blogService.GetBlogInfo();

            var latestPosts = (info.LatestPosts ?? [])
                .Select(p => new RecentPostDto(p.Title, p.PublishedOn, p.Tags ?? []))
                .ToList();

            var unpublished = (info.Unpublished ?? [])
                .Select(p => new DraftSummaryDto(p.Title, p.Extras.GetValueOrDefault("originalFilename")))
                .ToList();

            var result = new BlogInfoResult(
                baseDirectory,
                looksLikeJekyllBlog,
                info.PostCount,
                info.UnPublishedCount,
                info.DaysSinceLastPost?.Days,
                latestPosts,
                unpublished);

            return ToolResponse<BlogInfoResult>.Ok(result);
        });
    }
}
