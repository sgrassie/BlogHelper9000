using BlogHelper9000.Core.Scheduling;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace BlogHelper9000.Mcp.Tools;

[McpServerToolType]
public static class AddPostToSeriesTool
{
    [McpServerTool(Name = "add_post_to_series", Title = "Schedule a post in a series", UseStructuredContent = true, ReadOnly = false, Destructive = false, Idempotent = false),
     Description("Appends a post to a schedule series at the next position, creating the series if it does not exist. " +
                 "This only updates the schedule — use add_post first if the draft file itself does not exist yet.")]
    public static ToolResponse<AddToSeriesResult> AddPostToSeries(
        IScheduleService scheduleService,
        [Description("The series to add to, e.g. 'FootballData'. Created if missing.")] string series,
        [Description("The post title.")] string title,
        [Description("The draft filename, e.g. 'my-post.md'.")] string draftFilename,
        [Description("Optional schedule week number.")] int? week = null,
        [Description("Optional planned publish date, yyyy-MM-dd.")] string? publishDate = null,
        [Description("Optional comma-separated tags.")] string? tags = null,
        [Description("Optional notes.")] string? notes = null)
    {
        if (!scheduleService.DatabaseExists)
            return ToolResponse<AddToSeriesResult>.Fail(ListSeriesTool.NoDatabaseError);

        DateOnly? date = null;
        if (publishDate is not null)
        {
            if (!DateOnly.TryParse(publishDate, out var parsed))
                return ToolResponse<AddToSeriesResult>.Fail($"'{publishDate}' is not a valid yyyy-MM-dd date.");
            date = parsed;
        }

        var entry = scheduleService.AddToSeries(series, title, draftFilename, week, date, tags, notes);
        return entry is null
            ? ToolResponse<AddToSeriesResult>.Fail($"'{draftFilename}' is already on the schedule.")
            : ToolResponse<AddToSeriesResult>.Ok(new AddToSeriesResult(entry.Series, entry.Position, entry.DraftFilename));
    }
}
