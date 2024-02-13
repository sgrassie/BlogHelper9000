using System.Reflection;
using BlogHelper9000.Commands.Inputs;
using BlogHelper9000.YamlParsing;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Processing;
using Color = SixLabors.ImageSharp.Color;
using HorizontalAlignment = SixLabors.Fonts.HorizontalAlignment;

namespace BlogHelper9000.Commands;

// fonts from https://fonts.google.com/specimen/Ubuntu#standard-styles

public class ImageCommand : AsyncBaseCommand<ImageInput>
{
    public ImageCommand()
    {
        Usage("Default").Arguments(a => a.Post)
            .ValidFlags(
                f => f.AuthorBrandingFlag,
                f => f.ImageQueryFlag,
                f => f.BaseDirectoryFlag);
        Usage("Update images across all posts and drafts")
            .ValidFlags(
                f => f.AuthorBrandingFlag,
                a => a.ImageQueryFlag,
                a => a.ApplyAllFlag,
                a => a.BaseDirectoryFlag);
        Usage("Update images across all posts")
            .ValidFlags(
                f => f.AuthorBrandingFlag,
                a => a.ImageQueryFlag,
                a => a.ApplyToPostsFlag,
                a => a.BaseDirectoryFlag);
        Usage("Update images across all drafts")
            .ValidFlags(
                f => f.AuthorBrandingFlag,
                a => a.ImageQueryFlag,
                a => a.ApplyToDraftsFlag,
                a => a.BaseDirectoryFlag);
    }

    protected override async Task<bool> Run(ImageInput input)
    {
        //ConsoleWriter.Write(ConsoleColor.White, "Preparing to generate...");
        var imageProcessor = new ImageProcessor(input);

        foreach (var header in GetPosts(input))
        {
            //ConsoleWriter.Write(ConsoleColor.White, $"Generating image for {header.FilePath}");
            await using var stream = await UnsplashImageFetcher.FetchImageAsync(input.ImageQueryFlag);
            await imageProcessor.GenerateImage(header, stream);
            await Task.Delay(5000);
        }

        return true;
    }

    private IEnumerable<MarkdownFile> GetPosts(ImageInput input)
    {
        if (!string.IsNullOrEmpty(input.Post))
        {
            //ConsoleWriter.WriteWithIndent(ConsoleColor.Cyan, 5, "Generating image for single draft...");
            var path = File.Exists(input.Post) ? input.Post : Path.Combine(DraftsPath, input.Post);
            yield return MarkdownHandler.LoadFile(path);
        }
        else
        {
            if (input.ApplyAllFlag)
            {
                //ConsoleWriter.WriteWithIndent(ConsoleColor.Cyan, 5, "Generating image for posts and drafts...");
                var posts = Directory.EnumerateFiles(DraftsPath, "*.md", SearchOption.AllDirectories).ToList();
                posts.AddRange(Directory.EnumerateFiles(PostsPath, "*.md", SearchOption.AllDirectories));
                foreach (var file in posts)
                {
                    yield return MarkdownHandler.LoadFile(file);
                }
            }
            else if (input.ApplyToDraftsFlag)
            {
                //ConsoleWriter.WriteWithIndent(ConsoleColor.Cyan, 5, "Generating image for all drafts...");
                foreach (var file in Directory.EnumerateFiles(DraftsPath, "*.md", SearchOption.AllDirectories))
                {
                    yield return MarkdownHandler.LoadFile(file);
                }
            }
            else
            {
                //ConsoleWriter.WriteWithIndent(ConsoleColor.Cyan, 5, "Generating image for all posts...");
                foreach (var file in Directory.EnumerateFiles(PostsPath, "*.md", SearchOption.AllDirectories))
                {
                    yield return MarkdownHandler.LoadFile(file);
                }
            }
        }
    }

    private class ImageProcessor
    {
        private const string ImagesByUnsplash = "Background image by Unsplash";
        private string _rootImagesPath;
        private readonly ImageInput _input;

        public ImageProcessor(ImageInput input)
        {
            _input = input;
            _rootImagesPath = Path.Combine(input.BaseDirectoryFlag, "assets", "images");
        }

