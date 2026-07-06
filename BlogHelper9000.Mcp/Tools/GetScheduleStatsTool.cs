using BlogHelper9000.Core.Scheduling;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace BlogHelper9000.Mcp.Tools;

[McpServerToolType]
public static class GetScheduleStatsTool
{
    [McpServerTool(Name = "get_schedule_stats", Title = "Publishing dashboard", UseStructuredContent = true, ReadOnly = true, Idempotent = true),
     Description("Returns the publishing dashboard: baseline published count, posts published via the schedules, " +
                 "total published, total planned, drafts remaining, overall progress (0-1), and per-series stats.")]
    public static ToolResponse<ScheduleStatsResult> GetScheduleStats(IScheduleService scheduleService)
    {
        if (!scheduleService.DatabaseExists)
            return ToolResponse<ScheduleStatsResult>.Fail(ListSeriesTool.NoDatabaseError);

        var dashboard = scheduleService.GetDashboard();
        return ToolResponse<ScheduleStatsResult>.Ok(new ScheduleStatsResult(
            dashboard.BaselinePublished, dashboard.BaselineDate, dashboard.PublishedViaSchedules,
            dashboard.TotalPublished, dashboard.TotalPlanned, dashboard.TotalRemaining,
            dashboard.Progress, dashboard.Series.Select(ListSeriesTool.ToDto).ToList()));
    }
}
