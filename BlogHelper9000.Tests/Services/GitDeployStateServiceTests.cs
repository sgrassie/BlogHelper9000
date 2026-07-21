using BlogHelper9000.Core;
using BlogHelper9000.Core.Models;
using BlogHelper9000.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace BlogHelper9000.Tests.Services;

public class GitDeployStateServiceTests
{
    private const string BaseDirectory = "/blog";

    private readonly IOptions<BlogHelperOptions> _options = Options.Create(new BlogHelperOptions
    {
        BaseDirectory = BaseDirectory
    });

    private GitDeployStateService CreateSut(RecordingProcessRunner runner) =>
        new(runner, _options, NullLogger<GitDeployStateService>.Instance);

    private static void SeedCleanUpstream(RecordingProcessRunner runner, string revListCount = "0", string diffOutput = "")
    {
        runner.EnqueueSuccess("true\n");
        runner.EnqueueSuccess(string.Empty);
        runner.EnqueueSuccess($"{revListCount}\n");
        runner.EnqueueSuccess(diffOutput);
    }

    // ----- GetDeployState -----

    [Fact]
    public void GetDeployState_Should_Report_Clean_Repo_With_Upstream_And_Nothing_Pending()
    {
        var runner = new RecordingProcessRunner();
        SeedCleanUpstream(runner);
        var sut = CreateSut(runner);

        var state = sut.GetDeployState();

        state.IsGitRepo.Should().BeTrue();
        state.HasUpstream.Should().BeTrue();
        state.UncommittedFiles.Should().BeEmpty();
        state.UnpushedCommits.Should().Be(0);
        state.UnpushedFiles.Should().BeEmpty();
        state.PendingPostFiles.Should().BeEmpty();

        runner.Invocations.Should().HaveCount(4);
        runner.Invocations[0].Arguments.Should().Be("rev-parse --is-inside-work-tree");
        runner.Invocations[0].WorkingDirectory.Should().Be(BaseDirectory);
        runner.Invocations[1].Arguments.Should().Be("status --porcelain=v1");
        runner.Invocations[2].Arguments.Should().Be("rev-list --count @{upstream}..HEAD");
        runner.Invocations[3].Arguments.Should().Be("diff --name-only @{upstream}..HEAD");
        runner.Invocations.Should().OnlyContain(i => i.FileName == "git");
    }

    [Fact]
    public void GetDeployState_Should_Report_Dirty_Files_And_Filter_PendingPostFiles_To_Publish_Prefixes()
    {
        var runner = new RecordingProcessRunner();
        runner.EnqueueSuccess("true\n");
        runner.EnqueueSuccess(
            " M _posts/2026/2026-07-21-x.md\n" +
            "?? _drafts/y.md\n" +
            " M README.md\n");
        runner.EnqueueSuccess("0\n");
        runner.EnqueueSuccess(string.Empty);
        var sut = CreateSut(runner);

        var state = sut.GetDeployState();

        state.UncommittedFiles.Should().Equal("_posts/2026/2026-07-21-x.md", "_drafts/y.md", "README.md");
        state.PendingPostFiles.Should().Equal("_posts/2026/2026-07-21-x.md", "_drafts/y.md");
    }

    [Fact]
    public void GetDeployState_Should_Use_NewPath_For_Rename_Lines()
    {
        var runner = new RecordingProcessRunner();
        runner.EnqueueSuccess("true\n");
        runner.EnqueueSuccess("R  old.md -> _drafts/new.md\n");
        runner.EnqueueSuccess("0\n");
        runner.EnqueueSuccess(string.Empty);
        var sut = CreateSut(runner);

        var state = sut.GetDeployState();

        state.UncommittedFiles.Should().Equal("_drafts/new.md");
    }

    [Fact]
    public void GetDeployState_Should_Strip_Quotes_Git_Adds_For_Special_Characters()
    {
        var runner = new RecordingProcessRunner();
        runner.EnqueueSuccess("true\n");
        runner.EnqueueSuccess("?? \"_drafts/qu\\\"ote.md\"\n");
        runner.EnqueueSuccess("0\n");
        runner.EnqueueSuccess(string.Empty);
        var sut = CreateSut(runner);

        var state = sut.GetDeployState();

        state.UncommittedFiles.Should().ContainSingle()
            .Which.Should().Be("_drafts/qu\\\"ote.md");
    }

    [Fact]
    public void GetDeployState_Should_Report_No_Upstream_But_Keep_Uncommitted_Info()
    {
        var runner = new RecordingProcessRunner();
        runner.EnqueueSuccess("true\n");
        runner.EnqueueSuccess("?? _drafts/y.md\n");
        runner.Enqueue(new ProcessResult(128, string.Empty, "fatal: no upstream configured for branch 'main'\n", false));
        var sut = CreateSut(runner);

        var state = sut.GetDeployState();

        state.IsGitRepo.Should().BeTrue();
        state.HasUpstream.Should().BeFalse();
        state.UncommittedFiles.Should().Equal("_drafts/y.md");
        state.UnpushedCommits.Should().Be(0);
        state.UnpushedFiles.Should().BeEmpty();

        // diff should never be invoked without an upstream to diff against
        runner.Invocations.Should().HaveCount(3);
    }

