using BlogHelper9000.Core.Helpers;
using BlogHelper9000.Imaging;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace BlogHelper9000.Mcp.Tools;

[McpServerToolType]
public static class AddFeaturedImageTool
{
    [McpServerTool(Name = "add_featured_image"), Description("Generates a featured image for a blog post using Unsplash and overlays the post title.")]
    public static async Task<string> AddFeaturedImage(
        PostManager postManager,
        IUnsplashClient unsplashClient,
        IImageProcessor imageProcessor,
        [Description("The filename or path of the post to add an image to")] string postPath,
        [Description("Search query for the Unsplash image (e.g., 'programming', 'nature')")] string imageQuery = "programming")
    {
        if (!postManager.TryFindPost(postPath, out var markdownFile))
        {
            return $"Could not find post '{postPath}'.";
        }

        var imageStream = await unsplashClient.LoadImageAsync(imageQuery);
        if (imageStream == Stream.Null)
        {
            return "Failed to load image from Unsplash. Check that credentials are configured.";
        }

        await imageProcessor.Process(markdownFile, imageStream, brandingPath: null);
        return $"Featured image generated and applied to '{markdownFile.Metadata.Title}'.";
    }
}
