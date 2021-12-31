namespace BlogHelper9000.YamlParsing;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
public class YamlIgnoreAttribute : Attribute
{
    public YamlIgnoreAttribute(bool ignore = false)
    {
        Ignore = ignore;
    }
    public virtual bool Ignore { get; }
}