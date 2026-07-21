using BlogHelper9000.Core.Services;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace BlogHelper9000.Mcp.Tools;

[McpServerToolType]
public static class ValidatePostTool
{
    // Kept in sync with the Check ids PostValidator emits (BlogHelper9000.Core.Services.PostValidator).
    private const string CheckIds =
        "Check ids: 'front-matter' (missing title/description/tags, or front matter that failed to parse), " +
        "'image-missing' (a referenced image — front matter or body — was not found on disk), " +
        "'internal-link' (a root-relative markdown link doesn't resolve to a known page/post slug), " +
        "'liquid-link' (a {% post_url %} or {% link %} tag targets something that doesn't exist), " +
        "'placeholder' (a TODO/FIXME/XXX/TBD/[placeholder marker, or a link that looks like an unfilled GitHub profile URL), " +
        "'validator-error' (an unexpected failure validating a file or enumerating a directory — rare). " +
        "'not-found' only ever surfaces via validate_post's failure envelope, when the post can't be resolved at all.";

    [McpServerTool(Name = "validate_post", Title = "Validate a post", UseStructuredContent = true, ReadOnly = true, Idempotent = true),
     Description("Runs static, offline checks over a single post/draft: front matter sanity, image existence, internal " +
                 "link resolution, and placeholder-content detection. No Jekyll build, no network access. " + CheckIds)]
    public static ToolResponse<ValidatePostResult> ValidatePost(
        IPostValidator validator,
        [Description("Filename (e.g. 'my-post.md') or path of the post/draft to validate. Bare filenames are resolved against _drafts/ then _posts/; paths outside the blog root are rejected.")] string postPath)
    {
        return ToolGate.RunExclusive(() =>
        {
            var report = validator.ValidatePost(postPath);

            if (report.Findings.Count == 1 && report.Findings[0].Check == "not-found")
            {
                return ToolResponse<ValidatePostResult>.Fail($"Could not find post '{postPath}'.");
            }

            return ToolResponse<ValidatePostResult>.Ok(ToResult(report, report.Findings));
        });
    }

    [McpServerTool(Name = "validate_blog", Title = "Validate the whole blog", UseStructuredContent = true, ReadOnly = true, Idempotent = true),
     Description("Runs the same static, offline checks as validate_post (see its description) over every draft and post " +
                 "in the blog. 'minimumSeverity' filters which findings are returned in 'Files' (Info|Warning|Error, " +
                 "case-insensitive, default Info = everything); a file is included in 'Files' only if it has at least " +
                 "one finding at or above that level. 'FilesChecked', 'Errors', and 'Warnings' are always corpus-wide " +
                 "totals computed before filtering — they describe the whole blog, not just what's returned in 'Files'. " +
                 CheckIds)]
    public static ToolResponse<ValidateBlogResult> ValidateBlog(
        IPostValidator validator,
        [Description("Minimum severity to include in Files: Info, Warning, or Error (case-insensitive). Defaults to Info (everything).")] string? minimumSeverity = null)
    {
        return ToolGate.RunExclusive(() =>
        {
            if (!TryParseSeverity(minimumSeverity, out var threshold, out var error))
            {
                return ToolResponse<ValidateBlogResult>.Fail(error!);
            }

            var reports = validator.ValidateBlog();

            var filesChecked = reports.Count;
            var errors = reports.Sum(r => r.Findings.Count(f => f.Severity == ValidationSeverity.Error));
            var warnings = reports.Sum(r => r.Findings.Count(f => f.Severity == ValidationSeverity.Warning));

            var files = reports
                .Select(r => ToResult(r, r.Findings.Where(f => f.Severity >= threshold).ToList()))
                .Where(r => r.Findings.Count > 0)
                .ToList();

            return ToolResponse<ValidateBlogResult>.Ok(new ValidateBlogResult(filesChecked, errors, warnings, files));
        });
    }

    private static bool TryParseSeverity(string? raw, out ValidationSeverity severity, out string? error)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            severity = ValidationSeverity.Info;
            error = null;
            return true;
        }

        // Enum.TryParse also accepts the underlying numeric value (e.g. "5"), silently
        // producing an undefined enum member — guard with IsDefined so that only the
        // three named severities are accepted.
        if (Enum.TryParse(raw, ignoreCase: true, out severity) && Enum.IsDefined(severity))
        {
            error = null;
            return true;
        }

        severity = default;
        error = $"Unknown severity '{raw}'. Valid values: Info, Warning, Error.";
        return false;
    }

    private static ValidatePostResult ToResult(ValidationReport report, IReadOnlyList<ValidationFinding> findings) =>
        new(report.FilePath, report.IsValid, findings.Select(ToDto).ToList());

    private static ValidationFindingDto ToDto(ValidationFinding finding) =>
        new(finding.Severity.ToString(), finding.Check, finding.Message, finding.Line);
}
