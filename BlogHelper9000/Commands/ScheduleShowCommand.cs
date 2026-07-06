using BlogHelper9000.Core.Scheduling;
using BlogHelper9000.Reporters;
using TimeWarp.Nuru;

namespace BlogHelper9000.Commands;

[NuruRoute("schedule-show", Description = "Show the entries in a series")]
public sealed class ScheduleShowCommand : ICommand<Unit>
{
    [Parameter(Description = "The series to show, e.g. 'FootballData'.")]
    public string Series { get; set; }

    public sealed class Handler(ILogger<Handler> logger, IScheduleService scheduleService, ScheduleReporter reporter)
        : ICommandHandler<ScheduleShowCommand, Unit>
    {
        public ValueTask<Unit> Handle(ScheduleShowCommand request, CancellationToken cancellationToken)
        {
            if (!scheduleService.DatabaseExists)
            {
                logger.LogError("No schedule database found — run 'bloghelper schedule-import <xlsx>' first");
                return default;
            }

            var entries = scheduleService.GetSeriesEntries(request.Series);
            if (entries.Count == 0)
            {
                logger.LogError("No series named '{Series}' (or it has no entries) — try 'bloghelper schedule-list'", request.Series);
                return default;
            }

            reporter?.ReportEntries(request.Series, entries);
            return default;
        }
    }
}
