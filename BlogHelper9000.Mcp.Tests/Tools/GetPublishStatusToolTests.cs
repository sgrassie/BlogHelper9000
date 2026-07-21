using BlogHelper9000.Core.Models;
using BlogHelper9000.Core.Services;
using BlogHelper9000.Mcp.Tools;
using FluentAssertions;
using NSubstitute;

namespace BlogHelper9000.Mcp.Tests.Tools;

public class GetPublishStatusToolTests
{
    private readonly IDeployStateService _deployService = Substitute.For<IDeployStateService>();

    [Fact]
    public void GetPublishStatus_NotARepo_IsOkWithExplanatorySummary()
    {
        _deployService.GetDeployState().Returns(new DeployState(false, false, [], 0, [], []));

        var result = GetPublishStatusTool.GetPublishStatus(_deployService);

        result.Success.Should().BeTrue();
        result.Data!.IsGitRepo.Should().BeFalse();
        result.Data.Summary.Should().Be("The blog directory is not a git repository.");
    }

    [Fact]
    public void GetPublishStatus_CleanAndPushed_ReportsFullyDeployed()
    {
        _deployService.GetDeployState().Returns(new DeployState(true, true, [], 0, [], []));

        var result = GetPublishStatusTool.GetPublishStatus(_deployService);

        result.Success.Should().BeTrue();
        result.Data!.Summary.Should().Be("Everything is committed and pushed — the blog is fully deployed.");
    }

    [Fact]
    public void GetPublishStatus_Dirty_SummarisesCounts()
    {
        _deployService.GetDeployState().Returns(new DeployState(
            true, true,
            ["_posts/2026/a.md", "_posts/2026/b.md", "README.md"],
            2,
            ["_drafts/c.md", "x.md"],
            ["_posts/2026/a.md", "_posts/2026/b.md", "_drafts/c.md", "x.md"]));

        var result = GetPublishStatusTool.GetPublishStatus(_deployService);

        result.Success.Should().BeTrue();
        result.Data!.UncommittedFiles.Should().HaveCount(3);
        result.Data.UnpushedCommits.Should().Be(2);
        result.Data.PendingPostFiles.Should().HaveCount(4);
        result.Data.Summary.Should().Be(
            "3 uncommitted change(s) and 2 unpushed commit(s); 4 publish-related file(s) pending deploy.");
    }

    [Fact]
    public void GetPublishStatus_NoUpstream_MentionsItInSummary()
    {
        _deployService.GetDeployState().Returns(new DeployState(true, false, ["a.md"], 0, [], []));

        var result = GetPublishStatusTool.GetPublishStatus(_deployService);

        result.Success.Should().BeTrue();
        result.Data!.HasUpstream.Should().BeFalse();
        result.Data.Summary.Should().ContainEquivalentOf("upstream");
    }

    [Fact]
    public void GetPublishStatus_CleanButNoUpstream_ReportsBothInSummary()
    {
        _deployService.GetDeployState().Returns(new DeployState(true, false, [], 0, [], []));

        var result = GetPublishStatusTool.GetPublishStatus(_deployService);

        result.Success.Should().BeTrue();
        result.Data!.HasUpstream.Should().BeFalse();
        result.Data.Summary.Should().Be("No uncommitted changes. No upstream is configured.");
    }
}
