using BlogHelper9000.Core.Services;
using TimeWarp.Nuru;

namespace BlogHelper9000.Commands;

public sealed class PublishCommand : ICommand<Unit>
{
    [Parameter(Description = "The post to publish.")]
    public string Post { get; set; }

    public class Handler(ILogger<Handler> logger, IBlogService blogService)
        : ICommandHandler<PublishCommand, Unit>
    {
        public ValueTask<Unit> Handle(PublishCommand request, CancellationToken cancellationToken)
        {
            var result = blogService.PublishPost(request.Post);

            if (result is null)
                logger.LogError("Could not find {Post} to publish", request.Post);
            else
                logger.LogInformation("Published to {Result}", result);

            return default;
        }
    }
}
