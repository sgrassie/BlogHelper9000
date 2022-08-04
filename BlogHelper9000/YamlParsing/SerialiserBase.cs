using System.Reflection;

namespace BlogHelper9000.YamlParsing;

public abstract class SerialiserBase
{
    protected Dictionary<string, object?> GetYamlHeaderProperties(YamlHeader? header = null)
    {
        var yamlHeader = header ?? new YamlHeader();
        return yamlHeader.GetType()
            .GetProperties(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetCustomAttribute<YamlIgnoreAttribute>() is null)
            .ToDictionary(p =>
            {
                var attr = p.GetCustomAttribute<YamlNameAttribute>();

                return attr is not null ? attr.Name.ToLower() : p.Name.ToLower();
            }, p => p.GetValue(yamlHeader, null));
    }
}