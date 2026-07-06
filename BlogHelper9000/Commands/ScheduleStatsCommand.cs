using BlogHelper9000.Core.Scheduling;
using BlogHelper9000.Reporters;
using TimeWarp.Nuru;

namespace BlogHelper9000.Commands;

[NuruRoute("schedule-stats", Description = "Show the publishing dashboard (like the old spreadsheet)")]
public sealed class ScheduleStatsCommand : ICommand<Unit>
{
    public sealed class Handler(ILogger<Handler> logger, IScheduleService scheduleService, ScheduleReporter reporter)
        : ICommandHandler<ScheduleStatsCommand, Unit>
    {
        public ValueTask<Unit> Handle(ScheduleStatsCommand request, CancellationToken cancellationToken)
        {
            if (!scheduleService.DatabaseExists)
            {
                logger.LogError("No schedule database found — run 'bloghelper schedule-import <xlsx>' first");
                return default;
            }

            reporter?.ReportDashboard(scheduleService.GetDashboard());
            return default;
        }
    }
}
