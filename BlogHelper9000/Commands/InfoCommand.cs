using BlogHelper9000.Helpers;
using BlogHelper9000.Models;
using BlogHelper9000.Reporters;
using BlogHelper9000.YamlParsing;
using TimeWarp.Mediator;

namespace BlogHelper9000.Commands;

public sealed class InfoCommand : IRequest
{
    public string BaseDirectory { get; set; }

    public sealed class Handler(ILogger<Handler> logger, PostManager postManager, InfoCommandReporter reporter)
        : IRequestHandler<InfoCommand>
    {
        public Task Handle(InfoCommand request, CancellationToken cancellationToken)
        {
            
            logger.LogDebug("Loading all posts yaml headers");
            var posts = postManager.LoadYamlHeaderForAllPosts();
            var blogDetails = new BlogMetaInformation();

            DeterminePostCount(posts, blogDetails);
            DetermineDraftsInfo(posts, blogDetails);
            DetermineRecentPosts(posts, blogDetails);
            DetermineDaysSinceLastPost(blogDetails);

            reporter?.Report(blogDetails);
            
            return Task.CompletedTask;
        }

        private static void DetermineDaysSinceLastPost(BlogMetaInformation blogBlogMetaInformation)
        {
            if (blogBlogMetaInformation.LastPost is not null && blogBlogMetaInformation.LastPost.PublishedOn.HasValue)
                blogBlogMetaInformation.DaysSinceLastPost =
                    DateTime.Now - blogBlogMetaInformation.LastPost.PublishedOn.Value;
        }

        private static void DetermineRecentPosts(IEnumerable<YamlHeader> posts,
            BlogMetaInformation blogBlogMetaInformation)
        {
            var recents = posts
                .Where(x => x.IsPublished.GetValueOrDefault())
                .TakeLast(6)
                .OrderByDescending(x => x.PublishedOn)
                .ToList();

            blogBlogMetaInformation.LatestPosts =
                recents.Any() ? recents.Skip(1).ToList() : Enumerable.Empty<YamlHeader>().ToList();
            blogBlogMetaInformation.LastPost = recents.FirstOrDefault();
        }

        private static void DetermineDraftsInfo(IEnumerable<YamlHeader> posts,
            BlogMetaInformation blogBlogMetaInformation)
        {
            var unpublished = posts.Where(x => x.IsPublished == false).ToList();
            blogBlogMetaInformation.UnPublishedCount = unpublished.Count;
            blogBlogMetaInformation.Unpublished = unpublished.Any() ? unpublished : Enumerable.Empty<YamlHeader>();
        }

        private static void DeterminePostCount(IReadOnlyCollection<YamlHeader> posts,
            BlogMetaInformation blogBlogMetaInformation)
        {
            blogBlogMetaInformation.PostCount = posts.Count;
        }
    }
}