    [Fact]
    public void GetDeployState_Should_Report_NotARepo_When_RevParse_Fails()
    {
        var runner = new RecordingProcessRunner();
        runner.Enqueue(new ProcessResult(128, string.Empty, "fatal: not a git repository\n", false));
        var sut = CreateSut(runner);

        var state = sut.GetDeployState();

        state.IsGitRepo.Should().BeFalse();
        state.HasUpstream.Should().BeFalse();
        state.UncommittedFiles.Should().BeEmpty();
        state.UnpushedCommits.Should().Be(0);
        state.UnpushedFiles.Should().BeEmpty();
        state.PendingPostFiles.Should().BeEmpty();

        runner.Invocations.Should().HaveCount(1);
    }

    [Fact]
    public void GetDeployState_Should_Report_NotARepo_When_RevParse_TimesOut()
    {
        var runner = new RecordingProcessRunner();
        runner.Enqueue(new ProcessResult(-1, string.Empty, string.Empty, true));
        var sut = CreateSut(runner);

        var state = sut.GetDeployState();

        state.IsGitRepo.Should().BeFalse();
        runner.Invocations.Should().HaveCount(1);
    }

    [Fact]
    public void GetDeployState_Should_Report_NotARepo_When_Git_Binary_Is_Missing()
    {
        var runner = new RecordingProcessRunner();
        runner.ThrowOnNextInvocation(new System.ComponentModel.Win32Exception("No such file or directory"));
        var sut = CreateSut(runner);

        var state = sut.GetDeployState();

        state.IsGitRepo.Should().BeFalse();
        state.UncommittedFiles.Should().BeEmpty();
        runner.Invocations.Should().HaveCount(1);
    }

    // ----- Deploy -----

    [Fact]
    public void Deploy_Should_Return_NotARepo_And_Run_Only_RevParse()
    {
        var runner = new RecordingProcessRunner();
        runner.Enqueue(new ProcessResult(128, string.Empty, "fatal: not a git repository\n", false));
        var sut = CreateSut(runner);

        var result = sut.Deploy(dryRun: true);

        result.Outcome.Should().Be(DeployOutcome.NotARepo);
        result.StagedFiles.Should().BeEmpty();
        result.CommitMessage.Should().BeNull();
        result.Pushed.Should().BeFalse();
        runner.Invocations.Should().HaveCount(1);
    }

    [Fact]
    public void Deploy_Should_Return_NoUpstream_When_Repo_Has_No_Upstream()
    {
        var runner = new RecordingProcessRunner();
        runner.EnqueueSuccess("true\n");
        runner.EnqueueSuccess("?? _drafts/y.md\n");
        runner.Enqueue(new ProcessResult(128, string.Empty, "fatal: no upstream configured\n", false));
        var sut = CreateSut(runner);

        var result = sut.Deploy(dryRun: true);

        result.Outcome.Should().Be(DeployOutcome.NoUpstream);
        runner.Invocations.Should().HaveCount(3);
        runner.Invocations.Should().NotContain(i => i.Arguments.StartsWith("add") || i.Arguments.StartsWith("commit") || i.Arguments.StartsWith("push"));
    }

    [Fact]
    public void Deploy_Should_Return_NothingToDeploy_When_Clean_And_Nothing_Unpushed()
    {
        var runner = new RecordingProcessRunner();
        SeedCleanUpstream(runner);
        var sut = CreateSut(runner);

        var result = sut.Deploy(dryRun: false);

        result.Outcome.Should().Be(DeployOutcome.NothingToDeploy);
        result.StagedFiles.Should().BeEmpty();
        result.CommitMessage.Should().BeNull();
        result.Pushed.Should().BeFalse();
        runner.Invocations.Should().HaveCount(4);
    }

    [Fact]
    public void Deploy_DryRun_Should_Report_Staged_Files_And_Generated_Message_Without_Mutating()
    {
        var runner = new RecordingProcessRunner();
        runner.EnqueueSuccess("true\n");
        runner.EnqueueSuccess(
            " M _posts/2026/2026-07-21-x.md\n" +
            "?? _drafts/y.md\n" +
            " M README.md\n");
        runner.EnqueueSuccess("0\n");
        runner.EnqueueSuccess(string.Empty);
        var sut = CreateSut(runner);

        var result = sut.Deploy(dryRun: true);

        result.Outcome.Should().Be(DeployOutcome.Deployed);
        result.DryRun.Should().BeTrue();
        result.StagedFiles.Should().Equal("_posts/2026/2026-07-21-x.md", "_drafts/y.md");
        result.CommitMessage.Should().Be("publish: 2026-07-21-x.md, y.md");
        result.Pushed.Should().BeFalse();
        result.Error.Should().BeNull();

        // dry run must only ever issue the read-only status commands
        runner.Invocations.Should().HaveCount(4);
        runner.Invocations.Should().NotContain(i => i.Arguments.StartsWith("add") || i.Arguments.StartsWith("commit") || i.Arguments.StartsWith("push"));
    }

