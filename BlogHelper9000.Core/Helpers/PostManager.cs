using System.Diagnostics.CodeAnalysis;
using System.IO.Abstractions;
using BlogHelper9000.Core.YamlParsing;
using Microsoft.Extensions.Options;

namespace BlogHelper9000.Core.Helpers;

public class PostManager
{
    public PostManager(IFileSystem fileSystem, MarkdownHandler markdownHandler, IOptions<BlogHelperOptions> options)
    {
        _fileSystem = fileSystem;
        _markdownHandler = markdownHandler;
        _basePath = options.Value.BaseDirectory;
    }

    private IFileSystem _fileSystem;
    private readonly MarkdownHandler _markdownHandler;
    private string _basePath;
    private const string DefaultAuthorBrandingFile = "branding_logo.png";
    private const string DraftsFolder = "_drafts";
    private const string PostsFolder = "_posts";
    private const string ImagesFolder = "assets/images";


    public IFileSystem FileSystem => _fileSystem;
    public MarkdownHandler Markdown => _markdownHandler;
    public YamlConvert YamlConvert => _markdownHandler.YamlConvert;
    public string BasePath => _basePath;
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

    public bool TryFindAuthorBranding(string branding, out string brandingPath)
    {
        if (!string.IsNullOrEmpty(branding))
        {
            if (FileSystem.File.Exists(branding))
            {
                brandingPath = branding;
                return true;
            }

            var path = FileSystem.Path.Combine(Images, FileSystem.Path.GetFileName(branding));

            if (FileSystem.File.Exists(path))
            {
                brandingPath = path;
                return true;
            }
        }

        var defaultBrandingPath = FileSystem.Path.Combine(Images, DefaultAuthorBrandingFile);

        if (FileSystem.File.Exists(defaultBrandingPath))
        {
            brandingPath = defaultBrandingPath;
            return true;
        }

        brandingPath = string.Empty;
        return false;
    }

    private string MakeFileName(string title)
    {
        return title.Replace(" ", "-").ToLowerInvariant();
    }
}
