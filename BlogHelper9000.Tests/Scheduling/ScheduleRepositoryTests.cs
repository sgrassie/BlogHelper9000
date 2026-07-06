using BlogHelper9000.Core.Scheduling;

namespace BlogHelper9000.Tests.Scheduling;

public class ScheduleRepositoryTests : IDisposable
{
    private readonly ScheduleDatabase _db = ScheduleDatabase.OpenInMemory();
    private ScheduleRepository Repository => new(_db);

    private static NewScheduleEntry Entry(string title, string filename, int? position = null,
        int? week = null, DateOnly? date = null, bool published = false) =>
        new(position, week, date, "Topic", title, filename, "csharp, dotnet", "New", published, null);

    [Fact]
    public void AddSeries_ThenListSeries_ReturnsSeriesInSortOrder()
    {
        Repository.AddSeries("FootballData", sortOrder: 1);
        Repository.AddSeries("BlogHelper9000 revisited", sortOrder: 0);

        var series = Repository.ListSeries();

        series.Select(s => s.Name).Should().ContainInOrder("BlogHelper9000 revisited", "FootballData");
    }

    [Fact]
    public void FindSeries_IsCaseInsensitive()
    {
        Repository.AddSeries("Traefik in the homelab");

        Repository.FindSeries("traefik IN the Homelab").Should().NotBeNull();
    }

    [Fact]
    public void AddEntry_WithoutPosition_AppendsAfterHighestPosition()
    {
        var seriesId = Repository.AddSeries("Series");
        Repository.AddEntry(seriesId, Entry("First", "first.md", position: 5));

        Repository.AddEntry(seriesId, Entry("Second", "second.md"));

        var entries = Repository.GetEntries(seriesId);
        entries.Should().HaveCount(2);
        entries[1].Position.Should().Be(6);
    }

    [Fact]
    public void GetEntries_RoundTripsAllFields_OrderedByPosition()
    {
        var seriesId = Repository.AddSeries("Series");
        Repository.AddEntry(seriesId, Entry("B", "b.md", position: 2, week: 4, date: new DateOnly(2026, 7, 7)));
        Repository.AddEntry(seriesId, Entry("A", "a.md", position: 1));

        var entries = Repository.GetEntries(seriesId);

        entries.Select(e => e.Title).Should().ContainInOrder("A", "B");
        entries[1].Should().BeEquivalentTo(new
        {
            Series = "Series",
            Position = 2,
            Week = 4,
            PublishDate = new DateOnly(2026, 7, 7),
            Topic = "Topic",
            Title = "B",
            DraftFilename = "b.md",
            Tags = "csharp, dotnet",
            Source = "New",
            Published = false,
            PublishedOn = (DateOnly?)null
        }, options => options.ExcludingMissingMembers());
    }

    [Fact]
    public void FindEntryByFilename_ReturnsEntryWithSeriesName()
    {
        var seriesId = Repository.AddSeries("Series");
        Repository.AddEntry(seriesId, Entry("Post", "my-post.md"));

        var entry = Repository.FindEntryByFilename("my-post.md");

        entry.Should().NotBeNull();
        entry!.Series.Should().Be("Series");
    }

    [Fact]
    public void SetPublished_TicksEntryAndRecordsDate()
    {
        var seriesId = Repository.AddSeries("Series");
        Repository.AddEntry(seriesId, Entry("Post", "my-post.md"));
        var id = Repository.FindEntryByFilename("my-post.md")!.Id;

        Repository.SetPublished(id, true, new DateOnly(2026, 7, 6));

        var entry = Repository.FindEntryByFilename("my-post.md")!;
        entry.Published.Should().BeTrue();
        entry.PublishedOn.Should().Be(new DateOnly(2026, 7, 6));
    }

    [Fact]
    public void Meta_UpsertsAndReads()
    {
        Repository.SetMeta("baseline_published_count", "243");
        Repository.SetMeta("baseline_published_count", "244");

        Repository.GetMeta("baseline_published_count").Should().Be("244");
        Repository.GetMeta("missing").Should().BeNull();
    }

    public void Dispose() => _db.Dispose();
}
