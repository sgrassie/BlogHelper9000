using BlogHelper9000.Core.Services;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace BlogHelper9000.Mcp.Tools;

[McpServerToolType]
public static class ListDraftsTool
{
    [McpServerTool(Name = "list_drafts"), Description("Lists all draft blog posts in the _drafts/ directory.")]
    public static string ListDrafts(IBlogService blogService)
    {
        var drafts = blogService.ListDrafts();
        return drafts.Count == 0
            ? "No drafts found."
            : JsonSerializer.Serialize(drafts, new JsonSerializerOptions { WriteIndented = true });
    }
}
