using System.Reflection;

namespace BlogHelper9000.YamlParsing;

public class YamlParser
{
    public YamlHeader Parse(string[] fileContent)
    {
        var headerStartMarkerFound = false;
        var headerEndMarkerFound = false;
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

                if (!headerEndMarkerFound)
                {
                    headerEndMarkerFound = true;
                    break;
                }
            }
            
            yamlBlock.Add(line);
        }

        if (headerEndMarkerFound)
        {
            return ParseYamlHeader(yamlBlock);
        }
        
        return new YamlHeader();
    }

    private YamlHeader ParseYamlHeader(IEnumerable<string> yamlHeader)
    {
        var parsedHeaderProperties = new Dictionary<string, string>();
        var headerProperties = GetYamlHeaderProperties();
        
        foreach (var line in yamlHeader)
        {
            var entry = line.Trim().Split(':', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            var headerName = entry[0];
            var headerValue = entry[1];

            if (headerProperties.ContainsKey(headerName))
            {
                parsedHeaderProperties.Add(headerName, headerValue);
            }
        }

        return ToYamlHeader(parsedHeaderProperties);

        Dictionary<string, object> GetYamlHeaderProperties()
        {
            return typeof(YamlHeader)
                .GetProperties(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance)
                .ToDictionary(p =>
                {
                    var attr = p.GetCustomAttribute<YamlNameAttribute>();

                    return attr is not null ? attr.Name.ToLower() : p.Name.ToLower();
                }, p => new object());
        }

        PropertyInfo? GetPropertyInfo(Type type, string propertyName)
        {
            var property = type.GetProperty(propertyName, BindingFlags.IgnoreCase |  BindingFlags.Public | BindingFlags.Instance);

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
        
        YamlHeader ToYamlHeader(IDictionary<string, string> source)
        {
            var yamlHeader = new YamlHeader();
            var yamlHeaderType = yamlHeader.GetType();

            foreach (var item in source)
            {
               var key = item.Key.Replace("_", string.Empty);
               var property = GetPropertyInfo(yamlHeaderType, key);
               if (property.PropertyType == typeof(string))
               {
                   property.SetValue(yamlHeader, (string)item.Value, null);
               }

               if (property.PropertyType == typeof(bool))
               {
                   var value = bool.Parse(item.Value);
                   property.SetValue(yamlHeader, value, null);
               }

               if (property.PropertyType == typeof(List<>))
               {
                   var list = ((string)item.Value)
                       .Replace("[", string.Empty)
                       .Replace("]", string.Empty)
                       .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                       .ToList();
                   property.SetValue(yamlHeader, list, null);
               }
            }

            return yamlHeader;
        }
    }
}