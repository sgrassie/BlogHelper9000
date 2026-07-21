using BlogHelper9000.Core.Scheduling;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace BlogHelper9000.Mcp.Tools;

[McpServerToolType]
public static class RemoveScheduleEntryTool
{
    internal const string NotScheduledMessage =
        "'{0}' is not on any schedule. Call get_series to inspect the schedules.";

    [McpServerTool(Name = "remove_schedule_entry", Title = "Remove a schedule entry", UseStructuredContent = true, ReadOnly = false, Destructive = true, Idempotent = true),
     Description("Removes a post from its schedule series and densely renumbers the remaining entries. " +
                 "Defaults to a dry run that reports what would happen without changing anything.")]
    public static ToolResponse<RemoveScheduleEntryResult> RemoveScheduleEntry(
        IScheduleService scheduleService,
        [Description("The post's draft filename, e.g. 'my-post.md'. Date prefixes and paths are tolerated.")] string post,
        [Description("Preview the removal without changing anything. Defaults to true — set false to apply.")] bool dryRun = true)
    {
        if (!scheduleService.DatabaseExists)
            return ToolResponse<RemoveScheduleEntryResult>.Fail(ListSeriesTool.NoDatabaseError);

        var entry = scheduleService.FindEntry(post);
        if (entry is null)
            return ToolResponse<RemoveScheduleEntryResult>.Fail(string.Format(NotScheduledMessage, post));

        if (dryRun)
            return ToolResponse<RemoveScheduleEntryResult>.Ok(
                new RemoveScheduleEntryResult(entry.DraftFilename, entry.Series, entry.Position, true, false));

        var outcome = scheduleService.RemoveEntry(post);
        return outcome == RemoveEntryOutcome.Removed
            ? ToolResponse<RemoveScheduleEntryResult>.Ok(
                new RemoveScheduleEntryResult(entry.DraftFilename, entry.Series, entry.Position, false, true))
            : ToolResponse<RemoveScheduleEntryResult>.Fail(string.Format(NotScheduledMessage, post));
    }
}
