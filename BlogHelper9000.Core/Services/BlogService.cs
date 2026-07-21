using System.Globalization;
using System.IO.Abstractions;
using System.Text.RegularExpressions;
using BlogHelper9000.Core.Helpers;
using BlogHelper9000.Core.Models;
using BlogHelper9000.Core.YamlParsing;
using Microsoft.Extensions.Logging;

namespace BlogHelper9000.Core.Services;

public class BlogService : IBlogService
{
    private readonly PostManager _postManager;
    private readonly IFileSystem _fileSystem;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<BlogService> _logger;

    public BlogService(PostManager postManager, IFileSystem fileSystem, TimeProvider timeProvider, ILogger<BlogService> logger)
    {
        _postManager = postManager;
        _fileSystem = fileSystem;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    private static readonly Regex AlreadyPublishedFileNamePattern = new(@"^\d{4}-\d{2}-\d{2}-", RegexOptions.Compiled);

    public string? AddPost(string title, bool isDraft, bool isFeatured = false, bool isHidden = false, string? featuredImage = null, IReadOnlyList<string>? tags = null, string? content = null)
    {
        var filePath = isDraft
            ? _postManager.CreateDraftPath(title)
            : _postManager.CreatePostPath(title);

        if (_fileSystem.File.Exists(filePath))
        {
            _logger.LogError("A post already exists at {File}", filePath);
            return null;
        }

        var yamlHeader = new YamlHeader
        {
            Title = title,
            Tags = tags?.ToList() ?? [],
            FeaturedImage = featuredImage ?? string.Empty,
            IsFeatured = isFeatured,
            IsHidden = isHidden,
            IsPublished = !isDraft
        };

        var yamlHeaderText = _postManager.YamlConvert.Serialise(yamlHeader);
        var fileText = string.IsNullOrEmpty(content)
            ? yamlHeaderText
            : $"{yamlHeaderText}{Environment.NewLine}{Environment.NewLine}{content}";
        _fileSystem.File.AppendAllText(filePath, fileText);

        _logger.LogInformation("Added new post at {File}", filePath);
        return filePath;
    }

    public string? PublishPost(string postName) =>
        PublishPostDetailed(postName) is { Outcome: PublishOutcome.Published, PublishedPath: { } path } ? path : null;

    public PublishPostResult PublishPostDetailed(string postName)
    {
        if (!_postManager.TryFindPost(postName, out var postMarkdown))
        {
            _logger.LogError("Could not find {Post} to publish", postName);
            return new PublishPostResult(PublishOutcome.NotFound, null);
        }

        var currentPath = postMarkdown.FilePath;
        var fileName = _fileSystem.Path.GetFileName(currentPath);

        if (AlreadyPublishedFileNamePattern.IsMatch(fileName))
        {
            _logger.LogWarning("{Post} already appears to be published", postName);
            return new PublishPostResult(PublishOutcome.AlreadyPublished, null);
        }

        var now = _timeProvider.GetLocalNow().DateTime;
        var publishedFilename = $"{now:yyyy-MM-dd}-{fileName}";
        var targetFolder = _fileSystem.Path.Combine(_postManager.Posts, $"{now:yyyy}");
        var replacementPath = _fileSystem.Path.Combine(targetFolder, publishedFilename);

        if (_fileSystem.File.Exists(replacementPath))
        {
            _logger.LogError("A published post already exists at {Target}", replacementPath);
            return new PublishPostResult(PublishOutcome.TargetExists, null);
        }

        postMarkdown.Metadata.IsPublished = true;
        postMarkdown.Metadata.PublishedOn = now;
        _postManager.Markdown.UpdateFile(postMarkdown);

        if (!_fileSystem.Directory.Exists(targetFolder))
        {
            _fileSystem.Directory.CreateDirectory(targetFolder);
        }

        _logger.LogInformation("Publishing {PublishedFileName} to {TargetFolder}", publishedFilename, targetFolder);
        _fileSystem.File.Move(currentPath, replacementPath);

        return new PublishPostResult(PublishOutcome.Published, replacementPath);
    }

    public UnpublishPostResult UnpublishPostDetailed(string postName, bool dryRun = false)
    {
        if (!_postManager.TryFindPost(postName, out var postMarkdown))
        {
            _logger.LogError("Could not find {Post} to unpublish", postName);
            return new UnpublishPostResult(UnpublishOutcome.NotFound, null);
        }

        var currentPath = postMarkdown.FilePath;
        var fileName = _fileSystem.Path.GetFileName(currentPath);

        if (!IsPathUnder(currentPath, _postManager.Posts) || !AlreadyPublishedFileNamePattern.IsMatch(fileName))
        {
            _logger.LogWarning("{Post} does not appear to be published", postName);
            return new UnpublishPostResult(UnpublishOutcome.NotPublished, null);
        }

        var draftFileName = AlreadyPublishedFileNamePattern.Replace(fileName, string.Empty);
        var targetPath = _fileSystem.Path.Combine(_postManager.Drafts, draftFileName);

        if (_fileSystem.File.Exists(targetPath))
        {
            _logger.LogError("A draft already exists at {Target}", targetPath);
            return new UnpublishPostResult(UnpublishOutcome.TargetExists, null);
        }

        if (dryRun)
        {
            return new UnpublishPostResult(UnpublishOutcome.Unpublished, targetPath);
        }

        postMarkdown.Metadata.IsPublished = false;
        postMarkdown.Metadata.PublishedOn = null;
        _postManager.Markdown.UpdateFile(postMarkdown);

        _logger.LogInformation("Unpublishing {FileName} to {Target}", fileName, targetPath);
        _fileSystem.File.Move(currentPath, targetPath);

        return new UnpublishPostResult(UnpublishOutcome.Unpublished, targetPath);
    }

    public DeleteDraftResult DeleteDraft(string postName, bool dryRun = false)
    {
        if (!_postManager.TryFindPost(postName, out var postMarkdown))
        {
            _logger.LogError("Could not find {Post} to delete", postName);
            return new DeleteDraftResult(DeleteDraftOutcome.NotFound, null);
        }

        var currentPath = postMarkdown.FilePath;

        if (!IsPathUnder(currentPath, _postManager.Drafts))
        {
            _logger.LogWarning("{Post} is not a draft", postName);
            return new DeleteDraftResult(DeleteDraftOutcome.NotADraft, null);
        }

        if (dryRun)
        {
            return new DeleteDraftResult(DeleteDraftOutcome.Deleted, currentPath);
        }

        _logger.LogInformation("Deleting draft {File}", currentPath);
        _fileSystem.File.Delete(currentPath);

        return new DeleteDraftResult(DeleteDraftOutcome.Deleted, currentPath);
    }

    /// <summary>
    /// Mirrors <see cref="BlogPathResolver.TryResolveWithinBase"/>'s containment check to test
    /// whether a resolved path lives under a given root (e.g. Drafts or Posts).
    /// </summary>
    private bool IsPathUnder(string fullPath, string root)
    {
        var normalizedRoot = _fileSystem.Path.GetFullPath(root);
        var normalizedCandidate = _fileSystem.Path.GetFullPath(fullPath);

        var rootWithSeparator = normalizedRoot.EndsWith(_fileSystem.Path.DirectorySeparatorChar)
            ? normalizedRoot
            : normalizedRoot + _fileSystem.Path.DirectorySeparatorChar;

        return normalizedCandidate.Equals(normalizedRoot, StringComparison.Ordinal) ||
               normalizedCandidate.StartsWith(rootWithSeparator, StringComparison.Ordinal);
    }

    public FixMetadataResult FixMetadata(bool fixStatus, bool fixDescription, bool fixTags, bool dryRun = false)
    {
        var result = new FixMetadataResult();

        foreach (var file in _postManager.GetAllPosts())
        {
            try
            {
                _logger.LogInformation("Updating metadata for {PostTitle}", file.Metadata.Title);

                if (fixStatus) FixPublishedStatus(file);
                if (fixDescription) FixDescription(file);
                if (fixTags) FixTagsOnFile(file);

                if (!dryRun)
                {
                    _postManager.Markdown.UpdateFile(file);
                }

                result.Updated.Add(file.FilePath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Skipping {File} — could not fix metadata", file.FilePath);
                result.Skipped.Add(new FixMetadataSkip(file.FilePath, ex.Message));
            }
        }

        return result;
    }

    public BlogMetaInformation GetBlogInfo()
    {
        var posts = _postManager.LoadYamlHeaderForAllPosts();
        var blogDetails = new BlogMetaInformation();

        blogDetails.PostCount = posts.Count(x => x.IsPublished == true);

        var unpublished = posts.Where(x => x.IsPublished == false).ToList();
        blogDetails.UnPublishedCount = unpublished.Count;
        blogDetails.Unpublished = unpublished.Count > 0 ? unpublished : Enumerable.Empty<YamlHeader>();

        var recents = posts
            .Where(x => x.IsPublished.GetValueOrDefault())
            .TakeLast(6)
            .OrderByDescending(x => x.PublishedOn)
            .ToList();

        blogDetails.LatestPosts = recents;
        blogDetails.LastPost = recents.FirstOrDefault();

        if (blogDetails.LastPost?.PublishedOn.HasValue == true)
            blogDetails.DaysSinceLastPost = _timeProvider.GetLocalNow().DateTime - blogDetails.LastPost.PublishedOn.Value;

        return blogDetails;
    }

    public IReadOnlyList<string> ListDrafts()
    {
        var draftsPath = _postManager.Drafts;
        if (!_fileSystem.Directory.Exists(draftsPath))
            return [];

        return _fileSystem.Directory
            .EnumerateFiles(draftsPath, "*.md", SearchOption.AllDirectories)
            .Select(f => _fileSystem.Path.GetFileName(f))
            .OrderBy(f => f)
            .ToList();
    }

    public IReadOnlyList<DraftDetail> GetDraftDetails()
    {
        var draftsPath = _postManager.Drafts;
        if (!_fileSystem.Directory.Exists(draftsPath))
            return [];

        return _fileSystem.Directory
            .EnumerateFiles(draftsPath, "*.md", SearchOption.AllDirectories)
            .Select(BuildDraftDetail)
            .OrderByDescending(d => d.LastModified)
            .ToList();
    }

    private DraftDetail BuildDraftDetail(string path)
    {
        var fileName = _fileSystem.Path.GetFileName(path);
        var lastModified = _fileSystem.FileInfo.New(path).LastWriteTime;

        MarkdownFile markdownFile;
        try
        {
            markdownFile = _postManager.Markdown.LoadFile(path);
        }
        catch (Exception ex)
        {
            // One broken draft's front matter shouldn't fail the whole list_drafts call — report
            // it with best-effort metadata instead, mirroring the FixMetadata skip-and-report
            // precedent.
            _logger.LogWarning(ex, "Skipping front matter for {File} — could not parse", path);
            return new DraftDetail(fileName, path, null, 0, lastModified, false, ["unparseable front matter"]);
        }

        var body = _postManager.Markdown.GetBody(path);
        var wordCount = body.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;

        return new DraftDetail(
            fileName,
            path,
            markdownFile.Metadata.Title,
            wordCount,
            lastModified,
            !string.IsNullOrWhiteSpace(markdownFile.Metadata.FeaturedImage),
            ContentMarkers.FindMarkers(body));
    }

    private void FixPublishedStatus(MarkdownFile file)
    {
        var rawFileName = _fileSystem.Path.GetFileName(file.FilePath);
        var datePart = rawFileName.Length >= 10 ? rawFileName[..10] : rawFileName;

        if (!DateTime.TryParseExact(datePart, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var publishedOn))
            throw new FormatException($"Could not extract a yyyy-MM-dd date from filename '{rawFileName}'.");

        file.Metadata.PublishedOn = publishedOn;
        file.Metadata.IsPublished = true;
        file.Metadata.IsHidden = false;
    }

    private static void FixDescription(MarkdownFile file)
    {
        if (file.Metadata.Extras.TryGetValue("metadescription", out var metadescription))
        {
            file.Metadata.Description = metadescription;
        }
    }

    private static void FixTagsOnFile(MarkdownFile file)
    {
        if (file.Metadata.Extras.TryGetValue("category", out var category))
        {
            file.Metadata.Tags = category.Contains(',')
                ? SplitToQuotedList(category)
                : [$"'{category}'"];
        }

        if (file.Metadata.Extras.TryGetValue("categories", out var categories))
        {
            file.Metadata.Tags =
                SplitToQuotedList(categories.Replace("[", string.Empty).Replace("]", string.Empty));
        }

        var textInfo = CultureInfo.CurrentCulture.TextInfo;
        file.Metadata.Tags = file.Metadata.Tags
            .GroupBy(x => x)
            .Select(x => textInfo.ToTitleCase(x.First()))
            .ToList();

        static List<string> SplitToQuotedList(string s) =>
            s.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Select(s => $"'{s}'")
                .ToList();
    }
}
