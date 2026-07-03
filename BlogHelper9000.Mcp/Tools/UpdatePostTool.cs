using BlogHelper9000.Core.Helpers;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace BlogHelper9000.Mcp.Tools;

[McpServerToolType]
public static class UpdatePostTool
{
    [McpServerTool(Name = "update_post", Title = "Update a post", UseStructuredContent = true, ReadOnly = false, Destructive = true, Idempotent = true),
     Description("Updates a post or draft's front matter and/or body. Only supplied fields are changed. " +
                 "body REPLACES the entire post body — call get_post first to read the current content before overwriting it.")]
    public static ToolResponse<UpdatePostResult> UpdatePost(
        PostManager postManager,
        [Description("Filename (e.g. 'my-post.md') or path of the post/draft to update. Bare filenames are resolved against _drafts/ then _posts/; paths outside the blog root are rejected.")] string postPath,
        [Description("New title")] string? title = null,
        [Description("New description")] string? description = null,
        [Description("New comma-separated tags, e.g. 'csharp, dotnet' — replaces all existing tags")] string? tags = null,
        [Description("New markdown body — replaces the entire existing body")] string? body = null)
    {
        if (!postManager.TryFindPost(postPath, out var markdownFile))
        {
            return ToolResponse<UpdatePostResult>.Fail($"Could not find post '{postPath}'.");
        }

        var updatedFields = new List<string>();

        if (title is not null)
        {
            markdownFile.Metadata.Title = title;
            updatedFields.Add("title");
        }

        if (description is not null)
        {
            markdownFile.Metadata.Description = description;
            updatedFields.Add("description");
        }

        if (tags is not null)
        {
            markdownFile.Metadata.Tags = tags
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .ToList();
            updatedFields.Add("tags");
        }

        try
        {
            if (body is not null)
            {
                postManager.Markdown.UpdateFile(markdownFile, body);
                updatedFields.Add("body");
            }
            else if (updatedFields.Count > 0)
            {
                postManager.UpdateMarkdown(markdownFile);
            }
        }
        catch (Exception ex)
        {
            return ToolResponse<UpdatePostResult>.Fail($"Could not update '{postPath}': {ex.Message}");
        }

        return ToolResponse<UpdatePostResult>.Ok(new UpdatePostResult(markdownFile.FilePath, updatedFields));
    }
}
