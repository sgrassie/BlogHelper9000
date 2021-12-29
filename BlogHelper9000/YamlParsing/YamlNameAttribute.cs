namespace BlogHelper9000.YamlParsing;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
public class YamlNameAttribute : Attribute
{
    public YamlNameAttribute(string name)
    {
        Name = name;
    }

    public virtual string Name { get; }
}