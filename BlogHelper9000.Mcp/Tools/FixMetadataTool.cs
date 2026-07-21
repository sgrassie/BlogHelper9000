using BlogHelper9000.Core.Services;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace BlogHelper9000.Mcp.Tools;

[McpServerToolType]
public static class FixMetadataTool
{
    [McpServerTool(Name = "fix_metadata", Title = "Batch-fix front matter", UseStructuredContent = true, ReadOnly = false, Destructive = true, Idempotent = true),
     Description("Rewrites front matter across ALL posts in _posts/. Can fix published status, descriptions, and tags. " +
                 "Call with dryRun=true first to preview the change before applying it; dryRun=false applies it.")]
    public static ToolResponse<FixMetadataToolResult> FixMetadata(
        IBlogService blogService,
        [Description("Fix the 'published' status on posts")] bool fixStatus = false,
        [Description("Fix empty descriptions")] bool fixDescription = false,
        [Description("Fix/normalize tags")] bool fixTags = false,
        [Description("Preview the change without writing any files. Defaults to true — set false to apply.")] bool dryRun = true)
    {
        return ToolGate.RunExclusive(() =>
        {
            try
            {
                var result = blogService.FixMetadata(fixStatus, fixDescription, fixTags, dryRun);
                var skipped = result.Skipped.Select(s => new FixMetadataSkipDto(s.FilePath, s.Reason)).ToList();
                return ToolResponse<FixMetadataToolResult>.Ok(new FixMetadataToolResult(result.Updated, skipped, dryRun));
            }
            catch (Exception ex)
            {
                return ToolResponse<FixMetadataToolResult>.Fail($"Failed to fix metadata: {ex.Message}");
            }
        });
    }
}
