using BlogHelper9000.Core.Scheduling;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace BlogHelper9000.Mcp.Tools;

[McpServerToolType]
public static class GetDuePostsTool
{
    [McpServerTool(Name = "get_due_posts", Title = "List due and overdue posts", UseStructuredContent = true, ReadOnly = true, Idempotent = true),
     Description("Lists unpublished schedule entries whose resolved due date (explicit publish date, or cadence " +
                 "projected from week number) falls on or before the given date, ordered by due date then series " +
                 "then position. An empty list means nothing is due — that's a valid answer, not a failure.")]
    public static ToolResponse<GetDuePostsResult> GetDuePosts(
        IScheduleService scheduleService,
        [Description("Evaluate due-ness as of this date, yyyy-MM-dd. Defaults to today.")] string? asOf = null)
    {
        if (!scheduleService.DatabaseExists)
            return ToolResponse<GetDuePostsResult>.Fail(ListSeriesTool.NoDatabaseError);

        DateOnly? date = null;
        if (asOf is not null)
        {
            if (!DateOnly.TryParse(asOf, out var parsed))
                return ToolResponse<GetDuePostsResult>.Fail($"'{asOf}' is not a valid yyyy-MM-dd date.");
            date = parsed;
        }

        var due = scheduleService.GetDuePosts(date).Select(d => new DuePostDto(
            d.Entry.Series, d.Entry.Title, d.Entry.DraftFilename, d.DueDate.ToString("yyyy-MM-dd"),
            d.Overdue, d.DaysOverdue, d.Entry.Week, d.Entry.Position)).ToList();

        return ToolResponse<GetDuePostsResult>.Ok(new GetDuePostsResult(due));
    }
}
