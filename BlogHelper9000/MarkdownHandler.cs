using System.Text;
using BlogHelper9000.YamlParsing;

namespace BlogHelper9000;

public static class MarkdownHandler
{
    public static MarkdownFile LoadFile(string path)
    {
        return new MarkdownFile(path, YamlConvert.Deserialise(path));
    }

    public static void UpdateFile(MarkdownFile file)
    {
        var markerCount = 0;
        var withoutOriginalHeader = new List<string>();

        foreach (var line in EnumerateLines(file.FilePath))
        {
            if (markerCount < 2)
            {
                if (line == "---")
                {
                    markerCount++;
                }
            }
            else
            {
                withoutOriginalHeader.Add(line);
            }
        }

        var newContent = withoutOriginalHeader.Prepend(YamlConvert.Serialise(file.Metadata));

        using var writer = new StreamWriter(file.FilePath);
        for (var i = 0; i < newContent.Count(); i++)
        {
            var line = newContent.ElementAt(i);

            if (i == newContent.Count() - 1)
            {
                // last line, don't write a new line
                writer.Write(line);
            }
            else
            {
                writer.WriteLine(line);
            }
        }
    }

    // see: https://stackoverflow.com/a/55285905
    private static readonly char newLineMarker = Environment.NewLine.Last();
    private static readonly char[] newLine = Environment.NewLine.ToCharArray();
    private static readonly char eof = '\uffff';

    private static IEnumerable<string> EnumerateLines(string path)
    {
        using (var sr = new StreamReader(path))
        {
            char c;
            string line;
            var sb = new StringBuilder();

            while ((c = (char)sr.Read()) != eof)
            {
                sb.Append(c);
                if (c == newLineMarker &&
                    (line = sb.ToString()).EndsWith(Environment.NewLine))
                {
                    yield return line.Trim(newLine);

                    sb.Clear();
                    sb.Append(Environment.NewLine);
                }
            }

            if (sb.Length > 0)
                yield return sb.ToString().Trim(newLine);
        }
    }
}

public class MarkdownFile
{
    public MarkdownFile(string path, YamlHeader metadata)
    {
        FilePath = path;
        Metadata = metadata;
    }

    public string FilePath { get; set; }
    public YamlHeader Metadata { get; }
}