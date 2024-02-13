using System.CommandLine;
using System.IO.Abstractions;
using BlogHelper9000.YamlParsing;
using Command = System.CommandLine.Command;

namespace BlogHelper9000.CommandLine;

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

    private class AddCommandHandler
    {
        private readonly IFileSystem _fileSystem;
        private readonly string _blogBaseDirectory;
        private readonly IConsole _console;

        public AddCommandHandler(IFileSystem fileSystem, string blogBaseDirectory, IConsole console)
        {
            _fileSystem = fileSystem;
            _blogBaseDirectory = blogBaseDirectory;
            _console = console;
        }

        public void Execute(string title, string[] tags, string featuredImage, bool isFeatured, bool isHidden, bool isDraft)
        {
            var postFile = CreatePostFilePath(title, isDraft);
            AddYamlHeader(postFile, title, tags, featuredImage, isFeatured, isHidden, isDraft);

            ConsoleWriter.Write("Added new file {0} as draft", postFile);
        }

        private string CreatePostFilePath(string title, bool isDraft)
        {
            var fileName = title.Replace(" ", "-").ToLowerInvariant();
            var newPostFilePath = isDraft
                ? _fileSystem.Path.ChangeExtension(_fileSystem.Path.Combine(_blogBaseDirectory, "_drafts", fileName),
                    "md")
                : _fileSystem.Path.ChangeExtension(_fileSystem.Path.Combine(_blogBaseDirectory, "_posts", fileName),
                    "md");

            return newPostFilePath;
        }

        private void AddYamlHeader(string path, string title, string[] tags, string featuredImage, bool isFeatured,
            bool isHidden, bool isDraft)
        {
            var yamlHeader = new YamlHeader
            {
                Title = title,
                Tags = tags.ToList(),
                FeaturedImage = featuredImage,
                IsFeatured = isFeatured,
                IsHidden = isHidden,
                IsPublished = !isDraft
            };

            var yamlHeaderText = YamlConvert.Serialise(yamlHeader);

            _fileSystem.File.AppendAllText(path, yamlHeaderText);
        }
    }
}