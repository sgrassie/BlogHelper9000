using BlogHelper9000.Core.Models;
using BlogHelper9000.Core.Services;

namespace BlogHelper9000.Tests.Services;

/// <summary>
/// Hand-rolled recording fake for <see cref="IProcessRunner"/>. Canned results are enqueued
/// in the order git invocations are expected to happen; each <see cref="Run"/> call records
/// its exact arguments so tests can assert on invocation order/content, and dequeues the next
/// canned result. Optionally throws once, to simulate a missing git binary / launch failure.
/// </summary>
internal sealed class RecordingProcessRunner : IProcessRunner
{
    private readonly Queue<ProcessResult> _results = new();
    private Exception? _throwOnNextInvocation;

    public List<GitInvocation> Invocations { get; } = [];

    public void Enqueue(ProcessResult result) => _results.Enqueue(result);

    public void EnqueueSuccess(string standardOutput = "") =>
        Enqueue(new ProcessResult(0, standardOutput, string.Empty, false));

    public void EnqueueFailure(string standardError = "") =>
        Enqueue(new ProcessResult(1, string.Empty, standardError, false));

    public void ThrowOnNextInvocation(Exception exception) => _throwOnNextInvocation = exception;

    public ProcessResult Run(string fileName, IReadOnlyList<string> arguments, string workingDirectory, int timeoutMs = 5000)
    {
        Invocations.Add(new GitInvocation(fileName, arguments, workingDirectory, timeoutMs));

        if (_throwOnNextInvocation is { } exception)
        {
            _throwOnNextInvocation = null;
            throw exception;
        }

        return _results.Count > 0
            ? _results.Dequeue()
            : throw new InvalidOperationException($"No canned ProcessResult queued for invocation: {fileName} {string.Join(' ', arguments)}");
    }
}

internal sealed record GitInvocation(string FileName, IReadOnlyList<string> Arguments, string WorkingDirectory, int TimeoutMs);
