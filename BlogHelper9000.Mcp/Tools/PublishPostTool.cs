using BlogHelper9000.Core.Services;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace BlogHelper9000.Mcp.Tools;

[McpServerToolType]
public static class PublishPostTool
{
    [McpServerTool(Name = "publish_post"), Description("Publishes a draft post, moving it from _drafts/ to _posts/ with a date prefix.")]
    public static string PublishPost(
        IBlogService blogService,
        [Description("The filename or path of the draft to publish")] string postName)
    {
        var result = blogService.PublishPost(postName);
        return result is not null
            ? $"Published to: {result}"
            : $"Could not find post '{postName}' to publish.";
    }
}
