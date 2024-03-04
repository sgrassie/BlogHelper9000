using System.CommandLine;
using System.IO.Abstractions;

namespace BlogHelper9000.Commands;

public class BlogHelperRootCommand : RootCommand
{
    public BlogHelperRootCommand() : base("Blog Helper 9000")
    {
        AddGlobalOption(GlobalOptions.BaseDirectoryOption);
        AddGlobalOption(GlobalOptions.VerbosityOption);

        AddCommand(new AddCommand());
        AddCommand(new InfoCommand());
        AddCommand(new ImageCommand());
        AddCommand(new PublishCommand());
        AddCommand(new FixCommand());
    }
}   