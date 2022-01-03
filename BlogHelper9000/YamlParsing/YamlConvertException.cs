namespace BlogHelper9000.YamlParsing;

public class YamlConvertException : Exception
{
    public YamlConvertException(){ }

    public YamlConvertException(string message) : base(message) { }
    
    public YamlConvertException(string message, Exception inner) : base(message, inner) { }
}