using System.Buffers;
using BlogHelper9000.Nvim.Rpc;
using FluentAssertions;
using MessagePack;

namespace BlogHelper9000.Nvim.Tests.Rpc;

public class NvimExtensionFormatterTests
{
    private static readonly MessagePackSerializerOptions Options =
        MessagePackSerializerOptions.Standard.WithResolver(
            MessagePack.Resolvers.CompositeResolver.Create(
                NvimExtensionResolver.Instance,
                MessagePack.Resolvers.TypelessObjectResolver.Instance));

    [Fact]
    public void Deserializes_Buffer_Extension_Type()
    {
        var bytes = WriteExtension(0, 7);

        var result = MessagePackSerializer.Deserialize<object?>(bytes, Options);

        result.Should().BeOfType<NvimBuffer>();
        ((NvimBuffer)result!).Id.Should().Be(7);
    }

    [Fact]
    public void Deserializes_Window_Extension_Type()
    {
        var bytes = WriteExtension(1, 1);

        var result = MessagePackSerializer.Deserialize<object?>(bytes, Options);

        result.Should().BeOfType<NvimWindow>();
        ((NvimWindow)result!).Id.Should().Be(1);
    }

    [Fact]
    public void Deserializes_Tabpage_Extension_Type()
    {
        var bytes = WriteExtension(2, 42);

        var result = MessagePackSerializer.Deserialize<object?>(bytes, Options);

        result.Should().BeOfType<NvimTabpage>();
        ((NvimTabpage)result!).Id.Should().Be(42);
    }

    [Fact]
    public void Standard_Int_Deserializes_Through_Formatter()
    {
        var bytes = Serialize(42);

        var result = MessagePackSerializer.Deserialize<object?>(bytes, Options);

        Convert.ToInt32(result).Should().Be(42);
    }

    [Fact]
    public void Standard_String_Deserializes_Through_Formatter()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new MessagePackWriter(buffer);
        writer.Write("hello");
        writer.Flush();

        var result = MessagePackSerializer.Deserialize<object?>(buffer.WrittenMemory, Options);

        result.Should().Be("hello");
    }

    [Fact]
    public void Standard_Array_Deserializes_Through_Formatter()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new MessagePackWriter(buffer);
        writer.WriteArrayHeader(2);
        writer.Write(1);
        writer.Write(2);
        writer.Flush();

        var result = MessagePackSerializer.Deserialize<object?>(buffer.WrittenMemory, Options);

        result.Should().BeOfType<object?[]>();
        var arr = (object?[])result!;
        arr.Should().HaveCount(2);
    }

    [Fact]
    public void Unknown_Extension_Type_Does_Not_Crash()
    {
        var bytes = WriteExtension(5, 99);

        var act = () => MessagePackSerializer.Deserialize<object?>(bytes, Options);

        act.Should().NotThrow();
    }

    [Fact]
    public void Extension_Inside_Array_Deserializes_Correctly()
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new MessagePackWriter(buffer);
        writer.WriteArrayHeader(2);
        writer.Write("win_id");
        WriteExtensionTo(ref writer, 1, 3);
        writer.Flush();

        var result = MessagePackSerializer.Deserialize<object?>(buffer.WrittenMemory, Options);

        result.Should().BeOfType<object?[]>();
        var arr = (object?[])result!;
        arr[0].Should().Be("win_id");
        arr[1].Should().BeOfType<NvimWindow>();
        ((NvimWindow)arr[1]!).Id.Should().Be(3);
    }

    private static byte[] Serialize(int value)
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new MessagePackWriter(buffer);
        writer.Write(value);
        writer.Flush();
        return buffer.WrittenSpan.ToArray();
    }

    private static byte[] WriteExtension(sbyte typeCode, long id)
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new MessagePackWriter(buffer);
        WriteExtensionTo(ref writer, typeCode, id);
        writer.Flush();
        return buffer.WrittenSpan.ToArray();
    }

    private static void WriteExtensionTo(ref MessagePackWriter writer, sbyte typeCode, long id)
    {
        // Serialize the handle ID as a MsgPack integer
        var idBuffer = new ArrayBufferWriter<byte>();
        var idWriter = new MessagePackWriter(idBuffer);
        idWriter.Write(id);
        idWriter.Flush();

        writer.WriteExtensionFormat(new ExtensionResult(typeCode,
            new ReadOnlySequence<byte>(idBuffer.WrittenMemory)));
    }
}
