using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace BlogHelper9000.Nvim.Rpc;

/// <summary>
/// Manages an embedded Neovim process started with --embed --headless.
/// </summary>
public sealed class NvimProcess : IAsyncDisposable
{
    private readonly Process _process;
    private readonly ILogger _logger;
    private bool _disposed;

    private NvimProcess(Process process, ILogger logger)
    {
        _process = process;
        _logger = logger;
    }

    public Stream StandardInput => _process.StandardInput.BaseStream;
    public Stream StandardOutput => _process.StandardOutput.BaseStream;
    public bool HasExited => _process.HasExited;

    public Task WaitForExitAsync(CancellationToken ct = default) => _process.WaitForExitAsync(ct);

    public static NvimProcess Start(ILogger logger, string? nvimPath = null)
    {
        var path = nvimPath ?? "nvim";
        var psi = new ProcessStartInfo
        {
            FileName = path,
            Arguments = "--embed --headless",
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        logger.LogDebug("Starting Neovim process: {Path} {Args}", psi.FileName, psi.Arguments);

        var process = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start Neovim process at '{path}'");

        process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
                logger.LogWarning("nvim stderr: {Line}", e.Data);
        };
        process.BeginErrorReadLine();

        logger.LogInformation("Neovim process started (PID {Pid})", process.Id);
        return new NvimProcess(process, logger);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        if (!_process.HasExited)
        {
            _logger.LogDebug("Killing Neovim process (PID {Pid})", _process.Id);
            _process.Kill(entireProcessTree: true);
            await _process.WaitForExitAsync();
        }

        _process.Dispose();
        _logger.LogDebug("Neovim process disposed");
    }
}
