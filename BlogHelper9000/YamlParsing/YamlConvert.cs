using System.Globalization;
using System.Reflection;
using System.Text;

namespace BlogHelper9000.YamlParsing;

public static class YamlConvert
{
    public static string Serialise(YamlHeader header)
    {
        var dict = GetYamlHeaderProperties(header);

        var builder = new StringBuilder();
        builder.AppendLine("---");

        foreach (var item in dict)
        {
            if (item.Value is null) continue; // don't serialise if the value is null
            if(item.Value is string s && string.IsNullOrEmpty(s)) continue;

            if (item.Value is List<string> list)
            {
                if (list.Any())
                {
                    builder.AppendLine($"{item.Key}: [{TagsString(item.Value)}]");
                }
            }
            else if (item.Value is DateTime)
            {
                builder.AppendLine($"{item.Key}: {item.Value:dd/MM/yyyy}");
            }
            else
            {
                builder.AppendLine($"{item.Key}: {item.Value}");
            }
        }

        builder.Append("---");

        return builder.ToString();

        string TagsString(object? tags)
        {
            if (tags is null) return string.Empty;
            var result = string.Join(",", (List<string>)tags);
            return result;
        }
    }

    public static YamlHeader Deserialise(string filePath)
    {
        if (!File.Exists(filePath)) throw new FileNotFoundException("Unable to find specified file", filePath);

        var content = File.ReadAllLines(filePath);

        return Deserialise(content);
    }

    public static YamlHeader Deserialise(string[] fileContent)
    {
        var headerStartMarkerFound = false;
        var yamlBlock = new List<string>();

        foreach (var line in fileContent)
        {
            if (line.Trim() == "---")
            {
                if (!headerStartMarkerFound)
                {
                    headerStartMarkerFound = true;
                    continue;
                }

                break;
            }

            yamlBlock.Add(line);
        }
            
        return ParseYamlHeader(yamlBlock);
    }

    private static YamlHeader ParseYamlHeader(IEnumerable<string> yamlHeader)
    {
        var parsedHeaderProperties = new Dictionary<string, object>();
        var extraHeaderProperties = new Dictionary<string, string>();
        var headerProperties = GetYamlHeaderProperties();

        foreach (var line in yamlHeader)
        {
            var propertyValue = ParseHeaderTag(line);

            if (headerProperties.ContainsKey(propertyValue.property))
            {
                parsedHeaderProperties.Add(propertyValue.property, propertyValue.value);
            }
            else
            {
                extraHeaderProperties.Add(propertyValue.property, propertyValue.value);
            }
        }

        return ToYamlHeader(parsedHeaderProperties, extraHeaderProperties);

        (string property, string value) ParseHeaderTag(string tag)
        {
            tag = tag.Trim();
            var index = tag.IndexOf(':');
            var property = tag.Substring(0, index);
            var value = tag.Substring(index+1).Trim();
            return (property, value);
        }

        PropertyInfo? GetPropertyInfo(Type type, string propertyName)
        {
            var property = type.GetProperty(propertyName,
                BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);

            if (property is not null) return property;

            foreach (var prop in type.GetProperties())
            {
                var attribute = prop.GetCustomAttribute<YamlNameAttribute>();

                if (attribute is not null)
                {
                    if (attribute.Name == propertyName)
                    {
                        return prop;
                    }
                }
            }

            return null;
        }

        YamlHeader ToYamlHeader(Dictionary<string, object> source, Dictionary<string, string> extras)
        {
            var header = new YamlHeader();
            var yamlHeaderType = header.GetType();

            foreach (var item in source)
            {
                try
                {
                    var key = item.Key.Replace("_", string.Empty);
                    var property = GetPropertyInfo(yamlHeaderType, key);
                    if (property?.PropertyType == typeof(string))
                    {
                        property.SetValue(header, (string)item.Value, null);
                    }

                    if (property?.PropertyType == typeof(bool?))
                    {
                        var value = bool.Parse((string)item.Value);
                        property.SetValue(header, value, null);
                    }

                    if (property?.PropertyType == typeof(DateTime?))
                    {
                        var value = (string)item.Value;
                        var date = value is "draft" or "true" or "false"
                            ? DateTime.MinValue
                            : DateTime.ParseExact((string)item.Value, "dd/MM/yyyy", CultureInfo.InvariantCulture);
                        property.SetValue(header, date, null);
                    }

                    if (property?.PropertyType == typeof(List<string>))
                    {
                        var list = ((string)item.Value)
                            .Replace("[", string.Empty)
                            .Replace("]", string.Empty)
                            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                            .ToList();
                        property.SetValue(header, list, null);
                    }

                    header.Extras = extras;
                }
                catch (Exception e)
                {
                    var message = $"Error deserialising property '{item.Key}'";
                    throw new YamlConvertException(message, e);
                }
            }

            return header;
        }
    }

    private static Dictionary<string, object?> GetYamlHeaderProperties(YamlHeader? header = null)
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