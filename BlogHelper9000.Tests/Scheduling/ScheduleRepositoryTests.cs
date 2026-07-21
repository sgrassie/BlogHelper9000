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

    [Fact]
    public void UpdateEntry_ChangesEditableFields_ButLeavesPublishedStateUntouched()
    {
        var seriesId = Repository.AddSeries("Series");
        var otherSeriesId = Repository.AddSeries("Other Series");
        Repository.AddEntry(seriesId, Entry("Original", "post.md", position: 1, week: 1));
        var id = Repository.FindEntryByFilename("post.md")!.Id;
        Repository.SetPublished(id, true, new DateOnly(2026, 7, 1));

        Repository.UpdateEntry(id, otherSeriesId, 3, 9, new DateOnly(2026, 8, 1),
            "Updated Title", "newtags", "newnotes");

        var entry = Repository.FindEntryByFilename("post.md")!;
        entry.Series.Should().Be("Other Series");
        entry.Position.Should().Be(3);
        entry.Week.Should().Be(9);
        entry.PublishDate.Should().Be(new DateOnly(2026, 8, 1));
        entry.Title.Should().Be("Updated Title");
        entry.Tags.Should().Be("newtags");
        entry.Notes.Should().Be("newnotes");
        entry.Published.Should().BeTrue();
        entry.PublishedOn.Should().Be(new DateOnly(2026, 7, 1));
        entry.Topic.Should().Be("Topic");
        entry.Source.Should().Be("New");
    }

    [Fact]
    public void DeleteEntry_RemovesExactlyOneEntry()
    {
        var seriesId = Repository.AddSeries("Series");
        Repository.AddEntry(seriesId, Entry("First", "first.md"));
        Repository.AddEntry(seriesId, Entry("Second", "second.md"));
        var idToDelete = Repository.FindEntryByFilename("first.md")!.Id;

        Repository.DeleteEntry(idToDelete);

        var entries = Repository.GetEntries(seriesId);
        entries.Should().ContainSingle();
        entries[0].DraftFilename.Should().Be("second.md");
    }

    [Fact]
    public void CountEntries_ReturnsNumberOfEntriesInSeries()
    {
        var seriesId = Repository.AddSeries("Series");
        var otherSeriesId = Repository.AddSeries("Other");
        Repository.AddEntry(seriesId, Entry("First", "first.md"));
        Repository.AddEntry(seriesId, Entry("Second", "second.md"));
        Repository.AddEntry(otherSeriesId, Entry("Third", "third.md"));

        Repository.CountEntries(seriesId).Should().Be(2);
        Repository.CountEntries(otherSeriesId).Should().Be(1);
    }

    [Fact]
    public void RenameSeries_ChangesName()
    {
        var seriesId = Repository.AddSeries("Old Name");

        Repository.RenameSeries(seriesId, "New Name");

        Repository.ListSeries().Single(s => s.Id == seriesId).Name.Should().Be("New Name");
    }

    [Fact]
    public void DeleteSeries_CascadesEntriesAway()
    {
        var seriesId = Repository.AddSeries("Series");
        Repository.AddEntry(seriesId, Entry("First", "first.md"));

        Repository.DeleteSeries(seriesId);

        Repository.ListSeries().Should().NotContain(s => s.Id == seriesId);
        Repository.FindEntryByFilename("first.md").Should().BeNull();
    }

    [Fact]
    public void SetSeriesCadence_RoundTripsThroughListSeriesAndFindSeries()
    {
        var seriesId = Repository.AddSeries("Series");

        Repository.SetSeriesCadence(seriesId, (int)DayOfWeek.Wednesday, "2026-07-08");

        var fromList = Repository.ListSeries().Single(s => s.Id == seriesId);
        fromList.CadenceDay.Should().Be(DayOfWeek.Wednesday);
        fromList.CadenceStart.Should().Be(new DateOnly(2026, 7, 8));

        var fromFind = Repository.FindSeries("Series")!;
        fromFind.CadenceDay.Should().Be(DayOfWeek.Wednesday);
        fromFind.CadenceStart.Should().Be(new DateOnly(2026, 7, 8));
    }

    [Fact]
    public void SetSeriesCadence_WithNull_ClearsCadence()
    {
        var seriesId = Repository.AddSeries("Series");
        Repository.SetSeriesCadence(seriesId, (int)DayOfWeek.Wednesday, "2026-07-08");

        Repository.SetSeriesCadence(seriesId, null, null);

        var series = Repository.FindSeries("Series")!;
        series.CadenceDay.Should().BeNull();
        series.CadenceStart.Should().BeNull();
    }

    [Fact]
    public void Renumber_SwapsTwoEntriesPositions_WithoutViolatingUniqueConstraint()
    {
        var seriesId = Repository.AddSeries("Series");
        Repository.AddEntry(seriesId, Entry("First", "first.md", position: 1));
        Repository.AddEntry(seriesId, Entry("Second", "second.md", position: 2));
        var firstId = Repository.FindEntryByFilename("first.md")!.Id;
        var secondId = Repository.FindEntryByFilename("second.md")!.Id;

        Repository.Renumber(seriesId, [(firstId, 2), (secondId, 1)]);

        Repository.FindEntryByFilename("first.md")!.Position.Should().Be(2);
        Repository.FindEntryByFilename("second.md")!.Position.Should().Be(1);
    }

    [Fact]
    public void Renumber_ReversesAFullSeries()
    {
        var seriesId = Repository.AddSeries("Series");
        Repository.AddEntry(seriesId, Entry("First", "first.md", position: 1));
        Repository.AddEntry(seriesId, Entry("Second", "second.md", position: 2));
        Repository.AddEntry(seriesId, Entry("Third", "third.md", position: 3));
        var firstId = Repository.FindEntryByFilename("first.md")!.Id;
        var secondId = Repository.FindEntryByFilename("second.md")!.Id;
        var thirdId = Repository.FindEntryByFilename("third.md")!.Id;

        Repository.Renumber(seriesId, [(firstId, 3), (secondId, 2), (thirdId, 1)]);

        var entries = Repository.GetEntries(seriesId);
        entries.Select(e => e.DraftFilename).Should().ContainInOrder("third.md", "second.md", "first.md");
    }

    public void Dispose() => _db.Dispose();
}
