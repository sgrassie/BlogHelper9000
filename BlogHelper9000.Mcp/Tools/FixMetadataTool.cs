using BlogHelper9000.Core.Services;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace BlogHelper9000.Mcp.Tools;

[McpServerToolType]
public static class FixMetadataTool
{
    [McpServerTool(Name = "fix_metadata"), Description("Batch-fixes YAML front matter metadata across all posts. Can fix published status, descriptions, and tags.")]
    public static string FixMetadata(
        IBlogService blogService,
        [Description("Fix the 'published' status on posts")] bool fixStatus = false,
        [Description("Fix empty descriptions")] bool fixDescription = false,
        [Description("Fix/normalize tags")] bool fixTags = false)
    {
        blogService.FixMetadata(fixStatus, fixDescription, fixTags);
        return "Metadata fix completed successfully.";
    }
}
