using BlogHelper9000.Core.Scheduling;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace BlogHelper9000.Mcp.Tools;

[McpServerToolType]
public static class ListSeriesTool
{
    internal const string NoDatabaseError =
        "No schedule database found at the blog root. Ask the user to run 'bloghelper schedule-import <xlsx>' to create it.";

    [McpServerTool(Name = "list_series", Title = "List post series", UseStructuredContent = true, ReadOnly = true, Idempotent = true),
     Description("Lists every post series in the publishing schedule with progress stats: planned/published/remaining counts, " +
                 "percent done, the latest published title, and the next planned post with its slot (a date, 'Week n', or 'unscheduled').")]
    public static ToolResponse<ListSeriesResult> ListSeries(IScheduleService scheduleService)
    {
        if (!scheduleService.DatabaseExists)
            return ToolResponse<ListSeriesResult>.Fail(NoDatabaseError);

        var series = scheduleService.GetDashboard().Series.Select(ToDto).ToList();
        return ToolResponse<ListSeriesResult>.Ok(new ListSeriesResult(series));
    }

    internal static SeriesStatsDto ToDto(SeriesStats s) => new(
        s.Series, s.Planned, s.Published, s.Remaining, s.PercentDone,
        s.LatestPostedTitle, s.NextPlannedTitle, s.NextSlot, s.LastPostedOn, s.OverdueCount);
}
