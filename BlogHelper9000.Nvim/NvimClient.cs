using System.Threading.Channels;
using BlogHelper9000.Nvim.Grid;
using BlogHelper9000.Nvim.Rpc;
using BlogHelper9000.Nvim.UiEvents;
using Microsoft.Extensions.Logging;

namespace BlogHelper9000.Nvim;

/// <summary>
/// High-level facade for interacting with an embedded Neovim instance.
/// Manages the process lifecycle, RPC communication, and UI event stream.
/// </summary>
public sealed class NvimClient : IAsyncDisposable
{
    private readonly ILogger _logger;
    private NvimProcess? _process;
    private MsgPackRpcClient? _rpc;
    private readonly Channel<NvimUiEvent> _uiEvents;
    private Task? _notificationProcessor;
    private readonly CancellationTokenSource _cts = new();

    public NvimClient(ILogger logger)
    {
        _logger = logger;
        _uiEvents = Channel.CreateUnbounded<NvimUiEvent>(
            new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });
    }

    /// <summary>
    /// Stream of parsed UI events for consumption by the TUI renderer.
    /// </summary>
    public ChannelReader<NvimUiEvent> UiEvents => _uiEvents.Reader;

    public bool IsRunning => _process is not null && !_process.HasExited;

    public async Task StartAsync(string? nvimPath = null)
    {
        _process = NvimProcess.Start(_logger, nvimPath);
        _rpc = new MsgPackRpcClient(_process.StandardInput, _process.StandardOutput, _logger);
        _rpc.StartReading();

        _notificationProcessor = Task.Run(() => ProcessNotifications(_cts.Token));

        _logger.LogInformation("NvimClient started");
    }

    /// <summary>
    /// Attach the UI with ext_linegrid support.
    /// </summary>
    public async Task UiAttachAsync(int width, int height)
    {
        var options = new Dictionary<string, object>
        {
            ["ext_linegrid"] = true,
        };
        await RequestAsync("nvim_ui_attach", width, height, options);
        _logger.LogInformation("UI attached ({Width}x{Height})", width, height);
    }

    public async Task UiDetachAsync()
    {
        await RequestAsync("nvim_ui_detach");
    }

    public async Task UiTryResizeAsync(int width, int height)
    {
        await RequestAsync("nvim_ui_try_resize", width, height);
    }

    /// <summary>
    /// Send input keys to Neovim using nvim_input.
    /// </summary>
    public async Task<int> InputAsync(string keys)
    {
        var result = await RequestAsync("nvim_input", keys);
        return Convert.ToInt32(result);
    }

    /// <summary>
    /// Execute a Neovim command.
    /// </summary>
    public async Task CommandAsync(string command)
    {
        await RequestAsync("nvim_command", command);
    }

    /// <summary>
    /// Get lines from a buffer.
    /// </summary>
    public async Task<string[]> BufGetLinesAsync(int buffer, int start, int end, bool strictIndexing = false)
    {
        var result = await RequestAsync("nvim_buf_get_lines", buffer, start, end, strictIndexing);
        if (result is object?[] arr)
        {
            return arr.Select(x => x?.ToString() ?? "").ToArray();
        }
        return [];
    }

    /// <summary>
    /// Get API info from Neovim.
    /// </summary>
    public async Task<object?> GetApiInfoAsync()
    {
        return await RequestAsync("nvim_get_api_info");
    }

    private async Task<object?> RequestAsync(string method, params object[] args)
    {
        if (_rpc is null) throw new InvalidOperationException("NvimClient not started");
        return await _rpc.RequestAsync(method, args);
    }

    private async Task ProcessNotifications(CancellationToken ct)
    {
        if (_rpc is null) return;

        try
        {
            await foreach (var (method, args) in _rpc.Notifications.ReadAllAsync(ct))
            {
                if (method == "redraw")
                {
                    foreach (var evt in UiEventParser.Parse(args, _logger))
                    {
                        await _uiEvents.Writer.WriteAsync(evt, ct);
                    }
                }
                else if (method is "blog_buf_modified" or "blog_buf_saved")
                {
                    var filePath = args[0]?.ToString() ?? "";
                    NvimUiEvent evt = method == "blog_buf_modified"
                        ? new BufferModifiedEvent(filePath)
                        : new BufferSavedEvent(filePath);
                    await _uiEvents.Writer.WriteAsync(evt, ct);
                }
                else if (method is "blog_buf_enter")
                {
                    var handle = Convert.ToInt32(args[0]);
                    var filePath = args[1]?.ToString() ?? "";
                    await _uiEvents.Writer.WriteAsync(new BufferEnteredEvent(handle, filePath), ct);
                }
                else if (method is "blog_buf_deleted")
                {
                    var handle = Convert.ToInt32(args[0]);
                    await _uiEvents.Writer.WriteAsync(new BufferDeletedEvent(handle), ct);
                }
                else
                {
                    _logger.LogDebug("Unhandled notification: {Method}", method);
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing notifications");
        }
        finally
        {
            _uiEvents.Writer.TryComplete();
        }
    }

    /// <summary>
    /// Gracefully shuts down Neovim by sending :qa! and waiting for exit.
    /// Falls back to process kill if Neovim doesn't exit in time.
    /// </summary>
    public async Task ShutdownAsync(TimeSpan? timeout = null)
    {
        timeout ??= TimeSpan.FromSeconds(3);

        if (_rpc is not null && IsRunning)
        {
            try
            {
                _logger.LogDebug("Sending :qa! to Neovim");
                await RequestAsync("nvim_command", "qa!");
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error sending quit command (may already be exiting)");
            }
        }

        // Wait for process to exit gracefully, then force kill
        if (_process is not null && !_process.HasExited)
        {
            using var cts = new CancellationTokenSource(timeout.Value);
            try
            {
                await _process.WaitForExitAsync(cts.Token);
                _logger.LogDebug("Neovim exited gracefully");
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Neovim did not exit within timeout, force killing");
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync();

        if (_notificationProcessor is not null)
        {
            try { await _notificationProcessor; } catch { }
        }

        if (_rpc is not null)
            await _rpc.DisposeAsync();

        if (_process is not null)
            await _process.DisposeAsync();

        _cts.Dispose();
        _logger.LogDebug("NvimClient disposed");
    }
}
