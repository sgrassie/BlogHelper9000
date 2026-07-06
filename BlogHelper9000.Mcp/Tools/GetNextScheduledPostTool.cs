using BlogHelper9000.Core.Scheduling;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace BlogHelper9000.Mcp.Tools;

[McpServerToolType]
public static class GetNextScheduledPostTool
{
    [McpServerTool(Name = "get_next_scheduled_post", Title = "Next post to publish", UseStructuredContent = true, ReadOnly = true, Idempotent = true),
     Description("Returns the next unpublished schedule entry: within a series (first unticked by position), or across " +
                 "all series (earliest planned date first). Use this to decide what to publish next, then publish_post.")]
    public static ToolResponse<NextScheduledResult> GetNextScheduledPost(
        IScheduleService scheduleService,
        [Description("Optional series name to look in; omit to search all series.")] string? series = null)
    {
        if (!scheduleService.DatabaseExists)
            return ToolResponse<NextScheduledResult>.Fail(ListSeriesTool.NoDatabaseError);

        var entry = scheduleService.GetNextUnpublished(series);
        return entry is null
            ? ToolResponse<NextScheduledResult>.Fail(series is null
                ? "Every scheduled post is published. Congratulations."
                : $"No unpublished entries in '{series}' (or the series does not exist).")
            : ToolResponse<NextScheduledResult>.Ok(new NextScheduledResult(
                entry.Series, entry.Title, entry.DraftFilename, entry.Week, entry.PublishDate));
    }
}
