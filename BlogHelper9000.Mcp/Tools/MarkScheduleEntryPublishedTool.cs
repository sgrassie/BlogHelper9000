using BlogHelper9000.Core.Scheduling;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace BlogHelper9000.Mcp.Tools;

[McpServerToolType]
public static class MarkScheduleEntryPublishedTool
{
    [McpServerTool(Name = "mark_schedule_entry_published", Title = "Tick a schedule entry", UseStructuredContent = true, ReadOnly = false, Destructive = false, Idempotent = true),
     Description("Marks a schedule entry as published (or un-marks it) without touching the post file. " +
                 "Note publish_post already ticks the schedule automatically — use this for posts published " +
                 "by other means, or to correct mistakes.")]
    public static ToolResponse<MarkScheduledResult> MarkScheduleEntryPublished(
        IScheduleService scheduleService,
        [Description("The post's draft filename, e.g. 'my-post.md'. Date prefixes and paths are tolerated.")] string post,
        [Description("Optional actual publish date, yyyy-MM-dd; defaults to today.")] string? publishedOn = null,
        [Description("If true, un-ticks the entry instead.")] bool unmark = false)
    {
        if (!scheduleService.DatabaseExists)
            return ToolResponse<MarkScheduledResult>.Fail(ListSeriesTool.NoDatabaseError);

        DateOnly? date = null;
        if (publishedOn is not null)
        {
            if (!DateOnly.TryParse(publishedOn, out var parsed))
                return ToolResponse<MarkScheduledResult>.Fail($"'{publishedOn}' is not a valid yyyy-MM-dd date.");
            date = parsed;
        }

        var outcome = scheduleService.MarkPublished(post, date, unmark);
        return outcome == MarkPublishedOutcome.NotScheduled
            ? ToolResponse<MarkScheduledResult>.Fail($"'{post}' is not on any schedule. Call get_series to inspect the schedules.")
            : ToolResponse<MarkScheduledResult>.Ok(new MarkScheduledResult(post, outcome.ToString()));
    }
}
