using BlogHelper9000.Core.Scheduling;
using BlogHelper9000.Mcp.Tools;
using FluentAssertions;
using NSubstitute;

namespace BlogHelper9000.Mcp.Tests.Tools;

public class SeriesAdminToolsTests
{
    private readonly IScheduleService _scheduleService = Substitute.For<IScheduleService>();

    private static ScheduleEntry Entry(string series = "Series", int position = 1) =>
        new(1, series, position, 2, new DateOnly(2026, 7, 7), "Topic", "Title", "title.md",
            "csharp", "New", false, null, null);

    public SeriesAdminToolsTests() => _scheduleService.DatabaseExists.Returns(true);

    // rename_series

    [Fact]
    public void RenameSeries_Renamed_Succeeds()
    {
        _scheduleService.RenameSeries("Old", "New").Returns(RenameSeriesOutcome.Renamed);

        var result = SeriesAdminTools.RenameSeries(_scheduleService, "Old", "New");

        result.Success.Should().BeTrue();
        result.Data!.OldName.Should().Be("Old");
        result.Data.NewName.Should().Be("New");
    }

    [Fact]
    public void RenameSeries_NotFound_Fails()
    {
        _scheduleService.RenameSeries("Ghost", "New").Returns(RenameSeriesOutcome.NotFound);

        SeriesAdminTools.RenameSeries(_scheduleService, "Ghost", "New").Success.Should().BeFalse();
    }

    [Fact]
    public void RenameSeries_NameTaken_Fails()
    {
        _scheduleService.RenameSeries("Old", "Existing").Returns(RenameSeriesOutcome.NameTaken);

        SeriesAdminTools.RenameSeries(_scheduleService, "Old", "Existing").Success.Should().BeFalse();
    }

