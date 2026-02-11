using System.IO.Abstractions;

namespace BlogHelper9000.Core.YamlParsing;

public class YamlConvert(IFileSystem fileSystem)
{
    private static YamlSerialiser Serialiser = new YamlSerialiser();
    private static YamlDeserialiser Deserialiser = new YamlDeserialiser();

    public string Serialise(YamlHeader header)
    {
        return Serialiser.Serialise(header);
    }

    public YamlHeader Deserialise(string filePath)
    {
        if (!fileSystem.File.Exists(filePath)) throw new FileNotFoundException("Unable to find specified file", filePath);

        var content = fileSystem.File.ReadAllLines(filePath);

        return Deserialise(content);
    }

    public YamlHeader Deserialise(string[] rawHeader)
    {
        return Deserialiser.Deserialise(rawHeader);
    }
}
