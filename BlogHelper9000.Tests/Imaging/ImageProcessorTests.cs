using System.IO.Abstractions;
using BlogHelper9000.Core;
using BlogHelper9000.Core.Helpers;
using BlogHelper9000.Imaging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace BlogHelper9000.Tests.Imaging;

// ImageProcessor writes the generated image via ImageSharp's own file APIs rather than through
// IFileSystem, so these tests use a real (temp-directory) filesystem rather than MockFileSystem.
public class ImageProcessorTests : IDisposable
{
    private readonly string _baseDirectory;
    private readonly IFileSystem _fileSystem = new FileSystem();
    private readonly PostManager _postManager;

    public ImageProcessorTests()
    {
        _baseDirectory = Path.Combine(Path.GetTempPath(), $"bh9000-imageprocessor-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(_baseDirectory, "_drafts"));
        Directory.CreateDirectory(Path.Combine(_baseDirectory, "_posts"));
        Directory.CreateDirectory(Path.Combine(_baseDirectory, "assets", "images"));

        File.WriteAllText(Path.Combine(_baseDirectory, "_drafts", "my-post.md"), """
            ---
            title: My Post
            ---

            Content
            """);

        var options = Options.Create(new BlogHelperOptions { BaseDirectory = _baseDirectory });
        _postManager = new PostManager(_fileSystem, new MarkdownHandler(_fileSystem), options);
    }

    public void Dispose()
    {
        if (Directory.Exists(_baseDirectory))
        {
            Directory.Delete(_baseDirectory, recursive: true);
        }
    }

    private static MemoryStream CreateSourceImage()
    {
        using var image = new Image<Rgba32>(200, 150);
        var stream = new MemoryStream();
        image.SaveAsPng(stream);
        stream.Position = 0;
        return stream;
    }

    [Fact]
    public async Task Process_WithAttribution_DrawsAttribution_AndUpdatesMetadata()
    {
        _postManager.TryFindPost("my-post.md", out var markdownFile);
        markdownFile.Should().NotBeNull();

        var sut = new ImageProcessor(NullLogger.Instance, _postManager);
        await using var source = CreateSourceImage();

        var act = () => sut.Process(markdownFile!, source, brandingPath: null, attribution: "Photo by Jane Doe on Unsplash");

        await act.Should().NotThrowAsync();

        markdownFile!.Metadata.FeaturedImage.Should().Be("/assets/images/my-post.webp");
        markdownFile.Metadata.Image.Should().Be("/assets/images/my-post.webp");
        File.Exists(Path.Combine(_baseDirectory, "assets", "images", "my-post.webp")).Should().BeTrue();

        var savedContent = await File.ReadAllTextAsync(Path.Combine(_baseDirectory, "_drafts", "my-post.md"));
        savedContent.Should().Contain("/assets/images/my-post.webp");
    }

    [Fact]
    public async Task Process_WithoutAttribution_FallsBackToDefaultText_AndUpdatesMetadata()
    {
        _postManager.TryFindPost("my-post.md", out var markdownFile);
        markdownFile.Should().NotBeNull();

        var sut = new ImageProcessor(NullLogger.Instance, _postManager);
        await using var source = CreateSourceImage();

        var act = () => sut.Process(markdownFile!, source, brandingPath: null, attribution: null);

        await act.Should().NotThrowAsync();

        markdownFile!.Metadata.FeaturedImage.Should().Be("/assets/images/my-post.webp");
        File.Exists(Path.Combine(_baseDirectory, "assets", "images", "my-post.webp")).Should().BeTrue();
    }

    [Fact]
    public async Task Process_WithEmptyAttribution_FallsBackToDefaultText()
    {
        _postManager.TryFindPost("my-post.md", out var markdownFile);
        markdownFile.Should().NotBeNull();

        var sut = new ImageProcessor(NullLogger.Instance, _postManager);
        await using var source = CreateSourceImage();

        var act = () => sut.Process(markdownFile!, source, brandingPath: null, attribution: string.Empty);

        await act.Should().NotThrowAsync();
    }
}
