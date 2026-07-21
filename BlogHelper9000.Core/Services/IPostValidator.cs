namespace BlogHelper9000.Core.Services;

/// <summary>
/// Static, offline validation for a single post/draft or the whole blog: front matter
/// sanity, image existence, internal link resolution, and placeholder-content detection.
/// No Jekyll build, no network access.
/// </summary>
public interface IPostValidator
{
    /// <summary>
    /// Validates a single draft/post. Never throws — an unresolvable post yields a report
    /// containing a single Error finding rather than an exception.
    /// </summary>
    ValidationReport ValidatePost(string postPath);

    /// <summary>
    /// Validates every draft and post in the blog. Never throws — a file whose front matter
    /// can't be parsed yields a single-finding report for that file rather than aborting the sweep.
    /// </summary>
    IReadOnlyList<ValidationReport> ValidateBlog();
}

public sealed record ValidationReport(string FilePath, IReadOnlyList<ValidationFinding> Findings)
{
    public bool IsValid => Findings.All(f => f.Severity != ValidationSeverity.Error);
}

/// <param name="Check">Stable kebab-case id, e.g. "front-matter", "image-missing", "internal-link", "liquid-link", "placeholder".</param>
/// <param name="Line">1-based line number in the full file (front matter included), or null when a line is meaningless.</param>
public sealed record ValidationFinding(ValidationSeverity Severity, string Check, string Message, int? Line);

public enum ValidationSeverity
{
    Info,
    Warning,
    Error
}
