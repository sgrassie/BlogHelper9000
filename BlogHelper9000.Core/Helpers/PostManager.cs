using System.Diagnostics.CodeAnalysis;
using System.IO.Abstractions;
using System.Text.RegularExpressions;
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
        _pathResolver = new BlogPathResolver(fileSystem, _basePath);
    }

    private IFileSystem _fileSystem;
    private readonly MarkdownHandler _markdownHandler;
    private string _basePath;
    private readonly BlogPathResolver _pathResolver;
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

        var drafts = FileSystem.Directory.Exists(Drafts)
            ? FileSystem.Directory.EnumerateFiles(Drafts, "*.md", SearchOption.AllDirectories).ToList()
            : [];
        var posts = FileSystem.Directory.Exists(Posts)
            ? FileSystem.Directory.EnumerateFiles(Posts, "*.md", SearchOption.AllDirectories).ToList()
            : [];

        allPosts.AddRange(drafts.Select(GetHeaderWithOriginalFilename));
        allPosts.AddRange(posts.Select(GetHeaderWithOriginalFilename));

        return allPosts.OrderBy(x => x.PublishedOn).ToList().AsReadOnly();

        YamlHeader GetHeaderWithOriginalFilename(string f)
        {
            var lines = FileSystem.File.ReadAllLines(f);
            var header = YamlConvert.Deserialise(lines);
            var fileInfo = FileSystem.FileInfo.New(f);
            header.Extras["originalFilename"] = fileInfo.Name;
            header.Extras["lastUpdated"] = $"{fileInfo.LastWriteTime:dd/MM/yyyy hh:mm:ss}";
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
            if (_pathResolver.TryResolveWithinBase(possiblePath, out var resolved) && FileSystem.File.Exists(resolved))
            {
                draftPath = resolved;
                return true;
            }

            draftPath = $"{Drafts}/{FileSystem.Path.GetFileName(possiblePath)}";

            if (FileSystem.File.Exists(draftPath))
            {
                return true;
            }

            draftPath = string.Empty;
            return false;
        }

        bool IsPost(string possiblePath, out string postPath)
        {
            if (_pathResolver.TryResolveWithinBase(possiblePath, out var resolved) && FileSystem.File.Exists(resolved))
            {
                postPath = resolved;
                return true;
            }

            postPath = $"{Posts}/{FileSystem.Path.GetFileName(possiblePath)}";

            if (FileSystem.File.Exists(postPath))
            {
                return true;
            }

            postPath = string.Empty;
            return false;
        }
    }

    public void UpdateMarkdown(MarkdownFile postMarkdown)
    {
        Markdown.UpdateFile(postMarkdown);
    }

    /// <summary>
    /// Reads a post's body text. <paramref name="path"/> must already be a resolved,
    /// validated path (e.g. from <see cref="TryFindPost"/>), not a raw caller-supplied string.
    /// </summary>
    public string GetPostBody(string path) => Markdown.GetBody(path);

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
            if (_pathResolver.TryResolveWithinBase(branding, out var resolved) && FileSystem.File.Exists(resolved))
            {
                brandingPath = resolved;
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

    private static string MakeFileName(string title)
    {
        var slug = title.Trim().ToLowerInvariant();
        slug = Regex.Replace(slug, @"[\s/\\]+", "-");
        slug = Regex.Replace(slug, @"[^a-z0-9-]", "");
        slug = Regex.Replace(slug, @"-{2,}", "-").Trim('-');

        if (string.IsNullOrEmpty(slug))
            throw new ArgumentException($"Title '{title}' does not produce a usable filename.", nameof(title));

        return slug;
    }
}
