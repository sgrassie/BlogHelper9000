using BlogHelper9000.Core.Scheduling;
using BlogHelper9000.Core.Services;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace BlogHelper9000.Mcp.Tools;

[McpServerToolType]
public static class ListDraftsTool
{
    [McpServerTool(Name = "list_drafts", Title = "List draft posts", UseStructuredContent = true, ReadOnly = true, Idempotent = true),
     Description("Lists draft blog posts from the _drafts/ directory with per-draft triage metadata " +
                  "(title, word count, last modified, schedule series/slot, and readiness flags like TODO/FIXME " +
                  "markers), most recently modified first. Returns an empty list, not an error, when there are " +
                  "no drafts, and leaves Series/ScheduleSlot null when no schedule database exists or the draft " +
                  "isn't scheduled. Use 'limit' to cap the number returned; 'Total' always reports the full count.")]
    public static ToolResponse<ListDraftsResult> ListDrafts(
        IBlogService blogService,
        IScheduleService scheduleService,
        [Description("Maximum number of drafts to return")] int limit = 50)
    {
        return ToolGate.RunExclusive(() =>
        {
            var allDrafts = blogService.GetDraftDetails();

            var mapped = allDrafts
                .Select(d =>
                {
                    var entry = scheduleService.FindEntry(d.FileName);
                    var slot = entry?.PublishDate is { } publishDate
                        ? publishDate.ToString("yyyy-MM-dd")
                        : entry?.Week is { } week ? $"Week {week}" : null;

                    return new DraftDetailDto(d.FileName, d.Title, d.WordCount, d.LastModified,
                        entry?.Series, slot, d.ReadinessFlags, d.HasFeaturedImage);
                })
                .Take(limit)
                .ToList();

            return ToolResponse<ListDraftsResult>.Ok(new ListDraftsResult(mapped, allDrafts.Count));
        });
    }
}
