using BlogHelper9000.Core.Models;
using BlogHelper9000.Core.Services;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace BlogHelper9000.Mcp.Tools;

[McpServerToolType]
public static class GetPublishStatusTool
{
    [McpServerTool(Name = "get_publish_status", Title = "Get publish status", UseStructuredContent = true, ReadOnly = true, Idempotent = true),
     Description("Reports the blog repo's git state: uncommitted changes, unpushed commits, whether an upstream is " +
                 "configured, and which pending files touch publish-related paths (_posts/, _drafts/, assets/images/). " +
                 "Read-only status check — use deploy to act on what it reports. If the blog directory isn't a git " +
                 "repository at all, this still succeeds and says so in Summary rather than failing.")]
    public static ToolResponse<PublishStatusResult> GetPublishStatus(IDeployStateService deployService)
    {
        return ToolGate.RunExclusive(() =>
        {
            var state = deployService.GetDeployState();

            return ToolResponse<PublishStatusResult>.Ok(new PublishStatusResult(
                state.IsGitRepo,
                state.HasUpstream,
                state.UncommittedFiles,
                state.UnpushedCommits,
                state.UnpushedFiles,
                state.PendingPostFiles,
                BuildSummary(state)));
        });
    }

    private static string BuildSummary(DeployState state)
    {
        if (!state.IsGitRepo)
            return "The blog directory is not a git repository.";

        var isClean = state.UncommittedFiles.Count == 0 && state.UnpushedCommits == 0;

        if (isClean && state.HasUpstream)
            return "Everything is committed and pushed — the blog is fully deployed.";

        var parts = new List<string>();
        if (state.UncommittedFiles.Count > 0)
            parts.Add($"{state.UncommittedFiles.Count} uncommitted change(s)");
        if (state.UnpushedCommits > 0)
            parts.Add($"{state.UnpushedCommits} unpushed commit(s)");

        var summary = parts.Count > 0 ? string.Join(" and ", parts) : "No uncommitted changes";

        summary += state.PendingPostFiles.Count > 0
            ? $"; {state.PendingPostFiles.Count} publish-related file(s) pending deploy."
            : ".";

        if (!state.HasUpstream)
            summary += " No upstream is configured.";

        return summary;
    }
}
