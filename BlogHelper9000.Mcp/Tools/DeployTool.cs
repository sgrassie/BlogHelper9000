using BlogHelper9000.Core.Models;
using BlogHelper9000.Core.Services;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace BlogHelper9000.Mcp.Tools;

[McpServerToolType]
public static class DeployTool
{
    [McpServerTool(Name = "deploy", Title = "Deploy the blog", UseStructuredContent = true, ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = true),
     Description("Stages ONLY publish-related paths (_posts/, _drafts/, assets/images/), commits them, and pushes to " +
                 "the configured upstream. Defaults to dryRun=true — set false to actually commit and push. Run " +
                 "dryRun=true first to preview exactly what would be committed and pushed.")]
    public static ToolResponse<DeployToolResult> Deploy(
        IDeployStateService deployService,
        [Description("Defaults to true — set false to actually commit and push.")] bool dryRun = true,
        [Description("Optional commit message override. When omitted, a message is generated from the staged post filenames.")] string? message = null)
    {
        return ToolGate.RunExclusive(() =>
        {
            var result = deployService.Deploy(dryRun, message);

            switch (result.Outcome)
            {
                case DeployOutcome.NotARepo:
                    return ToolResponse<DeployToolResult>.Fail("The blog directory is not a git repository.");
                case DeployOutcome.NoUpstream:
                    return ToolResponse<DeployToolResult>.Fail(
                        "No upstream is configured for the blog repository; set one up (e.g. 'git push -u origin <branch>') before deploying.");
                case DeployOutcome.GitFailed:
                    return ToolResponse<DeployToolResult>.Fail($"A git command failed: {result.Error}");
                case DeployOutcome.Deployed:
                case DeployOutcome.NothingToDeploy:
                    break;
                default:
                    return ToolResponse<DeployToolResult>.Fail("Unknown deploy outcome.");
            }

            return ToolResponse<DeployToolResult>.Ok(new DeployToolResult(
                result.Outcome.ToString(), result.DryRun, result.StagedFiles, result.CommitMessage, result.Pushed));
        });
    }
}
