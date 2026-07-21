using BlogHelper9000.Core.Models;
using BlogHelper9000.Core.Services;
using BlogHelper9000.Mcp.Tools;
using FluentAssertions;
using NSubstitute;

namespace BlogHelper9000.Mcp.Tests.Tools;

public class DeployToolTests
{
    private readonly IDeployStateService _deployService = Substitute.For<IDeployStateService>();

    [Fact]
    public void Deploy_DefaultsToDryRun()
    {
        _deployService.Deploy(true, null).Returns(
            new DeployResult(DeployOutcome.Deployed, true, ["_posts/2026/a.md"], "publish: a.md", false, null));

        DeployTool.Deploy(_deployService);

        _deployService.Received(1).Deploy(dryRun: true, message: null);
    }

    [Fact]
    public void Deploy_RealRun_ForwardsDryRunFalse()
    {
        _deployService.Deploy(false, null).Returns(
            new DeployResult(DeployOutcome.Deployed, false, ["_posts/2026/a.md"], "publish: a.md", true, null));

        var result = DeployTool.Deploy(_deployService, dryRun: false);

        _deployService.Received(1).Deploy(dryRun: false, message: null);
        result.Success.Should().BeTrue();
        result.Data!.Pushed.Should().BeTrue();
        result.Data.DryRun.Should().BeFalse();
        result.Data.Outcome.Should().Be("Deployed");
    }

    [Fact]
    public void Deploy_MessageOverride_IsForwarded()
    {
        _deployService.Deploy(false, "custom message").Returns(
            new DeployResult(DeployOutcome.Deployed, false, ["_posts/2026/a.md"], "custom message", true, null));

        DeployTool.Deploy(_deployService, dryRun: false, message: "custom message");

        _deployService.Received(1).Deploy(dryRun: false, message: "custom message");
    }

    [Fact]
    public void Deploy_NothingToDeploy_IsOkNotFail()
    {
        _deployService.Deploy(true, null).Returns(
            new DeployResult(DeployOutcome.NothingToDeploy, true, [], null, false, null));

        var result = DeployTool.Deploy(_deployService);

        result.Success.Should().BeTrue();
        result.Data!.Outcome.Should().Be("NothingToDeploy");
    }

    [Fact]
    public void Deploy_NotARepo_Fails()
    {
        _deployService.Deploy(true, null).Returns(
            new DeployResult(DeployOutcome.NotARepo, true, [], null, false, null));

        var result = DeployTool.Deploy(_deployService);

        result.Success.Should().BeFalse();
    }

    [Fact]
    public void Deploy_NoUpstream_Fails()
    {
        _deployService.Deploy(true, null).Returns(
            new DeployResult(DeployOutcome.NoUpstream, true, [], null, false, null));

        var result = DeployTool.Deploy(_deployService);

        result.Success.Should().BeFalse();
    }

    [Fact]
    public void Deploy_GitFailed_FailsWithStderrInMessage()
    {
        _deployService.Deploy(false, null).Returns(
            new DeployResult(DeployOutcome.GitFailed, false, ["_posts/2026/a.md"], "publish: a.md", false,
                "fatal: could not read Username for 'https://example.com'"));

        var result = DeployTool.Deploy(_deployService, dryRun: false);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("could not read Username");
    }

    [Fact]
    public void Deploy_NotARepo_and_NoUpstream_AreDistinctFailures()
    {
        _deployService.Deploy(true, null).Returns(
            new DeployResult(DeployOutcome.NotARepo, true, [], null, false, null));
        var notARepo = DeployTool.Deploy(_deployService);

        _deployService.Deploy(true, null).Returns(
            new DeployResult(DeployOutcome.NoUpstream, true, [], null, false, null));
        var noUpstream = DeployTool.Deploy(_deployService);

        notARepo.Error.Should().NotBe(noUpstream.Error);
    }
}
