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

    private static bool IsMutatingInvocation(GitInvocation invocation) =>
        invocation.Arguments.Count > 0 && invocation.Arguments[0] is "add" or "commit" or "push";

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
        runner.Invocations[0].Arguments.Should().Equal("rev-parse", "--is-inside-work-tree");
        runner.Invocations[0].WorkingDirectory.Should().Be(BaseDirectory);
        runner.Invocations[1].Arguments.Should().Equal("status", "--porcelain=v1");
        runner.Invocations[2].Arguments.Should().Equal("rev-list", "--count", "@{upstream}..HEAD");
        runner.Invocations[3].Arguments.Should().Equal("diff", "--name-only", "@{upstream}..HEAD");
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
    public void GetDeployState_Should_Use_LastArrow_When_OldPath_Itself_Contains_An_Arrow()
    {
        // A rename/copy line's OLD path can legitimately contain a literal " -> " (e.g. it was
        // itself renamed from something with that text). Only the segment after the LAST
        // " -> " is guaranteed to be the real new path.
        var runner = new RecordingProcessRunner();
        runner.EnqueueSuccess("true\n");
        runner.EnqueueSuccess("R  a -> b.md -> _drafts/new.md\n");
        runner.EnqueueSuccess("0\n");
        runner.EnqueueSuccess(string.Empty);
        var sut = CreateSut(runner);

        var state = sut.GetDeployState();

        state.UncommittedFiles.Should().Equal("_drafts/new.md");
    }

    [Fact]
    public void GetDeployState_Should_Not_Split_NonRename_Line_On_Arrow_In_Filename()
    {
        // "??" (untracked) is not a rename/copy status, so a filename that happens to contain
        // " -> " must be reported whole, not mistaken for rename syntax.
        var runner = new RecordingProcessRunner();
        runner.EnqueueSuccess("true\n");
        runner.EnqueueSuccess("?? _drafts/weird -> file.md\n");
        runner.EnqueueSuccess("0\n");
        runner.EnqueueSuccess(string.Empty);
        var sut = CreateSut(runner);

        var state = sut.GetDeployState();

        state.UncommittedFiles.Should().Equal("_drafts/weird -> file.md");
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

    [Fact]
    public void GetDeployState_Should_Report_What_Is_Known_When_Status_Fails()
    {
        var runner = new RecordingProcessRunner();
        runner.EnqueueSuccess("true\n");
        runner.Enqueue(new ProcessResult(128, string.Empty, "fatal: index file corrupt\n", false));
        var sut = CreateSut(runner);

        var state = sut.GetDeployState();

        state.IsGitRepo.Should().BeTrue();
        state.UncommittedFiles.Should().BeEmpty();
        state.HasUpstream.Should().BeFalse();
        runner.Invocations.Should().HaveCount(2);
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
    public void Deploy_Should_Return_GitFailed_When_Status_Fails()
    {
        var runner = new RecordingProcessRunner();
        runner.EnqueueSuccess("true\n");
        runner.Enqueue(new ProcessResult(128, string.Empty, "fatal: index file corrupt\n", false));
        var sut = CreateSut(runner);

        var result = sut.Deploy(dryRun: true);

        result.Outcome.Should().Be(DeployOutcome.GitFailed);
        result.Error.Should().Be("fatal: index file corrupt\n");
        result.StagedFiles.Should().BeEmpty();
        runner.Invocations.Should().HaveCount(2);
        runner.Invocations.Should().NotContain(i => IsMutatingInvocation(i));
    }

    [Fact]
    public void Deploy_Should_Return_GitFailed_When_Diff_Fails()
    {
        var runner = new RecordingProcessRunner();
        runner.EnqueueSuccess("true\n");
        runner.EnqueueSuccess("?? _drafts/y.md\n");
        runner.EnqueueSuccess("1\n");
        runner.Enqueue(new ProcessResult(128, string.Empty, "fatal: bad revision\n", false));
        var sut = CreateSut(runner);

        var result = sut.Deploy(dryRun: false);

        result.Outcome.Should().Be(DeployOutcome.GitFailed);
        result.Error.Should().Be("fatal: bad revision\n");
        runner.Invocations.Should().HaveCount(4);
        runner.Invocations.Should().NotContain(i => IsMutatingInvocation(i));
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
        runner.Invocations.Should().NotContain(i => IsMutatingInvocation(i));
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
        runner.Invocations.Should().NotContain(i => IsMutatingInvocation(i));
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
        runner.Invocations[4].Arguments.Should().Equal("add", "-A", "--", "_posts/2026/2026-07-21-x.md", "_drafts/y.md");
        runner.Invocations[5].Arguments.Should().Equal("commit", "-m", "custom deploy message");
        runner.Invocations[6].Arguments.Should().Equal("push");
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
        runner.Invocations[4].Arguments.Should().Equal("push");
        runner.Invocations.Should().NotContain(i => i.Arguments.Count > 0 && (i.Arguments[0] == "add" || i.Arguments[0] == "commit"));
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

    // ----- Argument-injection regressions -----
    // Prior implementation built a single shell-style string (`$"commit -m \"{message}\""`,
    // `$"add -A -- {string.Join(' ', paths.Select(QuoteArg))}"`) with bare double-quote
    // wrapping and no escaping, so a message or filename containing a quote could terminate
    // the "argument" early and inject extra flags. IProcessRunner now takes an argument
    // vector; these tests assert the message/path always arrives as exactly ONE argv element,
    // proving nothing can be smuggled in as a sibling argument.

    [Fact]
    public void Deploy_Real_Should_Pass_CommitMessage_Containing_FlagLike_Text_As_A_Single_Argument()
    {
        var runner = new RecordingProcessRunner();
        runner.EnqueueSuccess("true\n");
        runner.EnqueueSuccess("?? _drafts/y.md\n");
        runner.EnqueueSuccess("0\n");
        runner.EnqueueSuccess(string.Empty);
        runner.EnqueueSuccess(); // add
        runner.EnqueueSuccess(); // commit
        runner.EnqueueSuccess(); // push
        var sut = CreateSut(runner);

        const string maliciousMessage = "innocuous\" --amend \"evil";

        var result = sut.Deploy(dryRun: false, message: maliciousMessage);

        result.Outcome.Should().Be(DeployOutcome.Deployed);
        result.CommitMessage.Should().Be(maliciousMessage);

        var commitInvocation = runner.Invocations.Single(i => i.Arguments.Count > 0 && i.Arguments[0] == "commit");
        commitInvocation.Arguments.Should().Equal("commit", "-m", maliciousMessage);
        commitInvocation.Arguments.Should().HaveCount(3, "the malicious text must arrive as one argv element, not be split into extra arguments");
    }

    [Fact]
    public void Deploy_Real_Should_Pass_Staged_Path_With_EmbeddedQuote_As_A_Single_Argument()
    {
        var runner = new RecordingProcessRunner();
        runner.EnqueueSuccess("true\n");
        // git wraps this untracked path in quotes because it contains a literal quote char,
        // escaping the embedded quote as \" the way git's quote-path does.
        runner.EnqueueSuccess("?? \"_drafts/we\\\"ird.md\"\n");
        runner.EnqueueSuccess("0\n");
        runner.EnqueueSuccess(string.Empty);
        runner.EnqueueSuccess(); // add
        runner.EnqueueSuccess(); // commit
        runner.EnqueueSuccess(); // push
        var sut = CreateSut(runner);

        var result = sut.Deploy(dryRun: false);

        result.Outcome.Should().Be(DeployOutcome.Deployed);
        var expectedPath = "_drafts/we\\\"ird.md";
        result.StagedFiles.Should().Equal(expectedPath);

        var addInvocation = runner.Invocations.Single(i => i.Arguments.Count > 0 && i.Arguments[0] == "add");
        addInvocation.Arguments.Should().Equal("add", "-A", "--", expectedPath);
        addInvocation.Arguments.Should().HaveCount(4, "the quote-bearing path must arrive as one argv element, not be split into extra arguments");
    }

    // ----- Timeouts -----
    // A real `git push` carrying image assets over a slow link must not be killed mid-transfer
    // by the same short timeout used for cheap read-only status probes.

    [Fact]
    public void Deploy_Real_Should_Give_Commit_And_Push_A_Longer_Timeout_Than_The_ReadOnly_Probes()
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

        var result = sut.Deploy(dryRun: false);

        result.Outcome.Should().Be(DeployOutcome.Deployed);

        var probeInvocations = runner.Invocations.Take(4);
        probeInvocations.Should().OnlyContain(i => i.TimeoutMs == 5000);

        var addInvocation = runner.Invocations.Single(i => i.Arguments.Count > 0 && i.Arguments[0] == "add");
        addInvocation.TimeoutMs.Should().Be(5000);

        var commitInvocation = runner.Invocations.Single(i => i.Arguments.Count > 0 && i.Arguments[0] == "commit");
        commitInvocation.TimeoutMs.Should().Be(60000);

        var pushInvocation = runner.Invocations.Single(i => i.Arguments.Count > 0 && i.Arguments[0] == "push");
        pushInvocation.TimeoutMs.Should().Be(60000);
    }

    [Fact]
    public void Deploy_Real_With_Nothing_Stageable_But_Unpushed_Commits_Should_Give_Push_A_Longer_Timeout()
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

        var pushInvocation = runner.Invocations.Single(i => i.Arguments.Count > 0 && i.Arguments[0] == "push");
        pushInvocation.TimeoutMs.Should().Be(60000);
    }
}
