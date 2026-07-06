using BlogHelper9000.Core.Scheduling;
using TimeWarp.Nuru;

namespace BlogHelper9000.Commands;

[NuruRoute("schedule-mark", Description = "Manually tick (or untick) a schedule entry as published")]
public sealed class ScheduleMarkCommand : ICommand<Unit>
{
    [Parameter(Description = "The post's draft filename, e.g. 'my-post.md'.")]
    public string Post { get; set; }

    [Option("date", "d", Description = "The date it was published (yyyy-MM-dd); defaults to today.")]
    public string? Date { get; set; }

    [Option("unmark", "u", Description = "Untick the entry instead.")]
    public bool Unmark { get; set; }

    public sealed class Handler(ILogger<Handler> logger, IScheduleService scheduleService)
        : ICommandHandler<ScheduleMarkCommand, Unit>
    {
        public ValueTask<Unit> Handle(ScheduleMarkCommand request, CancellationToken cancellationToken)
        {
            DateOnly? publishedOn = DateOnly.TryParse(request.Date, out var parsed) ? parsed : null;
            var outcome = scheduleService.MarkPublished(request.Post, publishedOn, request.Unmark);

            switch (outcome)
            {
                case MarkPublishedOutcome.Marked:
                    logger.LogInformation("Marked {Post} as published", request.Post);
                    break;
                case MarkPublishedOutcome.Unmarked:
                    logger.LogInformation("Unmarked {Post}", request.Post);
                    break;
                case MarkPublishedOutcome.AlreadyMarked:
                    logger.LogWarning("{Post} is already marked as published", request.Post);
                    break;
                case MarkPublishedOutcome.NotScheduled:
                    logger.LogError("{Post} is not on any schedule — check 'bloghelper schedule-show <series>'", request.Post);
                    break;
            }

            return default;
        }
    }
}
