using System.Buffers;
using BlogHelper9000.Nvim.Rpc;
using FluentAssertions;
using MessagePack;

namespace BlogHelper9000.Nvim.Tests.Rpc;

public class MsgPackSerializationTests
{
    private static readonly MessagePackSerializerOptions Options =
        MessagePackSerializerOptions.Standard.WithResolver(
            MessagePack.Resolvers.CompositeResolver.Create(
                NvimExtensionResolver.Instance,
                MessagePack.Resolvers.TypelessObjectResolver.Instance));

    [Fact]
    public void SerializeRequest_Produces_Valid_MsgPack()
    {
        var bytes = MsgPackRpcClient.SerializeRequest(1, "nvim_get_api_info", []);

        // Deserialize and verify structure
        var result = MessagePackSerializer.Deserialize<object?>(bytes, Options);

        result.Should().BeOfType<object?[]>();
        var arr = (object?[])result!;
        arr.Should().HaveCount(4);
        Convert.ToInt32(arr[0]).Should().Be(0);   // type: request
        Convert.ToInt64(arr[1]).Should().Be(1);    // msgid
        arr[2]?.ToString().Should().Be("nvim_get_api_info"); // method
    }

    [Fact]
    public void SerializeRequest_With_Args_Includes_Params()
    {
        var bytes = MsgPackRpcClient.SerializeRequest(42, "nvim_input", ["ihello"]);

        var result = MessagePackSerializer.Deserialize<object?>(bytes,
            Options);

        var arr = (object?[])result!;
        Convert.ToInt64(arr[1]).Should().Be(42);
        arr[2]?.ToString().Should().Be("nvim_input");
        var paramsArr = (object?[])arr[3]!;
        paramsArr.Should().HaveCount(1);
        paramsArr[0]?.ToString().Should().Be("ihello");
    }

    [Fact]
    public void SerializeRequest_With_Dict_Arg_Produces_MsgPack_Map()
    {
        var opts = new Dictionary<string, object> { ["ext_linegrid"] = true };
        var bytes = MsgPackRpcClient.SerializeRequest(1, "nvim_ui_attach", [80, 24, opts]);

        var result = MessagePackSerializer.Deserialize<object?>(bytes,
            Options);

        var arr = (object?[])result!;
        var paramsArr = (object?[])arr[3]!;
        paramsArr.Should().HaveCount(3);
        Convert.ToInt32(paramsArr[0]).Should().Be(80);
        Convert.ToInt32(paramsArr[1]).Should().Be(24);
        // The map should be deserialized
        paramsArr[2].Should().NotBeNull();
    }

    [Fact]
    public void SerializeNotification_Produces_Valid_MsgPack()
    {
        var bytes = MsgPackRpcClient.SerializeNotification("nvim_command", [":q!"]);

        var result = MessagePackSerializer.Deserialize<object?>(bytes,
            Options);

        result.Should().BeOfType<object?[]>();
        var arr = (object?[])result!;
        arr.Should().HaveCount(3);
        Convert.ToInt32(arr[0]).Should().Be(2);    // type: notification
        arr[1]?.ToString().Should().Be("nvim_command");
    }

    [Fact]
    public void SerializeRequest_With_Null_Arg()
    {
        var bytes = MsgPackRpcClient.SerializeRequest(1, "test", [null!]);

        var result = MessagePackSerializer.Deserialize<object?>(bytes,
            Options);

        var arr = (object?[])result!;
        var paramsArr = (object?[])arr[3]!;
        paramsArr[0].Should().BeNull();
    }

    [Fact]
    public void SerializeRequest_With_Bool_Args()
    {
        var bytes = MsgPackRpcClient.SerializeRequest(1, "test", [true, false]);

        var result = MessagePackSerializer.Deserialize<object?>(bytes,
            Options);

        var arr = (object?[])result!;
        var paramsArr = (object?[])arr[3]!;
        Convert.ToBoolean(paramsArr[0]).Should().BeTrue();
        Convert.ToBoolean(paramsArr[1]).Should().BeFalse();
    }
}