    [Fact]
    public void RenameSeries_WhenNoDatabase_FailsWithGuidance()
    {
        _scheduleService.DatabaseExists.Returns(false);

        SeriesAdminTools.RenameSeries(_scheduleService, "Old", "New").Error.Should().Contain("schedule-import");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void RenameSeries_EmptyOrWhitespaceNewName_FailsWithoutCallingService(string newName)
    {
        var result = SeriesAdminTools.RenameSeries(_scheduleService, "Old", newName);

        result.Success.Should().BeFalse();
        _scheduleService.DidNotReceiveWithAnyArgs().RenameSeries(default!, default!);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void RenameSeries_EmptyOrWhitespaceOldName_FailsWithoutCallingService(string oldName)
    {
        var result = SeriesAdminTools.RenameSeries(_scheduleService, oldName, "New");

        result.Success.Should().BeFalse();
        _scheduleService.DidNotReceiveWithAnyArgs().RenameSeries(default!, default!);
    }

    // delete_series

    [Fact]
    public void DeleteSeries_NotFound_Fails()
    {
        _scheduleService.ListSeries().Returns([]);

        SeriesAdminTools.DeleteSeries(_scheduleService, "Ghost").Success.Should().BeFalse();
    }

    [Fact]
    public void DeleteSeries_NotEmpty_FailsRegardlessOfDryRun()
    {
        _scheduleService.ListSeries().Returns([new SeriesInfo(1, "Series", 1)]);
        _scheduleService.GetSeriesEntries("Series").Returns([Entry()]);

        SeriesAdminTools.DeleteSeries(_scheduleService, "Series", dryRun: true).Success.Should().BeFalse();
        SeriesAdminTools.DeleteSeries(_scheduleService, "Series", dryRun: false).Success.Should().BeFalse();
        _scheduleService.DidNotReceive().DeleteSeries(Arg.Any<string>());
    }

    [Fact]
    public void DeleteSeries_EmptyDryRun_PreviewsWithoutDeleting()
    {
        _scheduleService.ListSeries().Returns([new SeriesInfo(1, "Series", 1)]);
        _scheduleService.GetSeriesEntries("Series").Returns([]);

        var result = SeriesAdminTools.DeleteSeries(_scheduleService, "Series");

        result.Success.Should().BeTrue();
        result.Data!.DryRun.Should().BeTrue();
        result.Data.Deleted.Should().BeFalse();
        _scheduleService.DidNotReceive().DeleteSeries(Arg.Any<string>());
    }

    [Fact]
    public void DeleteSeries_EmptyRealRun_Deletes()
    {
        _scheduleService.ListSeries().Returns([new SeriesInfo(1, "Series", 1)]);
        _scheduleService.GetSeriesEntries("Series").Returns([]);
        _scheduleService.DeleteSeries("Series").Returns(DeleteSeriesOutcome.Deleted);

        var result = SeriesAdminTools.DeleteSeries(_scheduleService, "Series", dryRun: false);

        result.Success.Should().BeTrue();
        result.Data!.Deleted.Should().BeTrue();
        result.Data.DryRun.Should().BeFalse();
        _scheduleService.Received(1).DeleteSeries("Series");
    }

    [Fact]
    public void DeleteSeries_WhenNoDatabase_FailsWithGuidance()
    {
        _scheduleService.DatabaseExists.Returns(false);

        SeriesAdminTools.DeleteSeries(_scheduleService, "Series").Error.Should().Contain("schedule-import");
    }

    // set_series_cadence

    [Fact]
    public void SetSeriesCadence_Set_Succeeds()
    {
        _scheduleService.SetSeriesCadence("Series", DayOfWeek.Monday, new DateOnly(2026, 7, 6))
            .Returns(SetCadenceOutcome.Set);

        var result = SeriesAdminTools.SetSeriesCadence(_scheduleService, "Series", "Monday", "2026-07-06");

        result.Success.Should().BeTrue();
        result.Data!.Series.Should().Be("Series");
        result.Data.CadenceDay.Should().Be("Monday");
        result.Data.CadenceStart.Should().Be("2026-07-06");
    }

    [Fact]
    public void SetSeriesCadence_IsCaseInsensitiveForDayOfWeek()
    {
        _scheduleService.SetSeriesCadence("Series", DayOfWeek.Monday, new DateOnly(2026, 7, 6))
            .Returns(SetCadenceOutcome.Set);

        var result = SeriesAdminTools.SetSeriesCadence(_scheduleService, "Series", "monday", "2026-07-06");

        result.Success.Should().BeTrue();
    }

    [Fact]
    public void SetSeriesCadence_SeriesNotFound_Fails()
    {
        _scheduleService.SetSeriesCadence("Ghost", DayOfWeek.Monday, new DateOnly(2026, 7, 6))
            .Returns(SetCadenceOutcome.SeriesNotFound);

        SeriesAdminTools.SetSeriesCadence(_scheduleService, "Ghost", "Monday", "2026-07-06")
            .Success.Should().BeFalse();
    }

    [Fact]
    public void SetSeriesCadence_DayMismatch_Fails()
    {
        _scheduleService.SetSeriesCadence("Series", DayOfWeek.Monday, new DateOnly(2026, 7, 7))
            .Returns(SetCadenceOutcome.DayMismatch);

        SeriesAdminTools.SetSeriesCadence(_scheduleService, "Series", "Monday", "2026-07-07")
            .Success.Should().BeFalse();
    }

    [Fact]
    public void SetSeriesCadence_BadDayOfWeek_FailsWithoutCallingService()
    {
        var result = SeriesAdminTools.SetSeriesCadence(_scheduleService, "Series", "Funday", "2026-07-06");

        result.Success.Should().BeFalse();
        _scheduleService.DidNotReceiveWithAnyArgs().SetSeriesCadence(default!, default, default);
    }

    [Fact]
    public void SetSeriesCadence_BadStartDate_FailsWithoutCallingService()
    {
        var result = SeriesAdminTools.SetSeriesCadence(_scheduleService, "Series", "Monday", "not-a-date");

        result.Success.Should().BeFalse();
        _scheduleService.DidNotReceiveWithAnyArgs().SetSeriesCadence(default!, default, default);
    }

    [Fact]
    public void SetSeriesCadence_WhenNoDatabase_FailsWithGuidance()
    {
        _scheduleService.DatabaseExists.Returns(false);

        SeriesAdminTools.SetSeriesCadence(_scheduleService, "Series", "Monday", "2026-07-06")
            .Error.Should().Contain("schedule-import");
    }
}
