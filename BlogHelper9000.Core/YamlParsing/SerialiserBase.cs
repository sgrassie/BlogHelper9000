using System.Reflection;

namespace BlogHelper9000.Core.YamlParsing;

public abstract class SerialiserBase
{
    protected const string FrontMatterDelimiter = "---";
    protected const string DateFormat = "dd/MM/yyyy";

    private static readonly Dictionary<string, PropertyInfo> _propertyCache = BuildPropertyCache();

    private static Dictionary<string, PropertyInfo> BuildPropertyCache() =>
        typeof(YamlHeader)
            .GetProperties(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetCustomAttribute<YamlIgnoreAttribute>() is null)
            .ToDictionary(p =>
            {
                var attr = p.GetCustomAttribute<YamlNameAttribute>();
                return attr is not null ? attr.Name.ToLower() : p.Name.ToLower();
            });

    protected Dictionary<string, object?> GetYamlHeaderProperties(YamlHeader? header = null)
    {
        var yamlHeader = header ?? new YamlHeader();
        return _propertyCache.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.GetValue(yamlHeader, null));
    }
}
