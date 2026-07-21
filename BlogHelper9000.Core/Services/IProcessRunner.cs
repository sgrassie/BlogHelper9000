using BlogHelper9000.Core.Models;

namespace BlogHelper9000.Core.Services;

/// <summary>
/// Seam for shelling out to an external process. Like SQLite, process invocation bypasses
/// <see cref="System.IO.Abstractions.IFileSystem"/> — this is the equivalent injectable
/// boundary so callers (e.g. <see cref="GitDeployStateService"/>) can be tested without
/// spawning real processes.
/// </summary>
public interface IProcessRunner
{
    /// <summary>
    /// Runs <paramref name="fileName"/> with <paramref name="arguments"/> in
    /// <paramref name="workingDirectory"/>, capturing stdout/stderr. Never throws for a
    /// non-zero exit code; a timeout is reported via <see cref="ProcessResult.TimedOut"/>
    /// rather than an exception.
    /// </summary>
    /// <remarks>
    /// <paramref name="arguments"/> is an argument vector, not a shell command line: each
    /// element becomes exactly one argv entry (via <c>ProcessStartInfo.ArgumentList</c> in
    /// <see cref="SystemProcessRunner"/>), with no quoting, escaping, or splitting performed
    /// by this seam or its caller. This is deliberate — a single concatenated-and-quoted
    /// string is how argument injection happens (a commit message or filename containing
    /// `" --amend "` or an embedded quote must never be able to smuggle in extra flags).
    /// </remarks>
    ProcessResult Run(string fileName, IReadOnlyList<string> arguments, string workingDirectory, int timeoutMs = 5000);
}
