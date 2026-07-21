using System.Text.RegularExpressions;
using BlogHelper9000.Core.Helpers;
using BlogHelper9000.Imaging;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace BlogHelper9000.Mcp.Tools;

[McpServerToolType]
public static class AddFeaturedImageTool
{
    [McpServerTool(Name = "add_featured_image", Title = "Generate featured image", UseStructuredContent = true, ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = true),
     Description("Generates a featured image for a blog post using Unsplash and overlays the post title. " +
                 "Performs network calls to Unsplash and requires credentials configured on the host machine. " +
                 "If imageQuery is omitted, a query is derived from the post title.")]
    public static async Task<ToolResponse<AddImageResult>> AddFeaturedImage(
        PostManager postManager,
        IUnsplashClient unsplashClient,
        IImageProcessor imageProcessor,
        [Description("Filename (e.g. 'my-post.md') or path of the post to add an image to. Bare filenames are resolved against _drafts/ then _posts/; paths outside the blog root are rejected.")] string postPath,
        [Description("Search query for the Unsplash image (e.g., 'programming', 'nature'). Derived from the post title if omitted.")] string? imageQuery = null,
        CancellationToken cancellationToken = default)
    {
        return await ToolGate.RunExclusiveAsync(async () =>
        {
            if (!postManager.TryFindPost(postPath, out var markdownFile))
            {
                return ToolResponse<AddImageResult>.Fail($"Could not find post '{postPath}'.");
            }

            var effectiveQuery = string.IsNullOrWhiteSpace(imageQuery)
                ? DeriveQueryFromTitle(markdownFile.Metadata.Title)
                : imageQuery;

            await using var imageStream = await unsplashClient.LoadImageAsync(effectiveQuery, cancellationToken);
            if (imageStream is null)
            {
                return ToolResponse<AddImageResult>.Fail("Failed to load image from Unsplash. Check that credentials are configured.");
            }

            await imageProcessor.Process(markdownFile, imageStream, brandingPath: null);

            var (_, savePath) = postManager.CreateImageFilePathForPost(markdownFile);
            return ToolResponse<AddImageResult>.Ok(new AddImageResult(markdownFile.Metadata.Title, savePath));
        }, cancellationToken);
    }

    private static string DeriveQueryFromTitle(string title)
    {
        var words = Regex.Matches(title, @"[A-Za-z0-9]+")
            .Select(m => m.Value)
            .Take(4)
            .ToList();

        return words.Count > 0 ? string.Join(' ', words) : "blog";
    }
}
