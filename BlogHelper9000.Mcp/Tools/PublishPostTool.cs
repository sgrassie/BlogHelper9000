using BlogHelper9000.Core.Models;
using BlogHelper9000.Core.Scheduling;
using BlogHelper9000.Core.Services;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace BlogHelper9000.Mcp.Tools;

[McpServerToolType]
public static class PublishPostTool
{
    [McpServerTool(Name = "publish_post", Title = "Publish a draft", UseStructuredContent = true, ReadOnly = false, Destructive = false, Idempotent = false),
     Description("Publishes a draft post, moving it from _drafts/ to _posts/<year>/ with a date prefix. " +
                 "On success the matching publishing-schedule entry (if any) is ticked automatically; the result's " +
                 "scheduleOutcome reports Marked/AlreadyMarked/NotScheduled. " +
                 "Fails distinctly when the post is not found, already published, or a same-day target already exists.")]
    public static ToolResponse<PublishResult> PublishPost(
        IBlogService blogService,
        IScheduleService scheduleService,
        [Description("Filename (e.g. 'my-draft.md') or path of the draft to publish. Bare filenames are resolved against _drafts/ then _posts/; paths outside the blog root are rejected.")] string postName)
    {
        var result = blogService.PublishPostDetailed(postName);

        return result.Outcome switch
        {
            PublishOutcome.Published => ToolResponse<PublishResult>.Ok(new PublishResult(
                result.PublishedPath, scheduleService.MarkPublished(postName).ToString())),
            PublishOutcome.NotFound => ToolResponse<PublishResult>.Fail($"No draft named '{postName}' was found in _drafts/ or _posts/."),
            PublishOutcome.AlreadyPublished => ToolResponse<PublishResult>.Fail($"'{postName}' already has a date prefix and appears to be published. Publishing is idempotent-safe: no action was taken."),
            PublishOutcome.TargetExists => ToolResponse<PublishResult>.Fail("A published post already exists at the target path for today's date."),
            _ => ToolResponse<PublishResult>.Fail("Unknown publish outcome.")
        };
    }
}
