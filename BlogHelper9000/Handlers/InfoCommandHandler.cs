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

    public void Execute()
    {
        var posts = LoadsPosts();
        var blogDetails = new Details();

        DeterminePostCount(posts, blogDetails);
        DetermineDraftsInfo(posts, blogDetails);
        DetermineRecentPosts(posts, blogDetails);
        DetermineDaysSinceLastPost(blogDetails);

        RenderDetails(blogDetails);
        
    }
    
    private static void DetermineDaysSinceLastPost(Details blogDetails)
    {
        if(blogDetails.LastPost is not null && blogDetails.LastPost.PublishedOn.HasValue)
            blogDetails.DaysSinceLastPost = DateTime.Now - blogDetails.LastPost.PublishedOn.Value;
    }

    private static void DetermineRecentPosts(IEnumerable<YamlHeader> posts, Details blogDetails)
    {
        var recents = posts
            .Where(x => x.IsPublished.GetValueOrDefault())
            .TakeLast(6)
            .OrderByDescending(x => x.PublishedOn)
            .ToList();

        blogDetails.LatestPosts = recents.Any() ? recents.Skip(1).ToList() : Enumerable.Empty<YamlHeader>().ToList();
        blogDetails.LastPost = recents.FirstOrDefault();
    }

    private static void DetermineDraftsInfo(IEnumerable<YamlHeader> posts, Details blogDetails)
    {
        var unpublished = posts.Where(x => x.IsPublished == false).ToList();
        blogDetails.UnPublishedCount = unpublished.Count;
        blogDetails.Unpublished = unpublished.Any() ? unpublished : Enumerable.Empty<YamlHeader>();
    }

    private static void DeterminePostCount(IReadOnlyCollection<YamlHeader> posts, Details blogDetails)
    {
        blogDetails.PostCount = posts.Count;
    }

    private IReadOnlyList<YamlHeader> LoadsPosts()
    {
        var allPosts = new List<YamlHeader>();

        var posts = _fileSystemHelper.FileSystem.Directory.EnumerateFiles(_fileSystemHelper.Posts, "*.md", SearchOption.AllDirectories);
        var drafts = _fileSystemHelper.FileSystem.Directory.EnumerateFiles(_fileSystemHelper.Posts, "*.md", SearchOption.AllDirectories);

        allPosts.AddRange(posts.Select(GetHeaderWithOriginalFilename));
        allPosts.AddRange(drafts.Select(GetHeaderWithOriginalFilename));

        return allPosts.OrderBy(x => x.PublishedOn).ToList().AsReadOnly();

        YamlHeader GetHeaderWithOriginalFilename(string f)
        {
            var header = _fileSystemHelper.YamlConvert.Deserialise(File.ReadAllLines(f));
            var fileInfo = new FileInfo(f);
            header.Extras.Add("originalFilename", fileInfo.Name);
            header.Extras.Add("lastUpdated", $"{fileInfo.LastWriteTime:dd/MM/yyyy hh:mm:ss}");
            return header;
        }
    }

    private void RenderDetails(Details details)
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
                .AddRow("Last Post", ":", FormatPostDetail(details.LastPost))
                .AddRow("# days since last post", ":", $"{details.DaysSinceLastPost.Days}")
                .AddRow("# of posts", ":", $"{details.PostCount - details.UnPublishedCount}")
                .AddRow("# of drafts", ":", $"{details.UnPublishedCount}")
                .AddRow("Available drafts", ":", FormatDraftPostDetail(details.Unpublished?.FirstOrDefault()));

            foreach (var recent in details.Unpublished?.Skip(1)!)
            {
                grid.AddRow(string.Empty, string.Empty, FormatDraftPostDetail(recent));
            }

            grid.AddRow("Recent posts", ":", FormatPostDetail(details.LatestPosts?.FirstOrDefault()));

            foreach (var latest in details.LatestPosts?.Skip(1)!)
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

    private class Details
    {
        public int PostCount { get; set; }
        public YamlHeader? LastPost { get; set; }
        public int UnPublishedCount { get; set; }
        public IEnumerable<YamlHeader>? Unpublished { get; set; }
        public List<YamlHeader>? LatestPosts { get; set; }
        public TimeSpan DaysSinceLastPost { get; set; }
    }
}