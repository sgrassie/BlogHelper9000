using System.IO;

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
            ConsoleWriter.Write(ConsoleColor.Green, file);
        }

        return true;
    }
}
