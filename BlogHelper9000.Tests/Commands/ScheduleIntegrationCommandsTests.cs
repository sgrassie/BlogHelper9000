using BlogHelper9000.Commands;
using BlogHelper9000.Core.Scheduling;
using BlogHelper9000.Core.Services;
using BlogHelper9000.TestHelpers;
using NSubstitute;

namespace BlogHelper9000.Tests.Commands;

public class ScheduleIntegrationCommandsTests
{
    private readonly IBlogService _blogService = Substitute.For<IBlogService>();
    private readonly IScheduleService _scheduleService = Substitute.For<IScheduleService>();

    [Fact]
    public async Task Add_WithSeriesOption_AddsCreatedDraftToSeries()
    {
        _blogService.AddPost("My Post", true, false, false, null, Arg.Any<IReadOnlyList<string>>())
            .Returns("/blog/_drafts/my-post.md");
        var logger = Substitute.For<MockLogger<AddCommand.Handler>>();
        var sut = new AddCommand.Handler(logger, _blogService, _scheduleService);

        await sut.Handle(new AddCommand
        {
            Title = "My Post", Tags = "csharp", IsDraft = true, Series = "FootballData", Week = 3
        }, CancellationToken.None);

        _scheduleService.Received(1).AddToSeries("FootballData", "My Post", "my-post.md",
            week: 3, publishDate: null, tags: "csharp", notes: null);
    }

    [Fact]
    public async Task Add_WithoutSeriesOption_DoesNotTouchTheSchedule()
    {
        _blogService.AddPost(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<bool>(),
            Arg.Any<string?>(), Arg.Any<IReadOnlyList<string>?>()).Returns("/blog/_drafts/my-post.md");
        var logger = Substitute.For<MockLogger<AddCommand.Handler>>();
        var sut = new AddCommand.Handler(logger, _blogService, _scheduleService);

        await sut.Handle(new AddCommand { Title = "My Post", Tags = "csharp" }, CancellationToken.None);

        _scheduleService.DidNotReceiveWithAnyArgs().AddToSeries(default!, default!, default!);
    }

    [Fact]
    public async Task Publish_OnSuccess_TicksTheScheduleEntry()
    {
        _blogService.PublishPost("my-post.md").Returns("/blog/_posts/2026/2026-07-06-my-post.md");
        _scheduleService.MarkPublished("my-post.md").Returns(MarkPublishedOutcome.Marked);
        var logger = Substitute.For<MockLogger<PublishCommand.Handler>>();
        var sut = new PublishCommand.Handler(logger, _blogService, _scheduleService);

        await sut.Handle(new PublishCommand { Post = "my-post.md" }, CancellationToken.None);

        _scheduleService.Received(1).MarkPublished("my-post.md");
    }

    [Fact]
    public async Task Publish_OnFailure_DoesNotTouchTheSchedule()
    {
        _blogService.PublishPost("missing.md").Returns((string?)null);
        var logger = Substitute.For<MockLogger<PublishCommand.Handler>>();
        var sut = new PublishCommand.Handler(logger, _blogService, _scheduleService);

        await sut.Handle(new PublishCommand { Post = "missing.md" }, CancellationToken.None);

        _scheduleService.DidNotReceiveWithAnyArgs().MarkPublished(default!);
    }

    [Fact]
    public async Task ScheduleMark_TicksAnEntry_WithOptionalBackdate()
    {
        _scheduleService.MarkPublished("my-post.md", new DateOnly(2026, 7, 1), false)
            .Returns(MarkPublishedOutcome.Marked);
        var logger = Substitute.For<MockLogger<ScheduleMarkCommand.Handler>>();
        var sut = new ScheduleMarkCommand.Handler(logger, _scheduleService);

        await sut.Handle(new ScheduleMarkCommand { Post = "my-post.md", Date = "2026-07-01" }, CancellationToken.None);

        _scheduleService.Received(1).MarkPublished("my-post.md", new DateOnly(2026, 7, 1), false);
    }

    [Fact]
    public async Task ScheduleMark_WithUnmark_ForwardsTheFlag()
    {
        _scheduleService.MarkPublished("my-post.md", null, true).Returns(MarkPublishedOutcome.Unmarked);
        var logger = Substitute.For<MockLogger<ScheduleMarkCommand.Handler>>();
        var sut = new ScheduleMarkCommand.Handler(logger, _scheduleService);

        await sut.Handle(new ScheduleMarkCommand { Post = "my-post.md", Unmark = true }, CancellationToken.None);

        _scheduleService.Received(1).MarkPublished("my-post.md", null, true);
    }
}
