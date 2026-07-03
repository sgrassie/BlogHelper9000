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

        blogDetails.PostCount = posts.Count;

        var unpublished = posts.Where(x => x.IsPublished == false).ToList();
        blogDetails.UnPublishedCount = unpublished.Count;
        blogDetails.Unpublished = unpublished.Count > 0 ? unpublished : Enumerable.Empty<YamlHeader>();

        var recents = posts
            .Where(x => x.IsPublished.GetValueOrDefault())
            .TakeLast(6)
            .OrderByDescending(x => x.PublishedOn)
            .ToList();

        blogDetails.LatestPosts = recents.Count > 0 ? recents.Skip(1).ToList() : [];
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
