using System.CommandLine;
using System.IO.Abstractions;
using BlogHelper9000.Handlers;
using Command = System.CommandLine.Command;

namespace BlogHelper9000.Commands;

public class FixCommand : Command
{
    public FixCommand(IFileSystem fileSystem, Option<string> baseDirectoryOption) : base("fix", "Fixes various things about the blog posts")
    {
        var statusOption = new Option<bool>(
            name: "--status",
            description: "Fix the published status of a post");
        statusOption.AddAlias("-s");
        AddOption(statusOption);
        
        var descriptionOption = new Option<bool>(
            name: "--description",
            description: "Fix the description of a post");
        descriptionOption.AddAlias("-d");
        AddOption(descriptionOption);

        var tagsOption = new Option<bool>(
            name: "--tags",
            description: "Fix the tags of a post");
        tagsOption.AddAlias("-t");
        AddOption(tagsOption);

        var seriesOption = new Option<bool>(
            name: "--series",
            description: "Fix the series of a post");
        seriesOption.AddAlias("-i");
        AddOption(seriesOption);
        
        this.SetHandler((status, description, tags, series, baseDirectory, console) =>
        {
            var fixCommandHandler = new FixCommandHandler(fileSystem, baseDirectory, console);
            fixCommandHandler.Execute(status, description, tags, series);
        }, statusOption, descriptionOption, tagsOption, seriesOption, baseDirectoryOption, Bind.FromServiceProvider<IConsole>());
    }
}