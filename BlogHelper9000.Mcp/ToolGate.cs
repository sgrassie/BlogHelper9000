namespace BlogHelper9000.Mcp;

/// <summary>
/// Serialises file-touching MCP tool bodies. The MCP SDK dispatches concurrent
/// tools/call requests in parallel; PostManager/MarkdownHandler open files with
/// no sharing strategy, so two calls touching the same post can collide with a
/// sharing violation. A single global gate is deliberate: this is a single-user
/// server and per-file locking buys nothing but complexity.
/// </summary>
internal static class ToolGate
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public static T RunExclusive<T>(Func<T> action)
    {
        Gate.Wait();
        try
        {
            return action();
        }
        finally
        {
            Gate.Release();
        }
    }

    public static async Task<T> RunExclusiveAsync<T>(Func<Task<T>> action, CancellationToken ct = default)
    {
        await Gate.WaitAsync(ct);
        try
        {
            return await action();
        }
        finally
        {
            Gate.Release();
        }
    }
}