    [Fact]
    public void Deploy_DryRun_Should_Fall_Back_To_Generic_Message_When_Only_Assets_Staged()
    {
        var runner = new RecordingProcessRunner();
        runner.EnqueueSuccess("true\n");
        runner.EnqueueSuccess("?? assets/images/2026/hero.png\n");
        runner.EnqueueSuccess("0\n");
        runner.EnqueueSuccess(string.Empty);
        var sut = CreateSut(runner);

        var result = sut.Deploy(dryRun: true);

        result.StagedFiles.Should().Equal("assets/images/2026/hero.png");
        result.CommitMessage.Should().Be("publish: blog content update");
    }

    [Fact]
    public void Deploy_Real_Should_Add_Commit_Push_In_Order_And_Honour_Message_Override()
    {
        var runner = new RecordingProcessRunner();
        runner.EnqueueSuccess("true\n");
        runner.EnqueueSuccess(" M _posts/2026/2026-07-21-x.md\n?? _drafts/y.md\n M README.md\n");
        runner.EnqueueSuccess("0\n");
        runner.EnqueueSuccess(string.Empty);
        runner.EnqueueSuccess(); // add
        runner.EnqueueSuccess(); // commit
        runner.EnqueueSuccess(); // push
        var sut = CreateSut(runner);

        var result = sut.Deploy(dryRun: false, message: "custom deploy message");

        result.Outcome.Should().Be(DeployOutcome.Deployed);
        result.DryRun.Should().BeFalse();
        result.StagedFiles.Should().Equal("_posts/2026/2026-07-21-x.md", "_drafts/y.md");
        result.CommitMessage.Should().Be("custom deploy message");
        result.Pushed.Should().BeTrue();
        result.Error.Should().BeNull();

        runner.Invocations.Should().HaveCount(7);
        runner.Invocations[4].Arguments.Should().Be("add -A -- \"_posts/2026/2026-07-21-x.md\" \"_drafts/y.md\"");
        runner.Invocations[5].Arguments.Should().Be("commit -m \"custom deploy message\"");
        runner.Invocations[6].Arguments.Should().Be("push");
        runner.Invocations.Should().OnlyContain(i => i.WorkingDirectory == BaseDirectory);
    }

    [Fact]
    public void Deploy_Real_With_Nothing_Stageable_But_Unpushed_Commits_Should_Push_Only()
    {
        var runner = new RecordingProcessRunner();
        runner.EnqueueSuccess("true\n");
        runner.EnqueueSuccess(string.Empty);
        runner.EnqueueSuccess("2\n");
        runner.EnqueueSuccess("README.md\n");
        runner.EnqueueSuccess(); // push
        var sut = CreateSut(runner);

        var result = sut.Deploy(dryRun: false);

        result.Outcome.Should().Be(DeployOutcome.Deployed);
        result.StagedFiles.Should().BeEmpty();
        result.CommitMessage.Should().BeNull();
        result.Pushed.Should().BeTrue();

        runner.Invocations.Should().HaveCount(5);
        runner.Invocations[4].Arguments.Should().Be("push");
        runner.Invocations.Should().NotContain(i => i.Arguments.StartsWith("add") || i.Arguments.StartsWith("commit"));
    }

    [Fact]
    public void Deploy_Real_Should_Report_GitFailed_When_Push_Fails()
    {
        var runner = new RecordingProcessRunner();
        runner.EnqueueSuccess("true\n");
        runner.EnqueueSuccess("?? _drafts/y.md\n");
        runner.EnqueueSuccess("0\n");
        runner.EnqueueSuccess(string.Empty);
        runner.EnqueueSuccess(); // add
        runner.EnqueueSuccess(); // commit
        runner.EnqueueFailure("fatal: could not read from remote repository\n"); // push
        var sut = CreateSut(runner);

        var result = sut.Deploy(dryRun: false);

        result.Outcome.Should().Be(DeployOutcome.GitFailed);
        result.Pushed.Should().BeFalse();
        result.Error.Should().Be("fatal: could not read from remote repository\n");
    }

    [Fact]
    public void Deploy_Real_Should_Report_GitFailed_When_Add_Fails_And_Not_Attempt_Commit_Or_Push()
    {
        var runner = new RecordingProcessRunner();
        runner.EnqueueSuccess("true\n");
        runner.EnqueueSuccess("?? _drafts/y.md\n");
        runner.EnqueueSuccess("0\n");
        runner.EnqueueSuccess(string.Empty);
        runner.EnqueueFailure("fatal: pathspec did not match any files\n"); // add
        var sut = CreateSut(runner);

        var result = sut.Deploy(dryRun: false);

        result.Outcome.Should().Be(DeployOutcome.GitFailed);
        result.Pushed.Should().BeFalse();
        result.Error.Should().Be("fatal: pathspec did not match any files\n");
        runner.Invocations.Should().HaveCount(5);
    }
}
