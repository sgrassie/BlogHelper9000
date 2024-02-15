using BlogHelper9000.YamlParsing;

namespace BlogHelper9000.Handlers;

public class BlogMetaInformation
{
    public int PostCount { get; set; }
    public YamlHeader? LastPost { get; set; }
    public int UnPublishedCount { get; set; }
    public IEnumerable<YamlHeader>? Unpublished { get; set; }
    public List<YamlHeader>? LatestPosts { get; set; }
    public TimeSpan DaysSinceLastPost { get; set; }
}