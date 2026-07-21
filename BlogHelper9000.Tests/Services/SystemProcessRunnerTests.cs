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
}
