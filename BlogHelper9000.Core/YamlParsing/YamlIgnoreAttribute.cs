namespace BlogHelper9000.Core.YamlParsing;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
public class YamlIgnoreAttribute : Attribute
{
    public YamlIgnoreAttribute(bool ignore = true)
    {
        Ignore = ignore;
    }
    public virtual bool Ignore { get; }
}
