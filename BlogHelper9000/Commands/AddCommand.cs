using System.CommandLine;
using System.IO.Abstractions;
using BlogHelper9000.Handlers;
using Command = System.CommandLine.Command;

namespace BlogHelper9000.Commands;

internal sealed class AddCommand : Command
{
    private readonly IFileSystem _fileSystem;
    private readonly Option<string> _baseDirectory;

    public AddCommand(IFileSystem fileSystem, Option<string> baseDirectory)
        : base("add", "Adds a new blog post")
    {
        _fileSystem = fileSystem;
        _baseDirectory = baseDirectory;
        var titleArgument = new Argument<string>("title", "The title of the new blog post");
        var tagsArgument = new Argument<string[]>("tags", "The tags for the new blog post");
        AddArgument(titleArgument);
        AddArgument(tagsArgument);

        var draftOption = new Option<bool>(
            name: "--draft",
            description: "Adds the post as a draft");
        
        var isFeaturedOption = new Option<bool>(
            name: "--is-featured",
            description: "Sets the post as a featured post");
        
        var isHidden = new Option<bool>(
            name: "--is-hidden",
            description: "Sets whether the post is hidden or not");
        
        var featuredImageOption = new Option<string>(
            name: "--featured-image",
            description: "Sets the featured image path");
        
        AddOption(draftOption);
        AddOption(isFeaturedOption);
        AddOption(isHidden);
        AddOption(featuredImageOption);

        this.SetHandler((title, tags, draft, featuredImage, isFeatured, hidden, blogBaseDir, console) =>
            {
                var handler = new AddCommandHandler(_fileSystem, blogBaseDir, console);
                handler.Execute(title, tags, featuredImage, isFeatured, hidden, draft);
            }, 
            titleArgument, tagsArgument, draftOption, featuredImageOption, isFeaturedOption, isHidden, _baseDirectory, Bind.FromServiceProvider<IConsole>());
    }
}