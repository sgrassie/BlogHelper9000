using System.CommandLine;

namespace BlogHelper9000.Tests.Commands;

public abstract class CommandTestsBase
{
    protected Option<string> BasePathOption()
    {
        var option = new Option<string>(
            name: "--base-directory",
            description: "The base directory",
            getDefaultValue: () => string.Empty);
        option.AddAlias("-b");

        return option;
    }
}