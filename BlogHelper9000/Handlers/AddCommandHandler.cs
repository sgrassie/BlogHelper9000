using System.CommandLine;
using System.IO.Abstractions;
using BlogHelper9000.Helpers;
using BlogHelper9000.YamlParsing;

namespace BlogHelper9000.Handlers;

public class AddCommandHandler
{
    private PostManager _postManager;
    private readonly IConsole _console;

    public AddCommandHandler(IFileSystem fileSystem, string blogBaseDirectory, IConsole console)
    {
        _postManager = new PostManager(fileSystem, blogBaseDirectory);
        _console = console;
    }

    public void Execute(string title, string[] tags, string featuredImage, bool isFeatured, bool isHidden, bool isDraft)
    {
        var postFile = CreatePostFilePath(title, isDraft);
        AddYamlHeader(postFile, title, tags, featuredImage, isFeatured, isHidden, isDraft);

        //ConsoleWriter.Write("Added new file {0} as draft", postFile);
    }

    private string CreatePostFilePath(string title, bool isDraft)
    {
        var newPostFilePath = isDraft
            ? _postManager.CreateDraftPath(title)
            : _postManager.CreatePostPath(title);

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

        var yamlHeaderText = _postManager.YamlConvert.Serialise(yamlHeader);

        _postManager.FileSystem.File.AppendAllText(path, yamlHeaderText);
    }
}