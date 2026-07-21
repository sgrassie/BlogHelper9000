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
                 "If imageQuery is omitted, a query is derived from the post title. " +
                 "If the post already has a featured image, the call fails unless replace=true — " +
                 "supplying photoId alone does not bypass this guard, since photoId only selects which " +
                 "Unsplash photo to use, it is not consent to overwrite an existing image.")]
    public static async Task<ToolResponse<AddImageResult>> AddFeaturedImage(
        PostManager postManager,
        IUnsplashClient unsplashClient,
        IImageProcessor imageProcessor,
        [Description("Filename (e.g. 'my-post.md') or path of the post to add an image to. Bare filenames are resolved against _drafts/ then _posts/; paths outside the blog root are rejected.")] string postPath,
        [Description("Search query for the Unsplash image (e.g., 'programming', 'nature'). Derived from the post title if omitted. Ignored (but still used for logging) when photoId is supplied.")] string? imageQuery = null,
        [Description("Regenerate the featured image even if the post already has one. Required to overwrite an existing image — photoId alone does not imply consent to replace.")] bool replace = false,
        [Description("Use this exact Unsplash photo instead of a random search result. If both imageQuery and photoId are given, photoId wins. Does NOT bypass the replace guard.")] string? photoId = null,
        CancellationToken cancellationToken = default)
    {
        return await ToolGate.RunExclusiveAsync(async () =>
        {
            if (!postManager.TryFindPost(postPath, out var markdownFile))
            {
                return ToolResponse<AddImageResult>.Fail($"Could not find post '{postPath}'.");
            }

            if (!string.IsNullOrWhiteSpace(markdownFile.Metadata.FeaturedImage) && !replace)
            {
                return ToolResponse<AddImageResult>.Fail(
                    $"Post already has a featured image ('{markdownFile.Metadata.FeaturedImage}') — " +
                    "pass replace=true to regenerate (optionally with photoId to pin a specific Unsplash photo).");
            }

            var effectiveQuery = string.IsNullOrWhiteSpace(imageQuery)
                ? DeriveQueryFromTitle(markdownFile.Metadata.Title)
                : imageQuery;

            UnsplashImageResult? result;
            try
            {
                result = await unsplashClient.LoadImageAsync(effectiveQuery, photoId, cancellationToken);
            }
            catch (HttpRequestException ex)
            {
                var idHint = string.IsNullOrWhiteSpace(photoId) ? string.Empty : $" (photo id '{photoId}'?)";
                var detail = ex.StatusCode is { } status ? $"{(int)status} {status}" : ex.Message;
                return ToolResponse<AddImageResult>.Fail(
                    $"Unsplash request failed{idHint}: {detail} — check the id or retry.");
            }

            if (result is null)
            {
                return ToolResponse<AddImageResult>.Fail("Failed to load image from Unsplash. Check that credentials are configured.");
            }

            await using var imageStream = result.Image;
            await imageProcessor.Process(markdownFile, imageStream, brandingPath: null, result.Attribution);

            var (_, savePath) = postManager.CreateImageFilePathForPost(markdownFile);
            return ToolResponse<AddImageResult>.Ok(new AddImageResult(
                markdownFile.Metadata.Title,
                savePath,
                result.PhotoId,
                result.PhotographerName,
                result.PhotographerProfileUrl,
                result.PhotoUrl,
                result.Attribution));
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
