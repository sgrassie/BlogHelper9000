using System.Diagnostics.CodeAnalysis;
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
    
    public IReadOnlyList<YamlHeader> LoadYamlHeaderForAllPosts()
    {
        var allPosts = new List<YamlHeader>();

        var posts = FileSystem.Directory.EnumerateFiles(Drafts, "*.md", SearchOption.AllDirectories).ToList();
        var drafts = FileSystem.Directory.EnumerateFiles(Posts, "*.md", SearchOption.AllDirectories).ToList();

        allPosts.AddRange(posts.Select(GetHeaderWithOriginalFilename));
        allPosts.AddRange(drafts.Select(GetHeaderWithOriginalFilename));

        return allPosts.OrderBy(x => x.PublishedOn).ToList().AsReadOnly();

        YamlHeader GetHeaderWithOriginalFilename(string f)
        {
            var lines = FileSystem.File.ReadAllLines(f);
            var header = YamlConvert.Deserialise(lines);
            var fileInfo = new FileInfo(f);
            header.Extras.Add("originalFilename", fileInfo.Name);
            header.Extras.Add("lastUpdated", $"{fileInfo.LastWriteTime:dd/MM/yyyy hh:mm:ss}");
            return header;
        }
    }
    
    public bool TryFindPost(string title, [NotNullWhen(returnValue: true)]out MarkdownFile? markdownFile)
    {
        var potentialDraftPath = CreateDraftPath(title);
        var potentialPostsPath = CreatePostPath(title);
        
        if (FileSystem.File.Exists(potentialDraftPath))
        {
            markdownFile = Markdown.LoadFile(potentialDraftPath);
            return true;
        }

        if (FileSystem.File.Exists(potentialPostsPath))
        {
            markdownFile = Markdown.LoadFile(potentialPostsPath);
            return true;
        }

        markdownFile = null;
        return false;
    }

    private string MakeFileName(string title)
    {
        return title.Replace(" ", "-").ToLowerInvariant();
    }
}