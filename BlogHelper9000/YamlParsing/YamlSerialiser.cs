using System.Text;

namespace BlogHelper9000.YamlParsing;

public sealed class YamlSerialiser : SerialiserBase
{
    public string Serialise(YamlHeader header)
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
}