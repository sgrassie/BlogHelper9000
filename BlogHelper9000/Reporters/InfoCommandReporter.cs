using BlogHelper9000.Core.Models;
using BlogHelper9000.Core.YamlParsing;

namespace BlogHelper9000.Reporters;

public class InfoCommandReporter
{
    public void Report(BlogMetaInformation blogMetaInformation)
    {
        var panel = RenderPanel(RenderGrid());

        AnsiConsole.WriteLine();
        AnsiConsole.Write(panel);
        return;

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
