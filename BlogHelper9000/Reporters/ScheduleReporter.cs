using BlogHelper9000.Core.Scheduling;

namespace BlogHelper9000.Reporters;

public class ScheduleReporter
{
    public void ReportSeries(IReadOnlyList<SeriesStats> series)
    {
        var table = new Table().Expand()
            .AddColumns("Series", "Planned", "Published", "Remaining", "% done", "Next planned", "Next slot");
        foreach (var s in series)
        {
            table.AddRow(
                Markup.Escape(s.Series),
                s.Planned.ToString(),
                s.Published.ToString(),
                s.Remaining.ToString(),
                $"{s.PercentDone:P0}",
                Markup.Escape(s.NextPlannedTitle ?? "all published"),
                Markup.Escape(s.NextSlot ?? string.Empty));
        }
        AnsiConsole.Write(table);
    }

    public void ReportEntries(string seriesName, IReadOnlyList<ScheduleEntry> entries)
    {
        var table = new Table().Expand().Title(Markup.Escape(seriesName))
            .AddColumns("#", "Week", "Date", "Topic", "Title", "Draft filename", "Published");
        foreach (var e in entries)
        {
            table.AddRow(
                e.Position.ToString(),
                e.Week?.ToString() ?? string.Empty,
                e.PublishDate?.ToString("yyyy-MM-dd") ?? string.Empty,
                Markup.Escape(e.Topic ?? string.Empty),
                Markup.Escape(e.Title),
                Markup.Escape(e.DraftFilename),
                e.Published
                    ? $"[green]published{(e.PublishedOn is { } on ? $" {on:yyyy-MM-dd}" : string.Empty)}[/]"
                    : string.Empty);
        }
        AnsiConsole.Write(table);
    }

    public void ReportDashboard(ScheduleDashboard dashboard)
    {
        var filled = (int)Math.Round(dashboard.Progress * 20);
        var bar = new string('█', filled) + new string('░', 20 - filled);

        var grid = new Grid { Expand = true }
            .AddColumns(new GridColumn().LeftAligned(), new GridColumn().LeftAligned(), new GridColumn())
            .AddRow("Published posts (baseline)", ":",
                $"{dashboard.BaselinePublished}{(dashboard.BaselineDate is { } d ? $" (as of {d:yyyy-MM-dd})" : string.Empty)}")
            .AddRow("Published via schedules", ":", dashboard.PublishedViaSchedules.ToString())
            .AddRow("Total published on the blog", ":", dashboard.TotalPublished.ToString())
            .AddRow("Posts planned across all series", ":", dashboard.TotalPlanned.ToString())
            .AddRow("Drafts remaining", ":", dashboard.TotalRemaining.ToString())
            .AddRow("Schedule progress", ":", $"{bar} {dashboard.Progress:P1}")
            .AddRow("Draft : published", ":", $"{dashboard.TotalRemaining} : {dashboard.TotalPublished}");

        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Panel(grid).Header("Publishing dashboard"));
        AnsiConsole.WriteLine();
        ReportSeries(dashboard.Series);
    }
}
