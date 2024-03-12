using BlogHelper9000.Helpers;
using BlogHelper9000.Imager;

namespace BlogHelper9000.Handlers;

public class ImageCommandAddSubCommandHandler(ILogger logger, PostManager postManager, IUnsplashClient unsplashClient, IImageProcessor imageProcessor)
{
    public async Task Execute(string post, string imageQuery, string authorBranding)
    {
        if (postManager.TryFindPost(post, out var postMarkdown))
        {
            if (postManager.TryFindAuthorBranding(authorBranding, out var brandingPath))
            {
                await using var stream = await unsplashClient.LoadImageAsync(imageQuery);
                await imageProcessor.Process(postMarkdown, stream, brandingPath);
            }
            else
            {
                logger.LogError("Could not load author branding image {AuthorBranding}", authorBranding);
            }
        }
        else
        {
            logger.LogError("Could not find {Post} to add an image to", post);
        }
    }
}