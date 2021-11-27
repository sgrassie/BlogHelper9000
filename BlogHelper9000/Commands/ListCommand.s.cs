using System.IO;

namespace BlogHelper9000.Commands;

public class ListInput : BlogInput
{
    public string Filter { get; set; } = string.Empty;
}

public class ListCommand : BaseCommand<ListInput>
{
    public override bool Run(ListInput input)
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

    private static bool Enumerate(string path, ListInput input)
    {
        foreach(var file in Directory.EnumerateFiles(path, input.Filter, SearchOption.AllDirectories))
        {
            ConsoleWriter.Write(ConsoleColor.Green, file);
        }

        return true;
    }
}
