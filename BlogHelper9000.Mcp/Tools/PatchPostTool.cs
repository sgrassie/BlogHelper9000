using BlogHelper9000.Core.Helpers;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace BlogHelper9000.Mcp.Tools;

[McpServerToolType]
public static class PatchPostTool
{
    [McpServerTool(Name = "patch_post", Title = "Patch a post's body", UseStructuredContent = true, ReadOnly = false, Destructive = true, Idempotent = false),
     Description("Replaces a single, unique occurrence of 'find' with 'replace' in a post's body — a verifiable, " +
                 "surgical edit for one-paragraph fixes that avoids having to reproduce the whole body like " +
                 "update_post's body parameter requires. Fails if 'find' occurs zero times or more than once, " +
                 "leaving the file unchanged either way; call get_post first if you're unsure of the exact text.")]
    public static ToolResponse<PatchPostResult> PatchPost(
        PostManager postManager,
        [Description("Filename (e.g. 'my-post.md') or path of the post/draft to patch. Bare filenames are resolved against _drafts/ then _posts/; paths outside the blog root are rejected.")] string postPath,
        [Description("Exact text to find in the body — must occur exactly once, or the patch is rejected.")] string find,
        [Description("Text to replace the match with. May be empty to delete the matched text.")] string replace)
    {
        if (!postManager.TryFindPost(postPath, out var markdownFile))
        {
            return ToolResponse<PatchPostResult>.Fail($"Could not find post '{postPath}'.");
        }

        if (string.IsNullOrEmpty(find))
        {
            return ToolResponse<PatchPostResult>.Fail("'find' must not be empty.");
        }

        if (find == replace)
        {
            return ToolResponse<PatchPostResult>.Fail("'find' and 'replace' are identical — this would change nothing.");
        }

        var body = postManager.GetPostBody(markdownFile.FilePath);
        var occurrences = CountOccurrences(body, find);

        if (occurrences == 0)
        {
            return ToolResponse<PatchPostResult>.Fail(
                $"'find' was not found in the body — body unchanged. Call get_post to read the current content.");
        }

        if (occurrences > 1)
        {
            return ToolResponse<PatchPostResult>.Fail(
                $"Found {occurrences} occurrences of 'find' — provide more surrounding context so the match is unique. Body unchanged.");
        }

        var index = body.IndexOf(find, StringComparison.Ordinal);
        var newBody = string.Concat(body.AsSpan(0, index), replace, body.AsSpan(index + find.Length));

        try
        {
            postManager.Markdown.UpdateFile(markdownFile, newBody);
        }
        catch (Exception ex)
        {
            return ToolResponse<PatchPostResult>.Fail($"Could not patch '{postPath}': {ex.Message}");
        }

        return ToolResponse<PatchPostResult>.Ok(new PatchPostResult(markdownFile.FilePath, true));
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        var count = 0;
        var index = 0;

        while ((index = haystack.IndexOf(needle, index, StringComparison.Ordinal)) != -1)
        {
            count++;
            index += needle.Length;
        }

        return count;
    }
}
