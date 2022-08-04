namespace BlogHelper9000.YamlParsing;

public static class YamlConvert
{
    private static YamlSerialiser Serialiser;
    private static YamlDeserialiser Deserialiser;
    
    static YamlConvert()
    {
        Serialiser = new YamlSerialiser();
        Deserialiser = new YamlDeserialiser();
    }
    
    public static string Serialise(YamlHeader header)
    {
        return Serialiser.Serialise(header);
    }

    public static YamlHeader Deserialise(string filePath)
    {
        if (!File.Exists(filePath)) throw new FileNotFoundException("Unable to find specified file", filePath);

        var content = File.ReadAllLines(filePath);

        return Deserialise(content);
    }

    public static YamlHeader Deserialise(string[] rawHeader)
    {
        return Deserialiser.Deserialise(rawHeader);
    }
}