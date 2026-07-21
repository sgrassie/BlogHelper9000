using System.Diagnostics;
using System.Text;
using BlogHelper9000.Core.Models;

namespace BlogHelper9000.Core.Services;

/// <summary>
/// Real <see cref="IProcessRunner"/> backed by <see cref="System.Diagnostics.Process"/>.
/// Redirects stdout/stderr and kills the process tree if it exceeds the timeout.
/// </summary>
public sealed class SystemProcessRunner : IProcessRunner
{
    public ProcessResult Run(string fileName, IReadOnlyList<string> arguments, string workingDirectory, int timeoutMs = 5000)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        // ArgumentList passes each element straight through as one argv entry — no shell
        // quoting/parsing, so embedded quotes, spaces, or flag-like text in a commit message
        // or filename can never be reinterpreted as extra arguments.
        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        var standardOutput = new StringBuilder();
        var standardError = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null) standardOutput.AppendLine(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null) standardError.AppendLine(e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        if (!process.WaitForExit(timeoutMs))
        {
            TryKill(process);
            // Give the (now-dying) process a moment to release the redirected streams.
            process.WaitForExit(1000);
            return new ProcessResult(-1, standardOutput.ToString(), standardError.ToString(), TimedOut: true);
        }

        // Recommended pattern per Process docs: call the parameterless overload after a
        // successful timed WaitForExit to guarantee redirected output has been fully read.
        process.WaitForExit();

        return new ProcessResult(process.ExitCode, standardOutput.ToString(), standardError.ToString(), TimedOut: false);
    }

    private static void TryKill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch
        {
            // Best effort — the process may have already exited between the timeout and the kill.
        }
    }
}
