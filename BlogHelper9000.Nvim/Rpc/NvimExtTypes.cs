using MessagePack;
using MessagePack.Formatters;

namespace BlogHelper9000.Nvim.Rpc;

/// <summary>
/// Neovim handle types sent as MsgPack extension types.
/// Buffer = 0x00, Window = 0x01, Tabpage = 0x02.
/// </summary>
public readonly record struct NvimBuffer(long Id);
public readonly record struct NvimWindow(long Id);
public readonly record struct NvimTabpage(long Id);

/// <summary>
/// Deserializes MsgPack extension types into Neovim handle records.
/// Delegates all other types to <see cref="PrimitiveObjectFormatter"/>.
/// </summary>
internal class NvimExtensionFormatter : IMessagePackFormatter<object?>
{
    public static readonly NvimExtensionFormatter Instance = new();

    public void Serialize(ref MessagePackWriter writer, object? value, MessagePackSerializerOptions options)
    {
        PrimitiveObjectFormatter.Instance.Serialize(ref writer, value, options);
    }

    public object? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        if (reader.NextMessagePackType == MessagePackType.Extension)
        {
            var ext = reader.ReadExtensionFormat();
            var dataReader = new MessagePackReader(ext.Data);
            var id = dataReader.ReadInt64();

            return ext.TypeCode switch
            {
                0 => new NvimBuffer(id),
                1 => new NvimWindow(id),
                2 => new NvimTabpage(id),
                _ => new ExtensionResult(ext.TypeCode, ext.Data)
            };
        }

        return PrimitiveObjectFormatter.Instance.Deserialize(ref reader, options);
    }
}

/// <summary>
/// Resolver that returns <see cref="NvimExtensionFormatter"/> for <c>object?</c>,
/// allowing it to intercept extension types before standard formatters.
/// </summary>
internal class NvimExtensionResolver : IFormatterResolver
{
    public static readonly IFormatterResolver Instance = new NvimExtensionResolver();

    public IMessagePackFormatter<T>? GetFormatter<T>()
    {
        if (typeof(T) == typeof(object))
            return (IMessagePackFormatter<T>)(object)NvimExtensionFormatter.Instance;
        return null;
    }
}
