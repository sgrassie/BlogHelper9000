using BlogHelper9000.Core.Scheduling;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace BlogHelper9000.Mcp.Tools;

[McpServerToolType]
public static class GetSeriesTool
{
    [McpServerTool(Name = "get_series", Title = "Get a series' schedule", UseStructuredContent = true, ReadOnly = true, Idempotent = true),
     Description("Returns every schedule entry in a series, in order: position, week, planned date, topic, title, " +
                 "draft filename, tags, published state, and notes. Use list_series to discover series names.")]
    public static ToolResponse<GetSeriesResult> GetSeries(
        IScheduleService scheduleService,
        [Description("The series name, e.g. 'FootballData' (case-insensitive).")] string series)
    {
        if (!scheduleService.DatabaseExists)
            return ToolResponse<GetSeriesResult>.Fail(ListSeriesTool.NoDatabaseError);

        var entries = scheduleService.GetSeriesEntries(series);
        if (entries.Count == 0)
            return ToolResponse<GetSeriesResult>.Fail(
                $"No series named '{series}' (or it has no entries). Call list_series for valid names.");

        return ToolResponse<GetSeriesResult>.Ok(new GetSeriesResult(series, entries.Select(ToDto).ToList()));
    }

    internal static ScheduleEntryDto ToDto(ScheduleEntry e) => new(e.Position, e.Week, e.PublishDate, e.Topic,
        e.Title, e.DraftFilename, e.Tags, e.Published, e.PublishedOn, e.Notes);
}
