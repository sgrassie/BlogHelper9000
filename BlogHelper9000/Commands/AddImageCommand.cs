using BlogHelper9000.Core.Helpers;
using BlogHelper9000.Imaging;
using TimeWarp.Nuru;

namespace BlogHelper9000.Commands;

public class AddImageCommand : ICommand<Unit>
{
    [Parameter(Description = "The post to add the image to.")]
    public string Post { get; set; }
    [Parameter(Description = "The query to use when searching for the image on Unsplash.")]
    public string ImageQuery { get; set; }
    [Parameter(Description = "The author branding to add to the image.")]
    public string AuthorBranding { get; set; }

    public class Handler(ILogger<AddImageCommand.Handler> logger, PostManager postManager, IUnsplashClient unsplashClient, IImageProcessor imageProcessor)
        : ICommandHandler<AddImageCommand, Unit>
    {
        public async ValueTask<Unit> Handle(AddImageCommand request, CancellationToken cancellationToken)
        {
            if (postManager.TryFindPost(request.Post, out var postMarkdown))
            {
                if (postManager.TryFindAuthorBranding(request.AuthorBranding, out var brandingPath))
                {
                    await using var stream = await unsplashClient.LoadImageAsync(request.ImageQuery);
                    await imageProcessor.Process(postMarkdown, stream, brandingPath);
                }
                else
                {
                    logger.LogError("Could not load author branding image {AuthorBranding}", request.AuthorBranding);
                }
            }
            else
            {
                logger.LogError("Could not find {Post} to add an image to", request.Post);
            }

            return default;
        }
    }

}
