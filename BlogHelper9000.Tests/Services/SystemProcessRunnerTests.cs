using BlogHelper9000.Core.Services;

namespace BlogHelper9000.Tests.Services;

public class SystemProcessRunnerTests
{
    /// <summary>
    /// The one deliberately real-process test in this suite: runs an actual `git --version`
    /// against the real git binary (guaranteed present on dev/CI here) to prove
    /// SystemProcessRunner's Process plumbing (start, redirect, exit code) works end to end.
    /// Everything else in GitDeployStateServiceTests uses the recording fake — this must
    /// never touch the working directory's own git repo.
    /// </summary>
    [Fact]
    public void Run_Should_Execute_Real_Process_And_Capture_Output()
    {
        var sut = new SystemProcessRunner();

        var result = sut.Run("git", ["--version"], workingDirectory: Path.GetTempPath());

        result.ExitCode.Should().Be(0);
        result.TimedOut.Should().BeFalse();
        result.StandardOutput.Should().NotBeNullOrWhiteSpace();
        result.StandardOutput.Should().Contain("git version");
    }

    /// <summary>
    /// Regression coverage for the timeout-path race: a process that is still emitting output
    /// when the timeout fires means Kill() and the async OutputDataReceived/ErrorDataReceived
    /// handlers race each other. Before the fix, Run() read the (non-thread-safe) StringBuilders
    /// immediately after a bounded WaitForExit(1000), with no guarantee the handlers were done
    /// appending. This drives that race on every run — a shell loop that keeps printing well
    /// past the timeout — and asserts Run() still returns cleanly instead of throwing/corrupting
    /// output.
    /// </summary>
    [Fact]
    public void Run_Should_Report_TimedOut_Cleanly_When_Process_Is_Still_Producing_Output_At_Kill_Time()
    {
        var sut = new SystemProcessRunner();

        var result = sut.Run(
            "sh",
            ["-c", "for i in $(seq 1 200); do echo line-$i; sleep 0.02; done"],
            workingDirectory: Path.GetTempPath(),
            timeoutMs: 100);

        result.TimedOut.Should().BeTrue();
        result.ExitCode.Should().Be(-1);
    }
}
