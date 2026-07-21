using BlogHelper9000.Core.Scheduling;
using BlogHelper9000.Mcp.Tools;
using FluentAssertions;
using NSubstitute;

namespace BlogHelper9000.Mcp.Tests.Tools;

public class RebaseSeriesToolTests
{
    private readonly IScheduleService _scheduleService = Substitute.For<IScheduleService>();

    private static RebaseSeriesResult Rebased(int moved = 3) => new(
        RebaseSeriesOutcome.Rebased, moved, new DateOnly(2026, 6, 30), new DateOnly(2026, 7, 28));

    public RebaseSeriesToolTests() => _scheduleService.DatabaseExists.Returns(true);

    [Fact]
    public void RebaseSeries_ExplicitNewStartDate_CallsRebaseAndMapsResult()
    {
        _scheduleService.RebaseSeries("FootballData", new DateOnly(2026, 7, 28))
            .Returns(Rebased());

        var result = RebaseSeriesTool.RebaseSeries(_scheduleService, "FootballData", newStartDate: "2026-07-28");

        result.Success.Should().BeTrue();
        result.Data!.Series.Should().Be("FootballData");
        result.Data.EntriesMoved.Should().Be(3);
        result.Data.OldStartDate.Should().Be(new DateOnly(2026, 6, 30));
        result.Data.NewStartDate.Should().Be(new DateOnly(2026, 7, 28));
    }

    [Fact]
    public void RebaseSeries_NoDateOrDelta_DefaultsToNextWeekdayRebase()
    {
        _scheduleService.RebaseSeries("FootballData", null).Returns(Rebased());

        var result = RebaseSeriesTool.RebaseSeries(_scheduleService, "FootballData");

        result.Success.Should().BeTrue();
        _scheduleService.Received(1).RebaseSeries("FootballData", null);
    }

    [Fact]
    public void RebaseSeries_Days_CallsShift()
    {
        _scheduleService.ShiftSeries("FootballData", 21).Returns(Rebased());

        var result = RebaseSeriesTool.RebaseSeries(_scheduleService, "FootballData", days: 21);

        result.Success.Should().BeTrue();
        _scheduleService.Received(1).ShiftSeries("FootballData", 21);
    }

    [Fact]
    public void RebaseSeries_Weeks_CallsShiftWithSevenTimesWeeks()
    {
        _scheduleService.ShiftSeries("Traefik in the homelab", 14).Returns(Rebased());

        var result = RebaseSeriesTool.RebaseSeries(_scheduleService, "Traefik in the homelab", weeks: 2);

        result.Success.Should().BeTrue();
        _scheduleService.Received(1).ShiftSeries("Traefik in the homelab", 14);
    }

    [Fact]
    public void RebaseSeries_NegativeDays_IsAllowed()
    {
        _scheduleService.ShiftSeries("FootballData", -7).Returns(Rebased());

        RebaseSeriesTool.RebaseSeries(_scheduleService, "FootballData", days: -7)
            .Success.Should().BeTrue();
    }

    [Fact]
    public void RebaseSeries_NewStartDateAndDays_FailsWithoutCallingService()
    {
        var result = RebaseSeriesTool.RebaseSeries(
            _scheduleService, "FootballData", newStartDate: "2026-07-28", days: 21);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("newStartDate");
        AssertServiceNotCalled();
    }

    [Fact]
    public void RebaseSeries_DaysAndWeeks_FailsWithoutCallingService()
    {
        var result = RebaseSeriesTool.RebaseSeries(_scheduleService, "FootballData", days: 21, weeks: 3);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("days").And.Contain("weeks");
        AssertServiceNotCalled();
    }

    [Fact]
    public void RebaseSeries_BadNewStartDate_FailsWithoutCallingService()
    {
        var result = RebaseSeriesTool.RebaseSeries(_scheduleService, "FootballData", newStartDate: "not-a-date");

        result.Success.Should().BeFalse();
        AssertServiceNotCalled();
    }

    [Theory]
    [InlineData(0, null)]
    [InlineData(null, 0)]
    public void RebaseSeries_ZeroDelta_FailsWithoutCallingService(int? days, int? weeks)
    {
        var result = RebaseSeriesTool.RebaseSeries(_scheduleService, "FootballData", days: days, weeks: weeks);

        result.Success.Should().BeFalse();
        AssertServiceNotCalled();
    }

    [Fact]
    public void RebaseSeries_SeriesNotFound_FailsWithGuidance()
    {
        _scheduleService.RebaseSeries("Ghost", null)
            .Returns(new RebaseSeriesResult(RebaseSeriesOutcome.SeriesNotFound, 0, null, null));

        var result = RebaseSeriesTool.RebaseSeries(_scheduleService, "Ghost");

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("list_series");
    }

    [Fact]
    public void RebaseSeries_NothingToMove_Fails()
    {
        _scheduleService.RebaseSeries("FootballData", null)
            .Returns(new RebaseSeriesResult(RebaseSeriesOutcome.NothingToMove, 0, null, null));

        RebaseSeriesTool.RebaseSeries(_scheduleService, "FootballData")
            .Success.Should().BeFalse();
    }

    [Fact]
    public void RebaseSeries_WhenNoDatabase_FailsWithGuidance()
    {
        _scheduleService.DatabaseExists.Returns(false);

        RebaseSeriesTool.RebaseSeries(_scheduleService, "FootballData")
            .Error.Should().Contain("schedule-import");
    }

    private void AssertServiceNotCalled()
    {
        _scheduleService.DidNotReceiveWithAnyArgs().RebaseSeries(default!);
        _scheduleService.DidNotReceiveWithAnyArgs().ShiftSeries(default!, default);
    }
}
