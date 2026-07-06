using BlogHelper9000.Core.Scheduling;
using BlogHelper9000.Core.Services;
using TimeWarp.Nuru;

namespace BlogHelper9000.Commands;

[NuruRoute("publish", Description = "Publish a draft post")]
public sealed class PublishCommand : ICommand<Unit>
{
    [Parameter(Description = "The post to publish.")]
    public string Post { get; set; }

    public class Handler(ILogger<Handler> logger, IBlogService blogService, IScheduleService scheduleService)
        : ICommandHandler<PublishCommand, Unit>
    {
        public ValueTask<Unit> Handle(PublishCommand request, CancellationToken cancellationToken)
        {
            var result = blogService.PublishPost(request.Post);

            if (result is null)
            {
                logger.LogError("Could not find {Post} to publish", request.Post);
                return default;
            }

            logger.LogInformation("Published to {Result}", result);

            switch (scheduleService.MarkPublished(request.Post))
            {
                case MarkPublishedOutcome.Marked:
                    logger.LogInformation("Ticked off the schedule entry for {Post}", request.Post);
                    break;
                case MarkPublishedOutcome.NotScheduled:
                    logger.LogDebug("{Post} is not on any schedule", request.Post);
                    break;
            }

            return default;
        }
    }
}
