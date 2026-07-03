using BlogHelper9000.Core.Helpers;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace BlogHelper9000.Mcp.Tools;

[McpServerToolType]
public static class GetPostTool
{
    [McpServerTool(Name = "get_post", Title = "Read a post", UseStructuredContent = true, ReadOnly = true, Idempotent = true),
     Description("Reads a post or draft's front matter and body text. FrontMatter values are returned as strings; " +
                 "'published' (the post date) is formatted as yyyy-MM-dd. Unknown front-matter keys are included too.")]
    public static ToolResponse<GetPostResult> GetPost(
        PostManager postManager,
        [Description("Filename (e.g. 'my-post.md') or path of the post/draft to read. Bare filenames are resolved against _drafts/ then _posts/; paths outside the blog root are rejected.")] string postPath)
    {
        if (!postManager.TryFindPost(postPath, out var markdownFile))
        {
            return ToolResponse<GetPostResult>.Fail($"Could not find post '{postPath}'.");
        }

        string body;
        try
        {
            body = postManager.GetPostBody(markdownFile.FilePath);
        }
        catch (Exception ex)
        {
            return ToolResponse<GetPostResult>.Fail($"Could not read '{postPath}': {ex.Message}");
        }

        var isDraft = markdownFile.FilePath.StartsWith(postManager.Drafts, StringComparison.Ordinal);
        var frontMatter = BuildFrontMatter(markdownFile.Metadata);

        return ToolResponse<GetPostResult>.Ok(new GetPostResult(markdownFile.FilePath, isDraft, frontMatter, body));
    }

    private static Dictionary<string, string?> BuildFrontMatter(BlogHelper9000.Core.YamlParsing.YamlHeader metadata)
    {
        var frontMatter = new Dictionary<string, string?>
        {
            ["title"] = metadata.Title,
            ["description"] = metadata.Description,
            ["tags"] = string.Join(", ", metadata.Tags),
            ["layout"] = metadata.Layout,
            ["featured_image"] = metadata.FeaturedImage,
            ["featured"] = metadata.IsFeatured?.ToString(),
            ["hidden"] = metadata.IsHidden?.ToString(),
            ["published"] = metadata.PublishedOn?.ToString("yyyy-MM-dd"),
            ["ispublished"] = metadata.IsPublished?.ToString(),
            ["series"] = metadata.Series,
        };

        foreach (var (key, value) in metadata.Extras)
        {
            frontMatter[key] = value;
        }

        return frontMatter;
    }
}
