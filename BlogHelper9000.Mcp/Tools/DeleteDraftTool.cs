using BlogHelper9000.Core.Models;
using BlogHelper9000.Core.Scheduling;
using BlogHelper9000.Core.Services;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace BlogHelper9000.Mcp.Tools;

[McpServerToolType]
public static class DeleteDraftTool
{
    [McpServerTool(Name = "delete_draft", Title = "Delete a draft", UseStructuredContent = true, ReadOnly = false, Destructive = true, Idempotent = false),
     Description("Permanently deletes a draft file from _drafts/. Only drafts can be deleted; published posts must " +
                 "be unpublished first. If the draft has a publishing-schedule entry, it is removed too. Defaults " +
                 "to a dry run that reports what would happen without deleting anything.")]
    public static ToolResponse<DeleteDraftToolResult> DeleteDraft(
        IBlogService blogService,
        IScheduleService scheduleService,
        [Description("Filename (e.g. 'my-draft.md') or path of the draft to delete. Bare filenames are resolved against _drafts/; paths outside the blog root are rejected.")] string postPath,
        [Description("Preview the deletion without changing anything. Defaults to true — set false to actually delete.")] bool dryRun = true)
    {
        var result = blogService.DeleteDraft(postPath, dryRun);

        switch (result.Outcome)
        {
            case DeleteDraftOutcome.NotFound:
                return ToolResponse<DeleteDraftToolResult>.Fail($"Could not find draft '{postPath}'.");
            case DeleteDraftOutcome.NotADraft:
                return ToolResponse<DeleteDraftToolResult>.Fail(
                    $"'{postPath}' is not a draft. Only drafts can be deleted; published posts must be unpublished first (see unpublish_post).");
            case DeleteDraftOutcome.Deleted:
                break;
            default:
                return ToolResponse<DeleteDraftToolResult>.Fail("Unknown delete-draft outcome.");
        }

        string? scheduleEntry = null;
        if (scheduleService.DatabaseExists)
        {
            var entry = scheduleService.FindEntry(postPath);
            if (entry is not null)
            {
                scheduleEntry = $"Series '{entry.Series}' position {entry.Position}";
                if (!dryRun)
                    scheduleService.RemoveEntry(postPath);
            }
        }

        return ToolResponse<DeleteDraftToolResult>.Ok(
            new DeleteDraftToolResult(result.FilePath!, dryRun, Deleted: !dryRun, scheduleEntry));
    }
}
