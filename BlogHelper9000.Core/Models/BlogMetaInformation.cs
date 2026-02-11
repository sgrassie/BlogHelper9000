using BlogHelper9000.Core.YamlParsing;

namespace BlogHelper9000.Core.Models;

public class BlogMetaInformation
{
    public int PostCount { get; set; }
    public YamlHeader? LastPost { get; set; }
    public int UnPublishedCount { get; set; }
    public IEnumerable<YamlHeader>? Unpublished { get; set; }
    public List<YamlHeader>? LatestPosts { get; set; }
    public TimeSpan DaysSinceLastPost { get; set; }
}
