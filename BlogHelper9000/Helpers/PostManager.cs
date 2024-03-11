using System.Diagnostics.CodeAnalysis;
using BlogHelper9000.YamlParsing;

namespace BlogHelper9000.Helpers;

public class PostManager
{
    private const string DraftsFolder = "_drafts";
    private const string PostsFolder = "_posts";
    private const string ImagesFolder = "assets/images";

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
    public string Images => $"{BasePath}/{ImagesFolder}";

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
    
    public bool TryFindPost(string post, [NotNullWhen(returnValue: true)]out MarkdownFile? markdownFile)
    {
        if (IsDraft(post, out var actualDraftPath))
        {
            markdownFile = Markdown.LoadFile(actualDraftPath);
            return true;
        }

        if (IsPost(post, out var actualPostPath))
        {
            markdownFile = Markdown.LoadFile(actualPostPath);
            return true;
        }

        markdownFile = null;
        return false;

        bool IsDraft(string possiblePath, out string draftPath)
        {
            if (FileSystem.File.Exists(possiblePath))
            {
                draftPath = possiblePath;
                return true;
            }

            draftPath = $"{Drafts}/{FileSystem.Path.GetFileName(possiblePath)}";
            
            if (FileSystem.File.Exists(draftPath))
            {
                return true;
            }

            return false;
        }
        
        bool IsPost(string possiblePath, out string postPath)
        {
            if (FileSystem.File.Exists(possiblePath))
            {
                postPath = possiblePath;
                return true;
            }

            postPath = $"{Posts}/{FileSystem.Path.GetFileName(possiblePath)}";
            
            if (FileSystem.File.Exists(postPath))
            {
                return true;
            }

            return false;
        }
    }

    public void UpdateMarkdown(MarkdownFile postMarkdown)
    {
        Markdown.UpdateFile(postMarkdown);
    }

    public (string fileName, string savePath) CreateImageFilePathForPost(MarkdownFile postMarkdown)
    {
        var fileName = FileSystem.Path.GetFileName(postMarkdown.FilePath);
        fileName = FileSystem.Path.ChangeExtension(fileName, "webp");
        var savePath = FileSystem.Path.Combine(Images, fileName);
        return (fileName, savePath);
    }

    private string MakeFileName(string title)
    {
        return title.Replace(" ", "-").ToLowerInvariant();
    }
}