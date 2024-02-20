using BlogHelper9000.Helpers;
using BlogHelper9000.Models;
using BlogHelper9000.Reporters;
using BlogHelper9000.YamlParsing;

namespace BlogHelper9000.Handlers;

public class InfoCommandHandler
{
    private readonly IConsole _console;
    private PostManager _postManager;

    public InfoCommandHandler(PostManager postManager, IConsole console)
    {
        _console = console;
        _postManager = postManager;
    }

    public void Execute(InfoCommandReporter? reporter = null)
    {
        var posts = _postManager.LoadYamlHeaderForAllPosts();
        var blogDetails = new BlogMetaInformation();

        DeterminePostCount(posts, blogDetails);
        DetermineDraftsInfo(posts, blogDetails);
        DetermineRecentPosts(posts, blogDetails);
        DetermineDaysSinceLastPost(blogDetails);

        reporter?.Report(blogDetails);
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
}