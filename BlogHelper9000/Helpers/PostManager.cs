using System.IO.Abstractions;
using BlogHelper9000.YamlParsing;

namespace BlogHelper9000.Helpers;

public class PostManager
{
    private const string DraftsFolder = "_drafts";
    private const string PostsFolder = "_posts";

    private MarkdownHandler _markdownHandler;
    
    public PostManager(IFileSystem fileSystem, string basePath)
    {
        FileSystem = fileSystem;
        BasePath = basePath;

        _markdownHandler = new MarkdownHandler(fileSystem);
    }

    public IFileSystem FileSystem { get; }
    public MarkdownHandler Markdown => _markdownHandler;
    public YamlConvert YamlConvert => _markdownHandler.YamlConvert;
    public string BasePath { get; }
    public string Drafts => $"{BasePath}/{DraftsFolder}";
    public string Posts => $"{BasePath}/{PostsFolder}";

    public IEnumerable<MarkdownFile> GetAllPosts()
    {
        return FileSystem
            .Directory
            .EnumerateFiles(Posts, "*.md", SearchOption.AllDirectories)
            .Select(Markdown.LoadFile);
    }

    public string CreateDraftPath(string title)
    {
        var fileName = MakeFileName(title);
        var path = FileSystem.Path.ChangeExtension(FileSystem.Path.Combine(Drafts, fileName), "md");
        return path;
    }

    public string CreatePostPath(string title)
    {
        var fileName = MakeFileName(title).ToLowerInvariant();
        var path = FileSystem.Path.ChangeExtension(FileSystem.Path.Combine(Posts, fileName), "md");
        return path;
    }

    private string MakeFileName(string title)
    {
        return title.Replace(" ", "-").ToLowerInvariant();
    }
}