using BlogHelper9000.Helpers;
using BlogHelper9000.YamlParsing;

namespace BlogHelper9000.Handlers;

public class InfoCommandHandler
{
    private readonly IFileSystem _fileSystem;
    private readonly string _baseDirectory;
    private readonly IConsole _console;
    private FileSystemHelper _fileSystemHelper;

    public InfoCommandHandler(IFileSystem fileSystem, string baseDirectory, IConsole console)
    {
        _fileSystem = fileSystem;
        _baseDirectory = baseDirectory;
        _console = console;
        _fileSystemHelper = new FileSystemHelper(_fileSystem, _baseDirectory);
    }

    public BlogMetaInformation Execute()
    {
        var posts = LoadsPosts();
        var blogDetails = new BlogMetaInformation();

        DeterminePostCount(posts, blogDetails);
        DetermineDraftsInfo(posts, blogDetails);
        DetermineRecentPosts(posts, blogDetails);
        DetermineDaysSinceLastPost(blogDetails);

        return blogDetails;
        // RenderDetails(blogDetails);
    }
    
    private static void DetermineDaysSinceLastPost(BlogMetaInformation blogBlogMetaInformation)
    {
        if(blogBlogMetaInformation.LastPost is not null && blogBlogMetaInformation.LastPost.PublishedOn.HasValue)
            blogBlogMetaInformation.DaysSinceLastPost = DateTime.Now - blogBlogMetaInformation.LastPost.PublishedOn.Value;
    }

    private static void DetermineRecentPosts(IEnumerable<YamlHeader> posts, BlogMetaInformation blogBlogMetaInformation)
    {
        var recents = posts
            .Where(x => x.IsPublished.GetValueOrDefault())
            .TakeLast(6)
            .OrderByDescending(x => x.PublishedOn)
            .ToList();

        blogBlogMetaInformation.LatestPosts = recents.Any() ? recents.Skip(1).ToList() : Enumerable.Empty<YamlHeader>().ToList();
        blogBlogMetaInformation.LastPost = recents.FirstOrDefault();
    }

    private static void DetermineDraftsInfo(IEnumerable<YamlHeader> posts, BlogMetaInformation blogBlogMetaInformation)
    {
        var unpublished = posts.Where(x => x.IsPublished == false).ToList();
        blogBlogMetaInformation.UnPublishedCount = unpublished.Count;
        blogBlogMetaInformation.Unpublished = unpublished.Any() ? unpublished : Enumerable.Empty<YamlHeader>();
    }

    private static void DeterminePostCount(IReadOnlyCollection<YamlHeader> posts, BlogMetaInformation blogBlogMetaInformation)
    {
        blogBlogMetaInformation.PostCount = posts.Count;
    }

    private IReadOnlyList<YamlHeader> LoadsPosts()
    {
        var allPosts = new List<YamlHeader>();

        var posts = _fileSystemHelper.FileSystem.Directory.EnumerateFiles(_fileSystemHelper.Drafts, "*.md", SearchOption.AllDirectories).ToList();
        var drafts = _fileSystemHelper.FileSystem.Directory.EnumerateFiles(_fileSystemHelper.Posts, "*.md", SearchOption.AllDirectories).ToList();

        allPosts.AddRange(posts.Select(GetHeaderWithOriginalFilename));
        allPosts.AddRange(drafts.Select(GetHeaderWithOriginalFilename));

        return allPosts.OrderBy(x => x.PublishedOn).ToList().AsReadOnly();

        YamlHeader GetHeaderWithOriginalFilename(string f)
        {
            var lines = _fileSystemHelper.FileSystem.File.ReadAllLines(f);
            var header = _fileSystemHelper.YamlConvert.Deserialise(lines);
            var fileInfo = new FileInfo(f);
            header.Extras.Add("originalFilename", fileInfo.Name);
            header.Extras.Add("lastUpdated", $"{fileInfo.LastWriteTime:dd/MM/yyyy hh:mm:ss}");
            return header;
        }
    }

    private void RenderDetails(BlogMetaInformation blogMetaInformation)
    {
        var panel = RenderPanel(RenderGrid());

        AnsiConsole.WriteLine();
        AnsiConsole.Write(panel);

        Grid RenderGrid()
        {
            var grid = new Grid { Expand = true }
                .AddColumns(
                    new GridColumn().LeftAligned(),
                    new GridColumn().LeftAligned(),
                    new GridColumn())
                .AddRow("Last Post", ":", FormatPostDetail(blogMetaInformation.LastPost))
                .AddRow("# days since last post", ":", $"{blogMetaInformation.DaysSinceLastPost.Days}")
                .AddRow("# of posts", ":", $"{blogMetaInformation.PostCount - blogMetaInformation.UnPublishedCount}")
                .AddRow("# of drafts", ":", $"{blogMetaInformation.UnPublishedCount}")
                .AddRow("Available drafts", ":", FormatDraftPostDetail(blogMetaInformation.Unpublished?.FirstOrDefault()));

            foreach (var recent in blogMetaInformation.Unpublished?.Skip(1)!)
            {
                grid.AddRow(string.Empty, string.Empty, FormatDraftPostDetail(recent));
            }

            grid.AddRow("Recent posts", ":", FormatPostDetail(blogMetaInformation.LatestPosts?.FirstOrDefault()));

            foreach (var latest in blogMetaInformation.LatestPosts?.Skip(1)!)
            {
                grid.AddRow(string.Empty, string.Empty, FormatPostDetail(latest));
            }

            string FormatPostDetail(YamlHeader? header)
            {
                if (header is null) return string.Empty;
                var title = string.IsNullOrEmpty(header.Title) ? string.Empty : header.Title;
                var postDate = header.PublishedOn.HasValue
                    ? header.PublishedOn.Value.ToShortDateString()
                    : string.Empty;
                return $"{title} ({postDate})";
            }

            string FormatDraftPostDetail(YamlHeader? header)
            {
                if (header is null) return string.Empty;

                if (header.Extras.TryGetValue("originalFilename", out var originalFileName)
                    && header.Extras.TryGetValue("lastUpdated", out var lastUpdated))
                {
                    return $"{originalFileName} (updated: {lastUpdated})";
                }

                return string.Empty;
            }

            return grid;
        }

        Panel RenderPanel(Grid grid)
        {
            return new Panel(grid)
                {
                    Border = BoxBorder.Rounded,
                    Padding = new Padding(1, 1, 1, 1),
                }
                .Header(new PanelHeader(" Details ", Justify.Center));
        }
    }
}