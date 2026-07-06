using BlogHelper9000.Core.Scheduling;
using BlogHelper9000.Scheduling;
using ClosedXML.Excel;

namespace BlogHelper9000.Tests.Scheduling;

public class ScheduleXlsxImporterTests : IDisposable
{
    private readonly ScheduleDatabase _db = ScheduleDatabase.OpenInMemory();

    // The workbook is built with ClosedXML (create/save works under SixLabors.Fonts 3.x;
    // only loading is broken) and read back through the importer's ExcelDataReader path.
    private static MemoryStream BuildWorkbook()
    {
        using var workbook = new XLWorkbook();

        var dashboard = workbook.AddWorksheet("Dashboard");
        dashboard.Cell("A4").Value = "Published posts (baseline, 4 Jul 2026)";
        dashboard.Cell("B4").Value = 243;
        dashboard.Cell("A11").Value = "BlogHelper9000 revisited";
        dashboard.Cell("B11").Value = "Schedule";

        var progress = workbook.AddWorksheet("Progress");
        progress.Cell("A1").Value = "Publishing progress";

        var schedule = workbook.AddWorksheet("Schedule");
        string[] scheduleHeader = ["#", "Week", "Series", "Post title", "Draft filename", "Source", "Published?", "Notes"];
        for (var i = 0; i < scheduleHeader.Length; i++)
            schedule.Cell(1, i + 1).Value = scheduleHeader[i];
        schedule.Cell("A2").Value = 1;
        schedule.Cell("B2").Value = 1;
        schedule.Cell("C2").Value = "BlogHelper9000 revisited";
        schedule.Cell("D2").Value = "BlogHelper9000, four years on";
        schedule.Cell("E2").Value = "bloghelper9000-four-years-on.md";
        schedule.Cell("F2").Value = "New";
        schedule.Cell("G2").Value = true;

        var traefik = workbook.AddWorksheet("Traefik Series");
        string[] datedHeader = ["#", "Week", "Publish date", "Day", "Series", "Post title", "Draft filename", "Tags", "Published?", "Notes"];
        for (var i = 0; i < datedHeader.Length; i++)
            traefik.Cell(1, i + 1).Value = datedHeader[i];
        traefik.Cell("A2").Value = 1;
        traefik.Cell("B2").Value = 1;
        traefik.Cell("C2").Value = new DateTime(2026, 7, 9);
        traefik.Cell("D2").Value = "Thursday";
        traefik.Cell("E2").Value = "Foundations";
        traefik.Cell("F2").Value = "Traefik in the homelab: one proxy for everything";
        traefik.Cell("G2").Value = "traefik-in-the-homelab-one-proxy-for-everything.md";
        traefik.Cell("H2").Value = "traefik, homelab, docker";
        traefik.Cell("I2").Value = false;
        traefik.Cell("J2").Value = "Series opener";

        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return stream;
    }

    [Fact]
    public void Import_SkipsDashboardAndProgress_AndImportsEntrySheets()
    {
        using var workbook = BuildWorkbook();

        var summary = ScheduleXlsxImporter.Import(workbook, _db);

        summary.SeriesCounts.Should().BeEquivalentTo(new[]
        {
            ("BlogHelper9000 revisited", 1),
            ("Traefik Series", 1)
        });
    }

    [Fact]
    public void Import_MapsSheetNamesToSeriesNamesViaDashboard()
    {
        using var workbook = BuildWorkbook();

        ScheduleXlsxImporter.Import(workbook, _db);

        var repository = new ScheduleRepository(_db);
        repository.FindSeries("BlogHelper9000 revisited").Should().NotBeNull();
        repository.FindSeries("Schedule").Should().BeNull();
    }

    [Fact]
    public void Import_ReadsAllEntryColumns()
    {
        using var workbook = BuildWorkbook();

        ScheduleXlsxImporter.Import(workbook, _db);

        var entry = new ScheduleRepository(_db)
            .FindEntryByFilename("traefik-in-the-homelab-one-proxy-for-everything.md");
        entry.Should().BeEquivalentTo(new
        {
            Position = 1,
            Week = 1,
            PublishDate = new DateOnly(2026, 7, 9),
            Topic = "Foundations",
            Tags = "traefik, homelab, docker",
            Published = false,
            Notes = "Series opener"
        }, options => options.ExcludingMissingMembers());
    }

    [Fact]
    public void Import_StoresBaselineMeta()
    {
        using var workbook = BuildWorkbook();

        ScheduleXlsxImporter.Import(workbook, _db);

        var repository = new ScheduleRepository(_db);
        repository.GetMeta("baseline_published_count").Should().Be("243");
        repository.GetMeta("baseline_date").Should().Be("2026-07-04");
    }

    [Fact]
    public void Import_TicksEntriesMarkedPublished()
    {
        using var workbook = BuildWorkbook();

        ScheduleXlsxImporter.Import(workbook, _db);

        new ScheduleRepository(_db)
            .FindEntryByFilename("bloghelper9000-four-years-on.md")!
            .Published.Should().BeTrue();
    }

    public void Dispose() => _db.Dispose();
}
