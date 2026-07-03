using BlogHelper9000.Nvim.Rpc;
using FluentAssertions;
using MessagePack;
using Microsoft.Extensions.Logging.Abstractions;

namespace BlogHelper9000.Nvim.Tests.Rpc;

public class MsgPackRpcClientTests
{
    private static readonly MessagePackSerializerOptions Options =
        MessagePackSerializerOptions.Standard.WithResolver(
            MessagePack.Resolvers.CompositeResolver.Create(
                NvimExtensionResolver.Instance,
                MessagePack.Resolvers.PrimitiveObjectResolver.Instance));

    /// <summary>
    /// Splits every write into two delayed chunks so concurrent, unsynchronized
    /// writers would interleave their bytes; records each logical write attempt separately.
    /// </summary>
    private sealed class SlowRecordingStream : Stream
    {
        private readonly List<byte> _combined = [];
        public readonly List<byte[]> Writes = [];
        private readonly SemaphoreSlim _gate = new(1, 1);

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            var data = buffer.Skip(offset).Take(count).ToArray();
            var half = data.Length / 2;
            var first = data[..half];
            var second = data[half..];

            _combined.AddRange(first);
            await Task.Delay(20, cancellationToken);
            _combined.AddRange(second);

            await _gate.WaitAsync(cancellationToken);
            try
            {
                Writes.Add(data);
            }
            finally
            {
                _gate.Release();
            }
        }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await WriteAsync(buffer.ToArray(), 0, buffer.Length, cancellationToken);
        }

        public override void Write(byte[] buffer, int offset, int count) =>
            WriteAsync(buffer, offset, count, CancellationToken.None).GetAwaiter().GetResult();

        public byte[] CombinedBytes => _combined.ToArray();

        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => 0;
        public override long Seek(long offset, SeekOrigin origin) => 0;
        public override void SetLength(long value) { }
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => 0;
        public override long Position { get; set; }
    }

    /// <summary>A read stream that never produces data, simulating an unresponsive Neovim process.</summary>
    private sealed class NeverRespondingStream : Stream
    {
        public override int Read(byte[] buffer, int offset, int count) => 0;

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return 0;
        }

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => 0;
        public override void SetLength(long value) { }
        public override void Write(byte[] buffer, int offset, int count) { }
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => 0;
        public override long Position { get; set; }
    }

    [Fact]
    public async Task RequestAsync_Should_Throw_When_No_Response_Arrives_Within_Timeout()
    {
        var input = new MemoryStream();
        var output = new NeverRespondingStream();
        var client = new MsgPackRpcClient(input, output, NullLogger.Instance);
        client.StartReading();

        Func<Task> act = () => client.RequestAsync("nvim_get_api_info", TimeSpan.FromMilliseconds(100));

        await act.Should().ThrowAsync<OperationCanceledException>();

        await client.DisposeAsync();
    }

    [Fact]
    public async Task RequestAsync_Should_Not_Interleave_Concurrent_Writes()
    {
        var input = new SlowRecordingStream();
        var output = new NeverRespondingStream();
        var client = new MsgPackRpcClient(input, output, NullLogger.Instance);
        client.StartReading();

        var timeout = TimeSpan.FromMilliseconds(200);
        var task1 = client.RequestAsync("first_method", timeout);
        var task2 = client.RequestAsync("second_method", timeout);

        try { await Task.WhenAll(task1, task2); } catch (OperationCanceledException) { }

        input.Writes.Should().HaveCount(2);
        foreach (var frame in input.Writes)
        {
            var act = () => MessagePackSerializer.Deserialize<object?>(frame, Options);
            act.Should().NotThrow();
        }

        // The wire bytes must show one full write completing before the other starts —
        // i.e. match one of the two non-interleaved concatenation orders.
        var combined = input.CombinedBytes;
        var orderAB = input.Writes[0].Concat(input.Writes[1]).ToArray();
        var orderBA = input.Writes[1].Concat(input.Writes[0]).ToArray();
        var isSerialized = combined.SequenceEqual(orderAB) || combined.SequenceEqual(orderBA);

        isSerialized.Should().BeTrue("concurrent writes must be serialized, not interleaved on the wire");

        await client.DisposeAsync();
    }
}
