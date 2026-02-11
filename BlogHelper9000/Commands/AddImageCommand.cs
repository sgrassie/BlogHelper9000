using BlogHelper9000.Core.Helpers;
using BlogHelper9000.Imaging;
using TimeWarp.Mediator;

namespace BlogHelper9000.Commands;

public class AddImageCommand : IRequest
{
    public string Post { get; set; }
    public string ImageQuery { get; set; }
    public string AuthorBranding { get; set; }

    public class Handler(ILogger<AddImageCommand.Handler> logger, PostManager postManager, IUnsplashClient unsplashClient, IImageProcessor imageProcessor)
        : IRequestHandler<AddImageCommand>
    {
        public async Task Handle(AddImageCommand request, CancellationToken cancellationToken)
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
        }
    }

}
