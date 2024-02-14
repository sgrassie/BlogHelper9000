using System.IO.Abstractions;
using System.Text;
using BlogHelper9000.YamlParsing;

namespace BlogHelper9000;

public class MarkdownHandler
{
    private readonly IFileSystem _fileSystem;
    private readonly YamlConvert _yamlConvert;

    public MarkdownHandler(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
        _yamlConvert = new YamlConvert(fileSystem);
    }

    public YamlConvert YamlConvert => _yamlConvert;
    
    public MarkdownFile LoadFile(string path)
    {
        return new MarkdownFile(path, _yamlConvert.Deserialise(path));
    }

    public void UpdateFile(MarkdownFile file)
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

        var newContent = withoutOriginalHeader.Prepend(_yamlConvert.Serialise(file.Metadata));

        var f = _fileSystem.FileInfo.New(file.FilePath);
        using var writer = new StreamWriter(f.OpenWrite());
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

    private IEnumerable<string> EnumerateLines(string path)
    {
        var foo = _fileSystem.FileInfo.New(path);
        using (var sr =  new StreamReader(foo.OpenRead()))
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