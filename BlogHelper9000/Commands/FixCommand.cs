using BlogHelper9000.Commands.Binders;
using BlogHelper9000.Handlers;
using BlogHelper9000.Helpers;
using Command = System.CommandLine.Command;

namespace BlogHelper9000.Commands;

public class FixCommand : Command
{
    public FixCommand(IFileSystem fileSystem) : base("fix", "Fixes various things about the blog posts")
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
        
        this.SetHandler((status, description, tags, baseDirectory, console) =>
        {
            var fixCommandHandler = new FixCommandHandler(new PostManager(fileSystem, baseDirectory), console);
            fixCommandHandler.Execute(status, description, tags);
        }, statusOption, descriptionOption, tagsOption, GlobalOptions.BaseDirectoryOption, Bind.FromServiceProvider<IConsole>());
    }
}