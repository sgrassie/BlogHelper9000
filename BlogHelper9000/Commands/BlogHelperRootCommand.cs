using System.CommandLine;
using System.IO.Abstractions;

namespace BlogHelper9000.Commands;

public class BlogHelperRootCommand : RootCommand
{
    private readonly IFileSystem _fileSystem;

    public BlogHelperRootCommand(IFileSystem fileSystem)
    : base("Blog Helper 9000")
    {
        _fileSystem = fileSystem;
        
        var baseDirectoryOption = new Option<string>(
            name: "--base-directory",
            description: "The base directory of the blog.");
        baseDirectoryOption.AddAlias("-b");
        AddGlobalOption(baseDirectoryOption);

        AddCommand(new AddCommand(_fileSystem, baseDirectoryOption));
        AddCommand(new InfoCommand(_fileSystem, baseDirectoryOption));
        AddCommand(new ImageCommand());
        AddCommand(new PublishCommand());
        AddCommand(new FixCommand(_fileSystem, baseDirectoryOption));
    }
}   