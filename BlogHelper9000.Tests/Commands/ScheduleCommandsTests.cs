using BlogHelper9000.Commands;
using BlogHelper9000.Core.Scheduling;
using BlogHelper9000.Reporters;
using BlogHelper9000.TestHelpers;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace BlogHelper9000.Tests.Commands;

public class ScheduleCommandsTests
{
    private readonly IScheduleService _scheduleService = Substitute.For<IScheduleService>();

    [Fact]
    public async Task ScheduleList_WhenNoDatabase_ReportsErrorAndDoesNotQuerySeries()
    {
        _scheduleService.DatabaseExists.Returns(false);
        var logger = Substitute.For<MockLogger<ScheduleListCommand.Handler>>();
        var sut = new ScheduleListCommand.Handler(logger, _scheduleService, null!);

        await sut.Handle(new ScheduleListCommand(), CancellationToken.None);

        _scheduleService.DidNotReceive().GetDashboard();
        logger.Received().Log(LogLevel.Error,
            "No schedule database found — run 'bloghelper schedule-import <xlsx>' first");
    }

    [Fact]
    public async Task ScheduleShow_QueriesTheNamedSeries()
    {
        _scheduleService.DatabaseExists.Returns(true);
        _scheduleService.GetSeriesEntries("FootballData").Returns([
            new ScheduleEntry(1, "FootballData", 1, 1, null, null, "A post", "a-post.md",
                null, null, false, null, null)
        ]);
        var logger = Substitute.For<MockLogger<ScheduleShowCommand.Handler>>();
        var sut = new ScheduleShowCommand.Handler(logger, _scheduleService, new ScheduleReporter());

        await sut.Handle(new ScheduleShowCommand { Series = "FootballData" }, CancellationToken.None);

        _scheduleService.Received(1).GetSeriesEntries("FootballData");
    }

    [Fact]
    public async Task ScheduleShow_UnknownSeries_ReportsError()
    {
        _scheduleService.DatabaseExists.Returns(true);
        _scheduleService.GetSeriesEntries("nope").Returns([]);
        var logger = Substitute.For<MockLogger<ScheduleShowCommand.Handler>>();
        var sut = new ScheduleShowCommand.Handler(logger, _scheduleService, null!);

        await sut.Handle(new ScheduleShowCommand { Series = "nope" }, CancellationToken.None);

        logger.Received().Log(LogLevel.Error,
            "No series named 'nope' (or it has no entries) — try 'bloghelper schedule-list'");
    }

    [Fact]
    public async Task ScheduleStats_QueriesTheDashboard()
    {
        _scheduleService.DatabaseExists.Returns(true);
        _scheduleService.GetDashboard().Returns(
            new ScheduleDashboard(243, new DateOnly(2026, 7, 4), 0, 243, 0, 0, 0, []));
        var logger = Substitute.For<MockLogger<ScheduleStatsCommand.Handler>>();
        var sut = new ScheduleStatsCommand.Handler(logger, _scheduleService, new ScheduleReporter());

        await sut.Handle(new ScheduleStatsCommand(), CancellationToken.None);

        _scheduleService.Received(1).GetDashboard();
    }
}
