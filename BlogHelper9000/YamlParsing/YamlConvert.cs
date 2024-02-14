using System.IO.Abstractions;

namespace BlogHelper9000.YamlParsing;

public class YamlConvert
{
    private readonly IFileSystem _fileSystem;
    private static YamlSerialiser Serialiser;
    private static YamlDeserialiser Deserialiser;
    
    public YamlConvert(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
        Serialiser = new YamlSerialiser();
        Deserialiser = new YamlDeserialiser();
    }
    
    public string Serialise(YamlHeader header)
    {
        return Serialiser.Serialise(header);
    }

    public YamlHeader Deserialise(string filePath)
    {
        if (!_fileSystem.File.Exists(filePath)) throw new FileNotFoundException("Unable to find specified file", filePath);

        var content = _fileSystem.File.ReadAllLines(filePath);

        return Deserialise(content);
    }

    public YamlHeader Deserialise(string[] rawHeader)
    {
        return Deserialiser.Deserialise(rawHeader);
    }
}