using System.Buffers;
using System.Collections.Concurrent;
using System.Threading.Channels;
using MessagePack;
using Microsoft.Extensions.Logging;

namespace BlogHelper9000.Nvim.Rpc;

/// <summary>
/// MsgPack-RPC client for Neovim's embedded protocol.
/// Handles request/response correlation and notification dispatch.
///
/// Neovim MsgPack-RPC message types:
///   Request:      [0, msgid, method, params]
///   Response:     [1, msgid, error, result]
///   Notification: [2, method, params]
/// </summary>
public sealed class MsgPackRpcClient : IAsyncDisposable
{
    private readonly Stream _input;
    private readonly Stream _output;
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<long, TaskCompletionSource<object?>> _pendingRequests = new();
    private readonly Channel<(string method, object?[] args)> _notifications;
    private long _nextMsgId;
    private Task? _readerTask;
    private readonly CancellationTokenSource _cts = new();

    private static readonly MessagePackSerializerOptions TypelessOptions =
        MessagePackSerializerOptions.Standard.WithResolver(
            MessagePack.Resolvers.CompositeResolver.Create(
                NvimExtensionResolver.Instance,
                MessagePack.Resolvers.TypelessObjectResolver.Instance));

    public MsgPackRpcClient(Stream input, Stream output, ILogger logger)
    {
        _input = input;
        _output = output;
        _logger = logger;
        _notifications = Channel.CreateUnbounded<(string, object?[])>(
            new UnboundedChannelOptions { SingleReader = false, SingleWriter = true });
    }

    public ChannelReader<(string method, object?[] args)> Notifications => _notifications.Reader;

    public void StartReading()
    {
        _readerTask = Task.Run(() => ReadLoop(_cts.Token));
    }

    public async Task<object?> RequestAsync(string method, params object[] args)
    {
        var msgId = Interlocked.Increment(ref _nextMsgId);
        var tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingRequests[msgId] = tcs;

        var bytes = SerializeRequest(msgId, method, args);

        _logger.LogTrace("RPC request [{MsgId}]: {Method}", msgId, method);

        await _input.WriteAsync(bytes, _cts.Token);
        await _input.FlushAsync(_cts.Token);

        return await tcs.Task;
    }

    internal static byte[] SerializeRequest(long msgId, string method, object[] args)
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new MessagePackWriter(buffer);
        writer.WriteArrayHeader(4);
        writer.Write(0);           // type: request
        writer.Write(msgId);
        writer.Write(method);
        WriteArray(ref writer, args);
        writer.Flush();
        return buffer.WrittenSpan.ToArray();
    }

    internal static byte[] SerializeNotification(string method, object[] args)
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new MessagePackWriter(buffer);
        writer.WriteArrayHeader(3);
        writer.Write(2);           // type: notification
        writer.Write(method);
        WriteArray(ref writer, args);
        writer.Flush();
        return buffer.WrittenSpan.ToArray();
    }

    private async Task ReadLoop(CancellationToken ct)
    {
        try
        {
            var reader = new MessagePackStreamReader(_output);
            while (!ct.IsCancellationRequested)
            {
                var result = await reader.ReadAsync(ct);
                if (result is null)
                {
                    _logger.LogWarning("Neovim stream closed");
                    break;
                }

                try
                {
                    var msg = MessagePackSerializer.Deserialize<object?>(result.Value, TypelessOptions);
                    if (msg is not object?[] arr || arr.Length < 3)
                    {
                        _logger.LogWarning("Invalid MsgPack-RPC message received");
                        continue;
                    }

                    var type = Convert.ToInt32(arr[0]);
                    switch (type)
                    {
                        case 1: // Response: [1, msgid, error, result]
                            HandleResponse(arr);
                            break;
                        case 2: // Notification: [2, method, params]
                            HandleNotification(arr);
                            break;
                        case 0: // Request from nvim (rare)
                            _logger.LogDebug("Received request from Neovim (not handled): {Method}",
                                arr[2]?.ToString());
                            break;
                        default:
                            _logger.LogWarning("Unknown MsgPack-RPC type: {Type}", type);
                            break;
                    }
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to deserialize MsgPack-RPC message, skipping");
                    continue;
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in MsgPack-RPC read loop");
        }
        finally
        {
            // Complete all pending requests
            foreach (var kvp in _pendingRequests)
            {
                kvp.Value.TrySetCanceled();
            }
            _notifications.Writer.TryComplete();
        }
    }

    private void HandleResponse(object?[] arr)
    {
        var msgId = Convert.ToInt64(arr[1]);
        var error = arr[2];
        var result = arr[3];

        if (_pendingRequests.TryRemove(msgId, out var tcs))
        {
            if (error is null)
            {
                tcs.TrySetResult(result);
            }
            else
            {
                tcs.TrySetException(new NvimRpcException(error.ToString() ?? "Unknown RPC error"));
            }
        }
        else
        {
            _logger.LogWarning("Received response for unknown msgId {MsgId}", msgId);
        }
    }

    private void HandleNotification(object?[] arr)
    {
        var method = arr[1]?.ToString() ?? "";
        var paramsObj = arr[2];
        var paramsList = paramsObj is object?[] paramArr
            ? paramArr
            : [];

        _logger.LogTrace("RPC notification: {Method}", method);
        _notifications.Writer.TryWrite((method, paramsList));
    }

    private static void WriteArray(ref MessagePackWriter writer, object[] args)
    {
        writer.WriteArrayHeader(args.Length);
        foreach (var arg in args)
        {
            WriteValue(ref writer, arg);
        }
    }

    private static void WriteValue(ref MessagePackWriter writer, object? value)
    {
        switch (value)
        {
            case null:
                writer.WriteNil();
                break;
            case string s:
                writer.Write(s);
                break;
            case int i:
                writer.Write(i);
                break;
            case long l:
                writer.Write(l);
                break;
            case bool b:
                writer.Write(b);
                break;
            case double d:
                writer.Write(d);
                break;
            case object[] arr:
                WriteArray(ref writer, arr);
                break;
            case Dictionary<string, object> dict:
                writer.WriteMapHeader(dict.Count);
                foreach (var kvp in dict)
                {
                    writer.Write(kvp.Key);
                    WriteValue(ref writer, kvp.Value);
                }
                break;
            default:
                writer.Write(value.ToString() ?? "");
                break;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync();
        if (_readerTask is not null)
        {
            try { await _readerTask; } catch { }
        }
        _cts.Dispose();
    }
}

public class NvimRpcException : Exception
{
    public NvimRpcException(string message) : base(message) { }
    public NvimRpcException(string message, Exception inner) : base(message, inner) { }
}
