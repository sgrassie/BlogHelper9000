using BlogHelper9000.Core.Services;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace BlogHelper9000.Mcp.Tools;

[McpServerToolType]
public static class ListDraftsTool
{
    [McpServerTool(Name = "list_drafts", Title = "List draft posts", UseStructuredContent = true, ReadOnly = true, Idempotent = true),
     Description("Lists all draft blog posts in the _drafts/ directory. Returns an empty list, not an error, when there are no drafts.")]
    public static ToolResponse<DraftListResult> ListDrafts(IBlogService blogService)
    {
        var drafts = blogService.ListDrafts();
        return ToolResponse<DraftListResult>.Ok(new DraftListResult(drafts));
    }
}
