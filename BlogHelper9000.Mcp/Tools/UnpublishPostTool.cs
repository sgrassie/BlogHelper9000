using BlogHelper9000.Core.Models;
using BlogHelper9000.Core.Scheduling;
using BlogHelper9000.Core.Services;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace BlogHelper9000.Mcp.Tools;

[McpServerToolType]
public static class UnpublishPostTool
{
    [McpServerTool(Name = "unpublish_post", Title = "Unpublish a post", UseStructuredContent = true, ReadOnly = false, Destructive = true, Idempotent = false),
     Description("Reverses publishing a post: clears the published metadata and moves it back to _drafts/ with its " +
                 "date prefix stripped. If the post has a publishing-schedule entry, it is un-ticked too; the result's " +
                 "scheduleOutcome reports Unmarked/NotScheduled. Defaults to a dry run that reports what would happen " +
                 "without changing anything.")]
    public static ToolResponse<UnpublishResult> UnpublishPost(
        IBlogService blogService,
        IScheduleService scheduleService,
        [Description("Filename (e.g. '2024-01-01-my-post.md') or path of the published post to unpublish. Bare filenames are resolved against _drafts/ then _posts/; paths outside the blog root are rejected.")] string postPath,
        [Description("Preview the unpublish without changing anything. Defaults to true — set false to actually unpublish.")] bool dryRun = true)
    {
        var result = blogService.UnpublishPostDetailed(postPath, dryRun);

        switch (result.Outcome)
        {
            case UnpublishOutcome.NotFound:
                return ToolResponse<UnpublishResult>.Fail($"Could not find published post '{postPath}'.");
            case UnpublishOutcome.NotPublished:
                return ToolResponse<UnpublishResult>.Fail(
                    $"'{postPath}' is not a published post (drafts cannot be unpublished).");
            case UnpublishOutcome.TargetExists:
                return ToolResponse<UnpublishResult>.Fail(
                    $"Cannot unpublish '{postPath}': a draft named '{result.DraftPath}' already exists.");
        }

        string? scheduleOutcome = null;
        if (scheduleService.DatabaseExists)
        {
            if (dryRun)
            {
                var entry = scheduleService.FindEntry(postPath);
                scheduleOutcome = entry is not null && entry.Published ? "WouldUnmark" : "NotScheduled";
            }
            else
            {
                var outcome = scheduleService.MarkPublished(postPath, unmark: true);
                scheduleOutcome = outcome switch
                {
                    MarkPublishedOutcome.Unmarked => "Unmarked",
                    MarkPublishedOutcome.NotScheduled => "NotScheduled",
                    _ => outcome.ToString()
                };
            }
        }

        return ToolResponse<UnpublishResult>.Ok(
            new UnpublishResult(result.DraftPath, result.Outcome.ToString(), scheduleOutcome, dryRun));
    }
}
