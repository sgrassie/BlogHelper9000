using System.Globalization;
using System.IO.Abstractions;
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

    public string AddPost(string title, bool isDraft, bool isFeatured = false, bool isHidden = false, string? featuredImage = null)
    {
        var filePath = isDraft
            ? _postManager.CreateDraftPath(title)
            : _postManager.CreatePostPath(title);

        var yamlHeader = new YamlHeader
        {
            Title = title,
            Tags = [],
            FeaturedImage = featuredImage ?? string.Empty,
            IsFeatured = isFeatured,
            IsHidden = isHidden,
            IsPublished = !isDraft
        };

        var yamlHeaderText = _postManager.YamlConvert.Serialise(yamlHeader);
        _fileSystem.File.AppendAllText(filePath, yamlHeaderText);

        _logger.LogInformation("Added new post at {File}", filePath);
        return filePath;
    }

    public string? PublishPost(string postName)
    {
        if (!_postManager.TryFindPost(postName, out var postMarkdown))
        {
            _logger.LogError("Could not find {Post} to publish", postName);
            return null;
        }

        var currentPath = postMarkdown.FilePath;
        postMarkdown.Metadata.IsPublished = true;
        postMarkdown.Metadata.PublishedOn = _timeProvider.GetLocalNow().DateTime;
        _postManager.Markdown.UpdateFile(postMarkdown);

        var fileName = _fileSystem.Path.GetFileName(postMarkdown.FilePath);
        var publishedFilename = $"{_timeProvider.GetLocalNow().DateTime:yyyy-MM-dd}-{fileName}";
        var targetFolder = _fileSystem.Path.Combine(_postManager.Posts, $"{_timeProvider.GetLocalNow().DateTime:yyyy}");

        if (!_fileSystem.Directory.Exists(targetFolder))
        {
            _fileSystem.Directory.CreateDirectory(targetFolder);
        }

        var replacementPath = _fileSystem.Path.Combine(targetFolder, publishedFilename);

        _logger.LogInformation("Publishing {PublishedFileName} to {TargetFolder}", publishedFilename, targetFolder);
        _fileSystem.File.Move(currentPath, replacementPath);
        _fileSystem.File.Delete(currentPath);

        return replacementPath;
    }

    public void FixMetadata(bool fixStatus, bool fixDescription, bool fixTags)
    {
        foreach (var file in _postManager.GetAllPosts())
        {
            _logger.LogInformation("Updating metadata for {PostTitle}", file.Metadata.Title);

            if (fixStatus) FixPublishedStatus(file);
            if (fixDescription) FixDescription(file);
            if (fixTags) FixTagsOnFile(file);

            _postManager.Markdown.UpdateFile(file);
        }
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
            blogDetails.DaysSinceLastPost = DateTime.Now - blogDetails.LastPost.PublishedOn.Value;

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

    private static void FixPublishedStatus(MarkdownFile file)
    {
        var rawFileName = file.FilePath.Split("/").Last();
        var datePart = rawFileName[..10];
        file.Metadata.PublishedOn = DateTime.ParseExact(datePart, "yyyy-MM-dd", CultureInfo.InvariantCulture).Date;
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
