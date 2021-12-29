using System.Globalization;
using System.IO;
using BlogHelper9000.YamlParsing;

namespace BlogHelper9000.Commands;

public class ListCommand : BaseCommand<ListInput>
{
    public ListCommand()
    {
        Usage("List all");
    }

    protected override bool Run(ListInput input)
    {
        if (input.DraftFlag)
        {
            return Enumerate(DraftsPath, input);
        }
        else
        {
            return Enumerate(PostsPath, input);
        }
    }

    private static bool Enumerate(string? path, ListInput input)
    {
        foreach(var file in Directory.EnumerateFiles(path, input.FilterFlag, SearchOption.AllDirectories))
        {
            if (input.ShowDetailFlag)
            {
                GetPostFileHeaderDetails(file);
            }
            else
            {
                ConsoleWriter.Write(ConsoleColor.Green, file);
            }
        }

        return true;
    }

    private static void GetPostFileHeaderDetails(string path)
    {
        var contents = File.ReadAllLines(path);
        var header = YamlConvert.Deserialise(contents);

        var rawFileName = path.Split("/").Last();
        var datePart = rawFileName.Substring(0, 10);
        var date = DateTime.ParseExact(datePart, "yyyy-mm-dd", CultureInfo.InvariantCulture);
        
        ConsoleWriter.Write(ConsoleColor.Green, $"{date.ToShortDateString()} - {header.Title}");
    }
}
