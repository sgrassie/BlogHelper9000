using BlogHelper9000.Core.Helpers;
using BlogHelper9000.Core.YamlParsing;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Reflection;

namespace BlogHelper9000.Mcp.Tools;

[McpServerToolType]
public static class UpdatePostTool
{
    // Reserved extras keys injected by PostManager.LoadYamlHeaderForAllPosts — never caller-writable.
    private static readonly HashSet<string> ReservedExtrasKeys =
        new(StringComparer.OrdinalIgnoreCase) { "originalFilename", "lastUpdated" };

    // Front matter keys already owned by a dedicated YamlHeader property (or its [YamlName] alias) —
    // derived by reflection so this list can't drift from the real model.
    private static readonly HashSet<string> KnownFrontMatterKeys = BuildKnownFrontMatterKeys();

    private static HashSet<string> BuildKnownFrontMatterKeys()
    {
        return typeof(YamlHeader)
            .GetProperties(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetCustomAttribute<YamlIgnoreAttribute>() is null)
            .Select(p => p.GetCustomAttribute<YamlNameAttribute>()?.Name ?? p.Name)
            .Select(name => name.ToLowerInvariant())
            .ToHashSet();
    }

    [McpServerTool(Name = "update_post", Title = "Update a post", UseStructuredContent = true, ReadOnly = false, Destructive = true, Idempotent = true),
     Description("Updates a post or draft's front matter and/or body. Only supplied fields are changed. " +
                 "body REPLACES the entire post body — call get_post first to read the current content before overwriting it.")]
    public static ToolResponse<UpdatePostResult> UpdatePost(
        PostManager postManager,
        [Description("Filename (e.g. 'my-post.md') or path of the post/draft to update. Bare filenames are resolved against _drafts/ then _posts/; paths outside the blog root are rejected.")] string postPath,
        [Description("New title")] string? title = null,
        [Description("New description")] string? description = null,
        [Description("New comma-separated tags, e.g. 'csharp, dotnet' — replaces all existing tags")] string? tags = null,
        [Description("New markdown body — replaces the entire existing body")] string? body = null,
        [Description("Set or clear the featured flag")] bool? featured = null,
        [Description("Set or clear the hidden flag")] bool? hidden = null,
        [Description("Corrects the post's publish date, yyyy-MM-dd. This does NOT flip publish status — it's for " +
                     "fixing the date on a post that is already published.")] string? publishedOn = null,
        [Description("Newline-separated 'key: value' lines merged into arbitrary front matter fields not covered " +
                     "by a dedicated parameter. An empty value (e.g. 'my_key:') removes that key. Keys colliding " +
                     "with a dedicated field (layout, title, description, tags, featured_image, image, " +
                     "featured_image_thumbnail, featured, hidden, published, ispublished, series) or with the " +
                     "internally-managed originalFilename/lastUpdated keys are rejected.")] string? extraFrontMatter = null)
    {
        if (!postManager.TryFindPost(postPath, out var markdownFile))
        {
            return ToolResponse<UpdatePostResult>.Fail($"Could not find post '{postPath}'.");
        }

        var updatedFields = new List<string>();

        if (title is not null)
        {
            markdownFile.Metadata.Title = title;
            updatedFields.Add("title");
        }

        if (description is not null)
        {
            markdownFile.Metadata.Description = description;
            updatedFields.Add("description");
        }

        if (tags is not null)
        {
            markdownFile.Metadata.Tags = tags
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .ToList();
            updatedFields.Add("tags");
        }

        if (featured is not null)
        {
            markdownFile.Metadata.IsFeatured = featured;
            updatedFields.Add("featured");
        }

        if (hidden is not null)
        {
            markdownFile.Metadata.IsHidden = hidden;
            updatedFields.Add("hidden");
        }

        if (publishedOn is not null)
        {
            if (!DateOnly.TryParse(publishedOn, out var parsedDate))
            {
                return ToolResponse<UpdatePostResult>.Fail($"'{publishedOn}' is not a valid yyyy-MM-dd date.");
            }

            markdownFile.Metadata.PublishedOn = parsedDate.ToDateTime(TimeOnly.MinValue);
            updatedFields.Add("publishedOn");
        }

        if (extraFrontMatter is not null)
        {
            var applyExtrasError = ApplyExtraFrontMatter(markdownFile, extraFrontMatter, updatedFields);
            if (applyExtrasError is not null)
            {
                return ToolResponse<UpdatePostResult>.Fail(applyExtrasError);
            }
        }

        try
        {
            if (body is not null)
            {
                postManager.Markdown.UpdateFile(markdownFile, body);
                updatedFields.Add("body");
            }
            else if (updatedFields.Count > 0)
            {
                postManager.UpdateMarkdown(markdownFile);
            }
        }
        catch (Exception ex)
        {
            return ToolResponse<UpdatePostResult>.Fail($"Could not update '{postPath}': {ex.Message}");
        }

        return ToolResponse<UpdatePostResult>.Ok(new UpdatePostResult(markdownFile.FilePath, updatedFields));
    }

    /// <summary>
    /// Parses and validates every line up front — if any line is malformed or rejected, nothing is applied
    /// to <paramref name="markdownFile"/>'s metadata (and nothing is ever written to disk, since the caller
    /// only persists after this returns null).
    /// </summary>
    /// <returns>An error message if validation failed; otherwise null.</returns>
    private static string? ApplyExtraFrontMatter(MarkdownFile markdownFile, string extraFrontMatter, List<string> updatedFields)
    {
        var edits = new List<(string Key, string Value)>();

        foreach (var rawLine in extraFrontMatter.Replace("\r\n", "\n").Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0) continue;

            var colonIndex = line.IndexOf(':');
            if (colonIndex <= 0)
            {
                return $"Malformed extraFrontMatter line '{rawLine}' — expected 'key: value'.";
            }

            var key = line[..colonIndex].Trim();
            var value = line[(colonIndex + 1)..].Trim();

            if (key.Length == 0)
            {
                return $"Malformed extraFrontMatter line '{rawLine}' — expected 'key: value'.";
            }

            if (KnownFrontMatterKeys.Contains(key.ToLowerInvariant()))
            {
                return $"'{key}' is a dedicated field — use the corresponding update_post parameter instead of extraFrontMatter.";
            }

            if (ReservedExtrasKeys.Contains(key))
            {
                return $"'{key}' is managed internally and cannot be set via extraFrontMatter.";
            }

            edits.Add((key, value));
        }

        foreach (var (key, value) in edits)
        {
            if (value.Length == 0)
            {
                markdownFile.Metadata.Extras.Remove(key);
            }
            else
            {
                markdownFile.Metadata.Extras[key] = value;
            }

            updatedFields.Add($"extra:{key}");
        }

        return null;
    }
}
