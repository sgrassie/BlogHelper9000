using BlogHelper9000.Core.Scheduling;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace BlogHelper9000.Mcp.Tools;

[McpServerToolType]
public static class UpdateScheduleEntryTool
{
    [McpServerTool(Name = "update_schedule_entry", Title = "Edit a schedule entry", UseStructuredContent = true, ReadOnly = false, Destructive = true, Idempotent = true),
     Description("Overlays the supplied fields onto an existing schedule entry: move it to another series/position, " +
                 "or change its week, publish date, title, tags, or notes. Only non-null (or clear*) parameters are " +
                 "applied; the rest of the entry is left unchanged. Supply at least one field.")]
    public static ToolResponse<UpdateScheduleEntryResult> UpdateScheduleEntry(
        IScheduleService scheduleService,
        [Description("The post's draft filename, e.g. 'my-post.md'. Date prefixes and paths are tolerated.")] string post,
        [Description("Move the entry into this series. The series must already exist.")] string? series = null,
        [Description("1-based position within the target series.")] int? position = null,
        [Description("Schedule week number.")] int? week = null,
        [Description("Planned publish date, yyyy-MM-dd.")] string? publishDate = null,
        [Description("New title.")] string? title = null,
        [Description("Comma-separated tags.")] string? tags = null,
        [Description("Notes.")] string? notes = null,
        [Description("Clear the week number instead of leaving it unchanged.")] bool clearWeek = false,
        [Description("Clear the publish date instead of leaving it unchanged.")] bool clearPublishDate = false)
    {
        if (!scheduleService.DatabaseExists)
            return ToolResponse<UpdateScheduleEntryResult>.Fail(ListSeriesTool.NoDatabaseError);

        if (series is null && position is null && week is null && publishDate is null &&
            title is null && tags is null && notes is null && !clearWeek && !clearPublishDate)
            return ToolResponse<UpdateScheduleEntryResult>.Fail(
                "Nothing to update — supply at least one of series/position/week/publishDate/title/tags/notes, " +
                "or set clearWeek/clearPublishDate.");

        if (week is not null && clearWeek)
            return ToolResponse<UpdateScheduleEntryResult>.Fail(
                "week and clearWeek are contradictory — supply a new week or clear it, not both.");

        if (publishDate is not null && clearPublishDate)
            return ToolResponse<UpdateScheduleEntryResult>.Fail(
                "publishDate and clearPublishDate are contradictory — supply a new publishDate or clear it, not both.");

        if (week is < 1)
            return ToolResponse<UpdateScheduleEntryResult>.Fail("week must be 1 or greater.");

        DateOnly? date = null;
        if (publishDate is not null)
        {
            if (!DateOnly.TryParse(publishDate, out var parsed))
                return ToolResponse<UpdateScheduleEntryResult>.Fail($"'{publishDate}' is not a valid yyyy-MM-dd date.");
            date = parsed;
        }

        var result = scheduleService.UpdateEntry(post, series, position, week, date, title, tags, notes,
            clearWeek, clearPublishDate);

        return result.Outcome switch
        {
            UpdateEntryOutcome.Updated => ToolResponse<UpdateScheduleEntryResult>.Ok(
                new UpdateScheduleEntryResult(result.Entry!.Series, GetSeriesTool.ToDto(result.Entry))),
            UpdateEntryOutcome.NotScheduled => ToolResponse<UpdateScheduleEntryResult>.Fail(
                $"'{post}' is not on any schedule. Call get_series to inspect the schedules."),
            UpdateEntryOutcome.SeriesNotFound => ToolResponse<UpdateScheduleEntryResult>.Fail(
                $"No series named '{series}'. Call list_series for valid names."),
            UpdateEntryOutcome.InvalidPosition => ToolResponse<UpdateScheduleEntryResult>.Fail(
                $"Position {position} is out of range for the target series."),
            _ => ToolResponse<UpdateScheduleEntryResult>.Fail("Unknown outcome.")
        };
    }
}
