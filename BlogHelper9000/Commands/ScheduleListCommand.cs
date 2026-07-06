using BlogHelper9000.Core.Scheduling;
using BlogHelper9000.Reporters;
using TimeWarp.Nuru;

namespace BlogHelper9000.Commands;

[NuruRoute("schedule-list", Description = "List post series with their schedule progress")]
public sealed class ScheduleListCommand : ICommand<Unit>
{
    public sealed class Handler(ILogger<Handler> logger, IScheduleService scheduleService, ScheduleReporter reporter)
        : ICommandHandler<ScheduleListCommand, Unit>
    {
        public ValueTask<Unit> Handle(ScheduleListCommand request, CancellationToken cancellationToken)
        {
            if (!scheduleService.DatabaseExists)
            {
                logger.LogError("No schedule database found — run 'bloghelper schedule-import <xlsx>' first");
                return default;
            }

            reporter?.ReportSeries(scheduleService.GetDashboard().Series);
            return default;
        }
    }
}
