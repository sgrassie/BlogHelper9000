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
        
        AddGlobalOption(GlobalOptions.BaseDirectoryOption);

        AddCommand(new AddCommand(_fileSystem));
        AddCommand(new InfoCommand(_fileSystem));
        AddCommand(new ImageCommand());
        AddCommand(new PublishCommand(_fileSystem));
        AddCommand(new FixCommand(_fileSystem));
    }
}   