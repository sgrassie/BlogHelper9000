using BlogHelper9000.Core.Services;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace BlogHelper9000.Mcp.Tools;

[McpServerToolType]
public static class SearchPostsTool
{
    [McpServerTool(Name = "search_posts", Title = "Search posts and drafts", UseStructuredContent = true, ReadOnly = true, Idempotent = true),
     Description("Searches drafts and published posts for a query and/or tag, to answer questions like 'have I already written about X?'.")]
    public static ToolResponse<SearchPostsResult> SearchPosts(
        IPostSearchService searchService,
        [Description("Case-insensitive substring to search for in the title, front matter, and body. May be blank when tag is supplied, for a tag-only search.")] string query,
        [Description("Restrict to posts/drafts carrying this tag (case-insensitive, ignoring surrounding quotes)")] string? tag = null,
        [Description("Include drafts in the search")] bool includeDrafts = true,
        [Description("Maximum number of matches to return, most recently published first")] int limit = 20)
    {
        return ToolGate.RunExclusive(() =>
        {
            if (string.IsNullOrWhiteSpace(query) && string.IsNullOrWhiteSpace(tag))
                return ToolResponse<SearchPostsResult>.Fail("Provide a query and/or a tag to search for.");

            var results = searchService.Search(query, tag, includeDrafts, limit);

            var matches = results.Matches
                .Select(m => new SearchMatchDto(
                    m.FileName,
                    m.Title,
                    m.IsDraft,
                    m.PublishedOn,
                    m.Tags,
                    m.Snippets.Select(s => new SnippetDto(s.Line, s.Text)).ToList()))
                .ToList();

            return ToolResponse<SearchPostsResult>.Ok(new SearchPostsResult(matches, results.TotalMatches));
        });
    }
}
