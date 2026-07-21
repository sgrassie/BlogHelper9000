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
    public async Task ScheduleRebase_WhenNoDatabase_ReportsErrorAndDoesNotCallService()
    {
        _scheduleService.DatabaseExists.Returns(false);
        var logger = Substitute.For<MockLogger<ScheduleRebaseCommand.Handler>>();
        var sut = new ScheduleRebaseCommand.Handler(logger, _scheduleService);

        await sut.Handle(new ScheduleRebaseCommand { Series = "FootballData" }, CancellationToken.None);

        _scheduleService.DidNotReceiveWithAnyArgs().RebaseSeries(default!);
        logger.Received().Log(LogLevel.Error,
            "No schedule database found — run 'bloghelper schedule-import <xlsx>' first");
    }

    [Fact]
    public async Task ScheduleRebase_WithStartDate_RebasesAndReportsTheMove()
    {
        _scheduleService.DatabaseExists.Returns(true);
        _scheduleService.RebaseSeries("FootballData", new DateOnly(2026, 7, 28)).Returns(
            new RebaseSeriesResult(RebaseSeriesOutcome.Rebased, 104,
                new DateOnly(2026, 6, 30), new DateOnly(2026, 7, 28)));
        var logger = Substitute.For<MockLogger<ScheduleRebaseCommand.Handler>>();
        var sut = new ScheduleRebaseCommand.Handler(logger, _scheduleService);

        await sut.Handle(new ScheduleRebaseCommand { Series = "FootballData", Start = "2026-07-28" },
            CancellationToken.None);

        logger.Received().Log(LogLevel.Information,
            "Moved 104 entries in 'FootballData': 2026-06-30 -> 2026-07-28");
    }

    [Fact]
    public async Task ScheduleRebase_WithoutStartOrDelta_DefaultsToNextWeekdayRebase()
    {
        _scheduleService.DatabaseExists.Returns(true);
        _scheduleService.RebaseSeries("FootballData", null).Returns(
            new RebaseSeriesResult(RebaseSeriesOutcome.Rebased, 104,
                new DateOnly(2026, 6, 30), new DateOnly(2026, 7, 28)));
        var logger = Substitute.For<MockLogger<ScheduleRebaseCommand.Handler>>();
        var sut = new ScheduleRebaseCommand.Handler(logger, _scheduleService);

        await sut.Handle(new ScheduleRebaseCommand { Series = "FootballData" }, CancellationToken.None);

        _scheduleService.Received(1).RebaseSeries("FootballData", null);
    }

    [Fact]
    public async Task ScheduleRebase_WithDays_Shifts()
    {
        _scheduleService.DatabaseExists.Returns(true);
        _scheduleService.ShiftSeries("FootballData", 21).Returns(
            new RebaseSeriesResult(RebaseSeriesOutcome.Rebased, 104,
                new DateOnly(2026, 6, 30), new DateOnly(2026, 7, 21)));
        var logger = Substitute.For<MockLogger<ScheduleRebaseCommand.Handler>>();
        var sut = new ScheduleRebaseCommand.Handler(logger, _scheduleService);

        await sut.Handle(new ScheduleRebaseCommand { Series = "FootballData", Days = 21 },
            CancellationToken.None);

        _scheduleService.Received(1).ShiftSeries("FootballData", 21);
    }

    [Fact]
    public async Task ScheduleRebase_WithWeeks_ShiftsBySevenTimesWeeks()
    {
        _scheduleService.DatabaseExists.Returns(true);
        _scheduleService.ShiftSeries("Traefik in the homelab", 14).Returns(
            new RebaseSeriesResult(RebaseSeriesOutcome.Rebased, 26,
                new DateOnly(2026, 7, 2), new DateOnly(2026, 7, 16)));
        var logger = Substitute.For<MockLogger<ScheduleRebaseCommand.Handler>>();
        var sut = new ScheduleRebaseCommand.Handler(logger, _scheduleService);

        await sut.Handle(new ScheduleRebaseCommand { Series = "Traefik in the homelab", Weeks = 2 },
            CancellationToken.None);

        _scheduleService.Received(1).ShiftSeries("Traefik in the homelab", 14);
    }

    [Fact]
    public async Task ScheduleRebase_StartAndDelta_ReportsErrorAndDoesNotCallService()
    {
        _scheduleService.DatabaseExists.Returns(true);
        var logger = Substitute.For<MockLogger<ScheduleRebaseCommand.Handler>>();
        var sut = new ScheduleRebaseCommand.Handler(logger, _scheduleService);

        await sut.Handle(new ScheduleRebaseCommand { Series = "FootballData", Start = "2026-07-28", Days = 21 },
            CancellationToken.None);

        _scheduleService.DidNotReceiveWithAnyArgs().RebaseSeries(default!);
        _scheduleService.DidNotReceiveWithAnyArgs().ShiftSeries(default!, default);
        logger.Received().Log(LogLevel.Error,
            "--start and --days/--weeks are contradictory — rebase to a date or shift by a delta, not both");
    }

    [Fact]
    public async Task ScheduleRebase_BadStartDate_ReportsErrorAndDoesNotCallService()
    {
        _scheduleService.DatabaseExists.Returns(true);
        var logger = Substitute.For<MockLogger<ScheduleRebaseCommand.Handler>>();
        var sut = new ScheduleRebaseCommand.Handler(logger, _scheduleService);

        await sut.Handle(new ScheduleRebaseCommand { Series = "FootballData", Start = "not-a-date" },
            CancellationToken.None);

        _scheduleService.DidNotReceiveWithAnyArgs().RebaseSeries(default!);
        logger.Received().Log(LogLevel.Error, "'not-a-date' is not a valid yyyy-MM-dd date");
    }

    [Fact]
    public async Task ScheduleRebase_UnknownSeries_ReportsError()
    {
        _scheduleService.DatabaseExists.Returns(true);
        _scheduleService.RebaseSeries("Ghost", null).Returns(
            new RebaseSeriesResult(RebaseSeriesOutcome.SeriesNotFound, 0, null, null));
        var logger = Substitute.For<MockLogger<ScheduleRebaseCommand.Handler>>();
        var sut = new ScheduleRebaseCommand.Handler(logger, _scheduleService);

        await sut.Handle(new ScheduleRebaseCommand { Series = "Ghost" }, CancellationToken.None);

        logger.Received().Log(LogLevel.Error,
            "No series named 'Ghost' — try 'bloghelper schedule-list'");
    }

    [Fact]
    public async Task ScheduleRebase_NothingToMove_Warns()
    {
        _scheduleService.DatabaseExists.Returns(true);
        _scheduleService.RebaseSeries("FootballData", null).Returns(
            new RebaseSeriesResult(RebaseSeriesOutcome.NothingToMove, 0, null, null));
        var logger = Substitute.For<MockLogger<ScheduleRebaseCommand.Handler>>();
        var sut = new ScheduleRebaseCommand.Handler(logger, _scheduleService);

        await sut.Handle(new ScheduleRebaseCommand { Series = "FootballData" }, CancellationToken.None);

        logger.Received().Log(LogLevel.Warning,
            "'FootballData' has no unpublished entries with a publish date — nothing to rebase");
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
