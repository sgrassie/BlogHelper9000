using BlogHelper9000.Core.Helpers;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace BlogHelper9000.Mcp.Tools;

[McpServerToolType]
public static class AppendToPostTool
{
    [McpServerTool(Name = "append_to_post", Title = "Append to a post's body", UseStructuredContent = true, ReadOnly = false, Destructive = false, Idempotent = false),
     Description("Appends content to the end of a post's body, separated from the existing content by a blank " +
                 "line. Use this to add a new section or paragraph without reproducing the whole body like " +
                 "update_post's body parameter requires.")]
    public static ToolResponse<AppendToPostResult> AppendToPost(
        PostManager postManager,
        [Description("Filename (e.g. 'my-post.md') or path of the post/draft to append to. Bare filenames are resolved against _drafts/ then _posts/; paths outside the blog root are rejected.")] string postPath,
        [Description("Markdown content to append to the end of the body.")] string content)
    {
        return ToolGate.RunExclusive(() =>
        {
            if (!postManager.TryFindPost(postPath, out var markdownFile))
            {
                return ToolResponse<AppendToPostResult>.Fail($"Could not find post '{postPath}'.");
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                return ToolResponse<AppendToPostResult>.Fail("'content' must not be empty.");
            }

            var existingBody = postManager.GetPostBody(markdownFile.FilePath);
            var newBody = string.IsNullOrWhiteSpace(existingBody)
                ? content
                : existingBody.TrimEnd('\n') + "\n\n" + content;

            try
            {
                postManager.Markdown.UpdateFile(markdownFile, newBody);
            }
            catch (Exception ex)
            {
                return ToolResponse<AppendToPostResult>.Fail($"Could not append to '{postPath}': {ex.Message}");
            }

            return ToolResponse<AppendToPostResult>.Ok(new AppendToPostResult(markdownFile.FilePath));
        });
    }
}
