using System.IO.Abstractions;
using System.Text.RegularExpressions;
using BlogHelper9000.Core.Helpers;
using BlogHelper9000.Core.YamlParsing;

namespace BlogHelper9000.Core.Services;

/// <summary>
/// Static, offline checks over a post/draft's front matter, images, links, and body text.
/// No Jekyll build, no network access — see <see cref="IPostValidator"/>.
/// </summary>
public sealed class PostValidator : IPostValidator
{
    // Deliberate limitation: both regexes stop at the first ')', so a target containing a
    // literal parenthesis (some Wikipedia-style URLs, etc.) gets truncated rather than
    // matched in full. Markdown itself allows escaping/angle-bracket forms for this case;
    // we don't attempt to parse those — false negatives on that edge case are accepted
    // rather than reaching for a full markdown parser here.
    private static readonly Regex ImageRegex =
        new(@"!\[[^\]]*\]\(\s*(\S*?)(?:\s+""[^""]*"")?\s*\)", RegexOptions.Compiled);

    private static readonly Regex LinkRegex =
        new(@"(?<!!)\[[^\]]*\]\(\s*([^)\s]*)(?:\s+""[^""]*"")?\s*\)", RegexOptions.Compiled);

    private static readonly Regex GithubProfileRegex =
        new(@"^https?://github\.com/[^/]+/?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex PostUrlTagRegex =
        new(@"\{%-?\s*post_url\s+([^\s%}]+)\s*-?%\}", RegexOptions.Compiled);

    private static readonly Regex LinkTagRegex =
        new(@"\{%-?\s*link\s+([^\s%}]+)\s*-?%\}", RegexOptions.Compiled);

    private static readonly Regex DatePrefixRegex =
        new(@"^\d{4}-\d{2}-\d{2}-", RegexOptions.Compiled);

    private readonly PostManager _postManager;
    private readonly IFileSystem _fileSystem;

    public PostValidator(PostManager postManager)
    {
        _postManager = postManager;
        _fileSystem = postManager.FileSystem;
    }

    public ValidationReport ValidatePost(string postPath)
    {
        MarkdownFile? markdownFile;
        try
        {
            if (!_postManager.TryFindPost(postPath, out markdownFile))
            {
                return NotFoundReport(postPath);
            }
        }
        catch (Exception ex)
        {
            // TryFindPost eagerly parses front matter as part of resolving the path, so a
            // malformed post surfaces here as an exception rather than a false return —
            // fold it into the same front-matter finding ValidateBlog produces per file,
            // instead of letting it escape (ValidatePost must never throw).
            return new ValidationReport(postPath, [FrontMatterParseFinding(ex)]);
        }

        var slugIndex = BuildSlugIndex();
        return ValidateFile(markdownFile.FilePath, slugIndex);
    }

    public IReadOnlyList<ValidationReport> ValidateBlog()
    {
        var reports = new List<ValidationReport>();

        // BuildSlugIndex enumerates the same directories as the file collection below and
        // is guarded the same way (see BuildSlugIndex) — call it first so a directory-level
        // failure there degrades the index rather than aborting the whole sweep.
        var slugIndex = BuildSlugIndex();

        var files = new List<string>();
        files.AddRange(CollectFiles(_postManager.Drafts, reports));
        files.AddRange(CollectFiles(_postManager.Posts, reports));

        foreach (var path in files)
        {
            reports.Add(ValidateFile(path, slugIndex));
        }

        return reports;
    }

    private static ValidationReport NotFoundReport(string postPath) =>
        new(postPath, [new ValidationFinding(ValidationSeverity.Error, "not-found", "post not found", null)]);

    private static ValidationFinding FrontMatterParseFinding(Exception ex) =>
        new(ValidationSeverity.Error, "front-matter", $"front matter does not parse: {ex.Message}", null);

    private static ValidationFinding ValidatorErrorFinding(string message) =>
        new(ValidationSeverity.Error, "validator-error", message, null);

    /// <summary>
    /// Enumerates the .md files under <paramref name="directory"/>. A directory-level IO
    /// failure (permissions, concurrent delete) must not abort the whole sweep — it's
    /// recorded as its own finding (Check="validator-error", FilePath=the directory) and
    /// the other directory (drafts/posts) still gets validated.
    /// </summary>
    private List<string> CollectFiles(string directory, List<ValidationReport> reports)
    {
        try
        {
            if (!_fileSystem.Directory.Exists(directory))
                return [];

            return _fileSystem.Directory.EnumerateFiles(directory, "*.md", SearchOption.AllDirectories).ToList();
        }
        catch (Exception ex)
        {
            reports.Add(new ValidationReport(directory, [ValidatorErrorFinding($"could not enumerate files: {ex.Message}")]));
            return [];
        }
    }

    private ValidationReport ValidateFile(string path, SlugIndex slugIndex)
    {
        MarkdownFile markdownFile;
        try
        {
            markdownFile = _postManager.Markdown.LoadFile(path);
        }
        catch (Exception ex)
        {
            // Front matter that doesn't parse short-circuits every other check for this
            // file — the body is still readable but the brief scopes this to a single
            // finding rather than guessing at a broken file's intent.
            return new ValidationReport(path, [FrontMatterParseFinding(ex)]);
        }

        try
        {
            var findings = new List<ValidationFinding>();
            var bodyLines = ExtractBodyLines(_fileSystem.File.ReadAllLines(path));

            ValidateFrontMatter(markdownFile.Metadata, findings);
            ValidateImages(markdownFile.Metadata, bodyLines, findings);
            ValidateLinksAndPlaceholders(bodyLines, slugIndex, findings);
            ValidatePlaceholderMarkers(bodyLines, findings);

            return new ValidationReport(path, findings);
        }
        catch (Exception ex)
        {
            // Belt-and-braces: nothing above should throw once front matter parses, but a
            // sweep across an entire blog must never die on one file (Phase-1 precedent).
            // Distinct Check id from the LoadFile catch above — this is an unexpected
            // failure in a later check (image/link/placeholder), not a front-matter parse
            // problem, so it shouldn't be mislabelled as one.
            return new ValidationReport(path, [ValidatorErrorFinding($"unexpected error validating file: {ex.Message}")]);
        }
    }

    // Deliberate limitation: this returns every line after the front-matter delimiters,
    // including fenced code blocks (```...```). Image/link/placeholder checks therefore run
    // inside code samples too — a code snippet showing `![alt](/missing.png)` as example
    // markdown, or a comment containing "TODO", can produce a false-positive finding. We
    // don't track fence state here; keeping the line-scan simple was judged worth the
    // occasional false positive over a fuller markdown-aware pass.
    private static List<(int LineNumber, string Text)> ExtractBodyLines(string[] lines)
    {
        var body = new List<(int, string)>();
        var delimiterCount = 0;

        for (var i = 0; i < lines.Length; i++)
        {
            if (delimiterCount < 2)
            {
                if (lines[i].Trim() == "---")
                    delimiterCount++;
                continue;
            }

            body.Add((i + 1, lines[i]));
        }

        return body;
    }

    private static void ValidateFrontMatter(YamlHeader header, List<ValidationFinding> findings)
    {
        if (string.IsNullOrWhiteSpace(header.Title))
            findings.Add(new ValidationFinding(ValidationSeverity.Error, "front-matter", "title is missing", null));

        if (string.IsNullOrWhiteSpace(header.Description))
            findings.Add(new ValidationFinding(ValidationSeverity.Warning, "front-matter", "description is missing", null));

        if (header.Tags is null || header.Tags.Count == 0)
            findings.Add(new ValidationFinding(ValidationSeverity.Warning, "front-matter", "tags are empty", null));
    }

    private void ValidateImages(YamlHeader header, List<(int LineNumber, string Text)> bodyLines, List<ValidationFinding> findings)
    {
        CheckImageTarget(header.FeaturedImage, null, findings);
        CheckImageTarget(header.Image, null, findings);

        foreach (var (lineNumber, text) in bodyLines)
        {
            foreach (Match match in ImageRegex.Matches(text))
            {
                CheckImageTarget(match.Groups[1].Value.Trim(), lineNumber, findings);
            }
        }
    }

    private void CheckImageTarget(string? target, int? line, List<ValidationFinding> findings)
    {
        if (string.IsNullOrWhiteSpace(target))
            return;

        if (IsHttpUrl(target))
            return;

        if (target.StartsWith('/'))
        {
            var relative = target.TrimStart('/');
            var fullPath = _fileSystem.Path.Combine(_postManager.BasePath, relative);
            if (!_fileSystem.File.Exists(fullPath))
                findings.Add(new ValidationFinding(ValidationSeverity.Error, "image-missing", $"image not found: {target}", line));
            return;
        }

        findings.Add(new ValidationFinding(ValidationSeverity.Info, "image-missing", $"relative image path — cannot verify: {target}", line));
    }

    private void ValidateLinksAndPlaceholders(
        List<(int LineNumber, string Text)> bodyLines, SlugIndex slugIndex, List<ValidationFinding> findings)
    {
        foreach (var (lineNumber, text) in bodyLines)
        {
            foreach (Match match in LinkRegex.Matches(text))
            {
                var target = match.Groups[1].Value.Trim();

                if (target.Length == 0)
                {
                    findings.Add(new ValidationFinding(ValidationSeverity.Error, "placeholder", "empty link target", lineNumber));
                    continue;
                }

                if (target.StartsWith('/') && target != "/")
                {
                    if (!ResolvesInSlugIndex(target, slugIndex.Slugs))
                        findings.Add(new ValidationFinding(ValidationSeverity.Warning, "internal-link", $"internal link may not resolve: {target}", lineNumber));
                    continue;
                }

                if (GithubProfileRegex.IsMatch(target))
                {
                    findings.Add(new ValidationFinding(ValidationSeverity.Warning, "placeholder", $"looks like a placeholder GitHub profile link: {target}", lineNumber));
                }
            }

            foreach (Match match in PostUrlTagRegex.Matches(text))
            {
                var name = match.Groups[1].Value.Trim();
                if (!slugIndex.PostFileNames.Contains(name.ToLowerInvariant()))
                    findings.Add(new ValidationFinding(ValidationSeverity.Error, "liquid-link", $"post_url target not found: {name}", lineNumber));
            }

            foreach (Match match in LinkTagRegex.Matches(text))
            {
                var linkPath = match.Groups[1].Value.Trim();
                var fullPath = _fileSystem.Path.Combine(_postManager.BasePath, linkPath);
                if (!_fileSystem.File.Exists(fullPath))
                    findings.Add(new ValidationFinding(ValidationSeverity.Error, "liquid-link", $"link target not found: {linkPath}", lineNumber));
            }
        }
    }

    private static void ValidatePlaceholderMarkers(List<(int LineNumber, string Text)> bodyLines, List<ValidationFinding> findings)
    {
        var body = string.Join("\n", bodyLines.Select(l => l.Text));
        var markers = ContentMarkers.FindMarkers(body);

        foreach (var marker in markers)
        {
            var line = FindMarkerLine(bodyLines, marker);
            findings.Add(new ValidationFinding(ValidationSeverity.Warning, "placeholder", $"possible placeholder marker found: {marker}", line));
        }
    }

    private static int? FindMarkerLine(List<(int LineNumber, string Text)> bodyLines, string marker)
    {
        foreach (var (lineNumber, text) in bodyLines)
        {
            var isHit = marker == "[placeholder"
                ? text.Contains(marker, StringComparison.OrdinalIgnoreCase)
                : Regex.IsMatch(text, $@"\b{Regex.Escape(marker)}\b");

            if (isHit)
                return lineNumber;
        }

        return null;
    }

    private static bool ResolvesInSlugIndex(string target, HashSet<string> slugs)
    {
        var path = target;
        var cut = path.IndexOfAny(['?', '#']);
        if (cut >= 0)
            path = path[..cut];

        path = path.Trim('/').ToLowerInvariant();
        if (path.Length == 0)
            return true;

        var lastSegment = path.Contains('/') ? path[(path.LastIndexOf('/') + 1)..] : path;
        return slugs.Contains(lastSegment) || slugs.Any(slug => path.EndsWith(slug, StringComparison.Ordinal));
    }

    private static bool IsHttpUrl(string target) =>
        target.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
        target.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Builds the slug index, degrading to an empty index on any IO failure (e.g. a
    /// directory-level error enumerating _posts) rather than throwing. An empty index is a
    /// conservative failure mode: every root-relative internal-link check then reports
    /// "unresolved" (Warning) and every {% post_url %} check reports "not found" (Error) —
    /// false positives instead of an escaping exception, which fits ValidateBlog/ValidatePost's
    /// "never throws" contract.
    /// </summary>
    private SlugIndex BuildSlugIndex()
    {
        try
        {
            return BuildSlugIndexCore();
        }
        catch (Exception)
        {
            return new SlugIndex([], []);
        }
    }

    private SlugIndex BuildSlugIndexCore()
    {
        var slugs = new HashSet<string>();
        var postFileNames = new HashSet<string>();

        if (_fileSystem.Directory.Exists(_postManager.Posts))
        {
            foreach (var path in _fileSystem.Directory.EnumerateFiles(_postManager.Posts, "*.md", SearchOption.AllDirectories))
            {
                var nameNoExt = _fileSystem.Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                postFileNames.Add(nameNoExt);
                slugs.Add(DatePrefixRegex.Replace(nameNoExt, string.Empty));
            }
        }

        if (_fileSystem.Directory.Exists(_postManager.BasePath))
        {
            var pages = _fileSystem.Directory.EnumerateFiles(_postManager.BasePath, "*.md", SearchOption.TopDirectoryOnly)
                .Concat(_fileSystem.Directory.EnumerateFiles(_postManager.BasePath, "*.html", SearchOption.TopDirectoryOnly));

            foreach (var path in pages)
            {
                slugs.Add(_fileSystem.Path.GetFileNameWithoutExtension(path).ToLowerInvariant());
            }
        }

        return new SlugIndex(slugs, postFileNames);
    }

    private sealed record SlugIndex(HashSet<string> Slugs, HashSet<string> PostFileNames);
}
