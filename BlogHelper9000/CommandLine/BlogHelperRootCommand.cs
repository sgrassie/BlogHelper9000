using System.CommandLine;
using System.IO.Abstractions;

namespace BlogHelper9000.CommandLine;

public class BlogHelperRootCommand : RootCommand
{
    private readonly IFileSystem _fileSystem;

    public BlogHelperRootCommand(IFileSystem fileSystem)
    : base("Blog Helper 9000")
    {
        _fileSystem = fileSystem;
        
        var baseDirectory = new Option<string>(
            name: "--base-directory",
            description: "The base directory of the blog.");
        baseDirectory.AddAlias("-b");
        AddGlobalOption(baseDirectory);
        
        AddCommand(new AddCommand(_fileSystem, baseDirectory));
        AddCommand(new ImageCommand());
        AddCommand(new FixCommand());
        AddCommand(new PublishCommand());
    }
}   