using System.CommandLine;

namespace BlogHelper9000.CommandLine;

public class CommandFactory
{
    public RootCommand RootCommand()
    {
        var baseDirectory = new Option<string>(
            name: "--base-directory",
            description: "The base directory of the blog.");
        baseDirectory.AddAlias("-b");
        
        var rootCommand = new RootCommand
        {
            new AddCommand(baseDirectory),
            new ImageCommand()
        };

        rootCommand.AddGlobalOption(baseDirectory);
        return rootCommand;
    }
}   