        public async Task GenerateImage(MarkdownFile markdownFile, Stream unsplashSource)
        {
            //ConsoleWriter.WriteWithIndent(ConsoleColor.Cyan, 5, "Generating image...");

            var unsplashAttributionFont = FontHandler.GetFont("Ubuntu", 15, FontStyle.Italic);
            var mainFont = FontHandler.GetFont("Ubuntu", 90);
            var logo = await Image.LoadAsync(Path.Combine(_rootImagesPath, _input.AuthorBrandingFlag));
            var unsplash = await Image.LoadAsync(unsplashSource);
            var unsplashWidth = unsplash.Width;
            var unsplashHeight = unsplash.Height;

            unsplash.Mutate(x =>
            {
                //ConsoleWriter.WriteWithIndent(ConsoleColor.Cyan, 5, "Adding logo...");
                var position = new Point(0, 0);
                x.DrawImage(logo, position, opacity: 1f);
            });

            unsplash.Mutate(x =>
            {
                //ConsoleWriter.WriteWithIndent(ConsoleColor.Cyan, 5, "Adding Unsplash attribution...");
                var measure = TextMeasurer.MeasureSize(ImagesByUnsplash, new TextOptions(unsplashAttributionFont));

                // x.DrawText(
                //     new DrawingOptions
                //     {
                //         GraphicsOptions = new GraphicsOptions { Antialias = true },
                //     },
                //     new TextOptions(unsplashAttributionFont)
                //     {
                //         Origin = new PointF(unsplashWidth - measure.Width, unsplashHeight - measure.Height),
                //         HorizontalAlignment = HorizontalAlignment.Center
                //     },
                //     ImagesByUnsplash,
                //     new SolidBrush(Color.WhiteSmoke),
                //     new SolidPen(Color.WhiteSmoke, 1)
                // );
            });

            // text shadow
            unsplash.Mutate(x =>
            {
                //ConsoleWriter.WriteWithIndent(ConsoleColor.Cyan, 5, "Adding post description text shadow...");
                // x.DrawText(
                //     new DrawingOptions
                //     {
                //         GraphicsOptions = new GraphicsOptions { Antialias = true }
                //     },
                //     new TextOptions(mainFont)
                //     {
                //         Origin = new PointF(603, 203),
                //         WrappingLength = 1000f,
                //         HorizontalAlignment = HorizontalAlignment.Center
                //     },
                //     markdownFile.Metadata.Title,
                //     new SolidBrush(Color.Black),
                //     new Pen(Color.Black, 1)
                // );
            });

            unsplash.Mutate(x =>
            {
                // ConsoleWriter.WriteWithIndent(ConsoleColor.Cyan, 5, "Adding post description text...");
                // x.DrawText(
                //     new DrawingOptions
                //     {
                //         GraphicsOptions = new GraphicsOptions { Antialias = true }
                //     },
                //     new TextOptions(mainFont)
                //     {
                //         Origin = new PointF(600, 200),
                //         WrappingLength = 1000f,
                //         HorizontalAlignment = HorizontalAlignment.Center
                //     },
                //     markdownFile.Metadata.Title,
                //     new SolidBrush(Color.WhiteSmoke),
                //     new Pen(Color.WhiteSmoke, 1)
                // );
            });

            //ConsoleWriter.WriteWithIndent(ConsoleColor.Cyan, 5, "Saving generated image...");
            var (fileName, savePath) = GetSavePath(markdownFile);
            var markdownPath = $"/assets/images/{fileName}";
            markdownFile.Metadata.FeaturedImage = markdownPath;
            markdownFile.Metadata.Image = markdownPath;
            MarkdownHandler.UpdateFile(markdownFile);
            await unsplash.SaveAsWebpAsync(savePath);
        }

        private (string Filename, string SavePath) GetSavePath(MarkdownFile markdownFile)
        {
            var fileName = Path.GetFileName(markdownFile.FilePath);
            fileName = Path.ChangeExtension(fileName, "webp");
            var savePath = Path.Combine(_rootImagesPath, fileName);
            return (fileName, savePath);
        }
    }

    private static class FontHandler
    {
        private static readonly Assembly Assembly = typeof(ImageCommand).Assembly;

        private static FontCollection _fontCollection;

        static FontHandler()
        {
            //ConsoleWriter.WriteWithIndent(ConsoleColor.Cyan, 5, "Loading fonts...");
            _fontCollection = new FontCollection();

            foreach (var ttf in Assembly.GetManifestResourceNames().Where(x => x.EndsWith(".ttf")))
            {
                using var stream = Assembly.GetManifestResourceStream(ttf);
                if (stream != null) _fontCollection.Add(stream);
            }
        }

        public static Font GetFont(string fontName, int fontSize = 125, FontStyle style = FontStyle.Bold)
        {
            if (_fontCollection.TryGet(fontName, out var family))
            {
                //ConsoleWriter.WriteWithIndent(ConsoleColor.Cyan, 5, $"Loading {fontName} font with size {fontSize}...");
                return family.CreateFont(fontSize, style);
            }

            throw new ArgumentException($"Could not create {fontName} font.", nameof(fontName));
        }
    }

    private class UnsplashImageFetcher : IDisposable
    {
        private readonly string _requestUrl;
        private readonly HttpClient _httpClient;

        private UnsplashImageFetcher(string query)
        {
            _requestUrl = $"https://source.unsplash.com/random/1280x720?{query}";
            _httpClient = new HttpClient();
        }

        public static Task<Stream> FetchImageAsync(string query)
        {
            //ConsoleWriter.WriteWithIndent(ConsoleColor.Cyan, 5, $"Loading random Unsplah image for '{query}'...");
            var fetcher = new UnsplashImageFetcher(query);
            return fetcher.FetchImage();
        }

        private Task<Stream> FetchImage()
        {
            return _httpClient.GetStreamAsync(_requestUrl);
        }

        public void Dispose()
        {
            _httpClient.Dispose();
        }
    }
}