using BlogHelper9000.Core.Scheduling;
using TimeWarp.Nuru;

namespace BlogHelper9000.Commands;

[NuruRoute("schedule-rebase", Description = "Push back (or pull forward) a slipped series' unpublished schedule in one move")]
public sealed class ScheduleRebaseCommand : ICommand<Unit>
{
    [Parameter(Description = "The series to rebase, e.g. 'FootballData'.")]
    public string Series { get; set; }

    [Option("start", "s", Description = "New start date for the first unpublished entry (yyyy-MM-dd); defaults to the next occurrence of the series' weekday after today.")]
    public string? Start { get; set; }

    [Option("days", "d", Description = "Shift every unpublished entry by this many days instead (negative pulls forward).")]
    public int? Days { get; set; }

    [Option("weeks", "w", Description = "Shift every unpublished entry by this many weeks instead (negative pulls forward).")]
    public int? Weeks { get; set; }

    public sealed class Handler(ILogger<Handler> logger, IScheduleService scheduleService)
        : ICommandHandler<ScheduleRebaseCommand, Unit>
    {
        public ValueTask<Unit> Handle(ScheduleRebaseCommand request, CancellationToken cancellationToken)
        {
            if (!scheduleService.DatabaseExists)
            {
                logger.LogError("No schedule database found — run 'bloghelper schedule-import <xlsx>' first");
                return default;
            }

            if (request.Start is not null && (request.Days is not null || request.Weeks is not null))
            {
                logger.LogError("--start and --days/--weeks are contradictory — rebase to a date or shift by a delta, not both");
                return default;
            }

            if (request.Days is not null && request.Weeks is not null)
            {
                logger.LogError("--days and --weeks are contradictory — supply one delta, not both");
                return default;
            }

            if (request.Days is 0 || request.Weeks is 0)
            {
                logger.LogError("The shift delta must be non-zero");
                return default;
            }

            DateOnly? newStartDate = null;
            if (request.Start is not null)
            {
                if (!DateOnly.TryParse(request.Start, out var parsed))
                {
                    logger.LogError("'{Start}' is not a valid yyyy-MM-dd date", request.Start);
                    return default;
                }
                newStartDate = parsed;
            }

            var result = request.Days is not null || request.Weeks is not null
                ? scheduleService.ShiftSeries(request.Series, request.Days ?? request.Weeks!.Value * 7)
                : scheduleService.RebaseSeries(request.Series, newStartDate);

            switch (result.Outcome)
            {
                case RebaseSeriesOutcome.Rebased:
                    logger.LogInformation("Moved {EntriesMoved} entries in '{Series}': {OldStart} -> {NewStart}",
                        result.EntriesMoved, request.Series,
                        result.OldStartDate!.Value.ToString("yyyy-MM-dd"),
                        result.NewStartDate!.Value.ToString("yyyy-MM-dd"));
                    break;
                case RebaseSeriesOutcome.SeriesNotFound:
                    logger.LogError("No series named '{Series}' — try 'bloghelper schedule-list'", request.Series);
                    break;
                case RebaseSeriesOutcome.NothingToMove:
                    logger.LogWarning("'{Series}' has no unpublished entries with a publish date — nothing to rebase", request.Series);
                    break;
            }

            return default;
        }
    }
}
