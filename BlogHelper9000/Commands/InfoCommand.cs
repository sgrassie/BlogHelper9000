using BlogHelper9000.Commands.Inputs;
using BlogHelper9000.YamlParsing;

namespace BlogHelper9000.Commands;

public class InfoCommand : BaseCommand<BaseInput>
{
    public InfoCommand()
    {
        Usage("Info");
    }

    protected override bool Run(BaseInput input)
    {
        var posts = LoadsPosts();
        var blogDetails = new Details();

        DeterminePostCount(posts, blogDetails);
        DetermineDraftsInfo(posts, blogDetails);
        DetermineRecentPosts(posts, blogDetails);
        DetermineDaysSinceLastPost(blogDetails);

        RenderDetails(blogDetails);

        return true;
    }

    private void DetermineDaysSinceLastPost(Details blogDetails)
    {
        if(blogDetails.LastPost is not null && blogDetails.LastPost.PublishedOn.HasValue)
            blogDetails.DaysSinceLastPost = DateTime.Now - blogDetails.LastPost.PublishedOn.Value;
    }

    private void DetermineRecentPosts(IReadOnlyList<YamlHeader> posts, Details blogDetails)
    {
        var recents = posts
            .Where(x => x.IsPublished.GetValueOrDefault())
            .TakeLast(6)
            .OrderByDescending(x => x.PublishedOn)
            .ToList();

        blogDetails.LatestPosts = recents.Any() ? recents.Skip(1).ToList() : Enumerable.Empty<YamlHeader>().ToList();
        blogDetails.LastPost = recents.FirstOrDefault();
    }

    private void DetermineDraftsInfo(IReadOnlyList<YamlHeader> posts, Details blogDetails)
    {
        var unpublished = posts.Where(x => x.IsPublished == false).ToList();
        blogDetails.UnPublishedCount = unpublished.Count;
        blogDetails.Unpublished = unpublished.Any() ? unpublished : Enumerable.Empty<YamlHeader>();
    }

    private void DeterminePostCount(IReadOnlyList<YamlHeader> posts, Details blogDetails)
    {
        blogDetails.PostCount = posts.Count;
    }

    private IReadOnlyList<YamlHeader> LoadsPosts()
    {
        var allPosts = new List<YamlHeader>();

        var posts = Directory.EnumerateFiles(PostsPath, "*.md", SearchOption.AllDirectories);
        var drafts = Directory.EnumerateFiles(DraftsPath, "*.md", SearchOption.AllDirectories);

        allPosts.AddRange(posts.Select(file => YamlConvert.Deserialise(File.ReadAllLines(file))));
        allPosts.AddRange(drafts.Select(file => YamlConvert.Deserialise(File.ReadAllLines(file))));

        return allPosts.OrderBy(x => x.PublishedOn).ToList().AsReadOnly();
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
                .AddRow("Available drafts", ":", FormatPostDetail(details.Unpublished?.FirstOrDefault()));

            foreach (var recent in details.Unpublished?.Skip(1)!)
            {
                grid.AddRow(string.Empty, string.Empty, FormatPostDetail(recent));
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