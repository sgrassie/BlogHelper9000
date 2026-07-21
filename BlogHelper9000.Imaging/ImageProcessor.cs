using BlogHelper9000.Core.Helpers;
using Microsoft.Extensions.Logging;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Processing;
using Color = SixLabors.ImageSharp.Color;
using HorizontalAlignment = SixLabors.Fonts.HorizontalAlignment;

namespace BlogHelper9000.Imaging;

public class ImageProcessor(ILogger logger, PostManager postManager) : IImageProcessor
{
    private const string ImagesByUnsplash = "Background image by Unsplash";
    private readonly FontManager _fontManager = new FontManager(logger);

    public async Task Process(MarkdownFile postMarkdown, Stream imageSource, string? brandingPath, string? attribution = null)
    {
        logger.LogInformation("Generating image for {Post}", postMarkdown.Metadata.Title);
        var mainFont = _fontManager.GetFont("Ubuntu", 90);

        using var baseImage = await Image.LoadAsync(imageSource);
        var baseImageWidth = baseImage.Width;
        var baseImageHeight = baseImage.Height;

        if (brandingPath is not null)
        {
            using var logoImage = await Image.LoadAsync(brandingPath);
            AddLogo(baseImage, logoImage);
        }

        AddAttribution(baseImage, baseImageWidth, baseImageHeight, attribution);
        AddTextShadow(postMarkdown, baseImage, mainFont);
        AddDescriptionText(postMarkdown, baseImage, mainFont);

        await SaveImage(postMarkdown, baseImage);
    }

    private async Task SaveImage(MarkdownFile postMarkdown, Image baseImage)
    {
        logger.LogDebug("Saving generated image");

        var (fileName, savePath) = postManager.CreateImageFilePathForPost(postMarkdown);
        var directory = postManager.FileSystem.Path.GetDirectoryName(savePath);
        if (!string.IsNullOrEmpty(directory) && !postManager.FileSystem.Directory.Exists(directory))
            postManager.FileSystem.Directory.CreateDirectory(directory);

        await baseImage.SaveAsWebpAsync(savePath);

        var markdownPath = $"/assets/images/{fileName}";
        postMarkdown.Metadata.FeaturedImage = markdownPath;
        postMarkdown.Metadata.Image = markdownPath;
        postManager.UpdateMarkdown(postMarkdown);
    }

    private void AddDescriptionText(MarkdownFile postMarkdown, Image baseImage, Font mainFont)
    {
        baseImage.Mutate(x =>
        {
            logger.LogDebug("Adding post description text");
            x.Paint(
                new DrawingOptions
                {
                    GraphicsOptions = new GraphicsOptions { Antialias = true }
                },
                canvas => canvas.DrawText(
                    new RichTextOptions(mainFont)
                    {
                        Origin = new PointF(600, 200),
                        WrappingLength = 1000f,
                        HorizontalAlignment = HorizontalAlignment.Center
                    },
                    postMarkdown.Metadata.Title,
                    new SolidBrush(Color.WhiteSmoke),
                    new SolidPen(Color.WhiteSmoke, 1)
                )
            );
        });
    }

    private void AddTextShadow(MarkdownFile postMarkdown, Image baseImage, Font mainFont)
    {
        // text shadow
        baseImage.Mutate(x =>
        {
            logger.LogDebug("Adding post description text shadow");

            x.Paint(
                new DrawingOptions
                {
                    GraphicsOptions = new GraphicsOptions { Antialias = true }
                },
                canvas => canvas.DrawText(
                    new RichTextOptions(mainFont)
                    {
                        Origin = new PointF(603, 203),
                        WrappingLength = 1000f,
                        HorizontalAlignment = HorizontalAlignment.Center
                    },
                    postMarkdown.Metadata.Title,
                    new SolidBrush(Color.Black),
                    new SolidPen(Color.Black, 1)
                )
            );
        });
    }

    private void AddLogo(Image baseImage, Image logoImage)
    {
        baseImage.Mutate(x =>
        {
            logger.LogDebug("Adding logo");
            var position = new Point(0, 0);
            x.DrawImage(logoImage, position, opacity: 1f);
        });
    }

    private void AddAttribution(Image baseImage, int baseImageWidth, int baseImageHeight, string? attribution)
    {
        var attributionText = string.IsNullOrEmpty(attribution) ? ImagesByUnsplash : attribution;
        var unsplashAttributionFont = _fontManager.GetFont("Ubuntu", 15, FontStyle.Italic);

        baseImage.Mutate(x =>
        {
            logger.LogDebug("Adding Unsplash attribution");
            var measure = TextMeasurer.MeasureAdvance(attributionText, new TextOptions(unsplashAttributionFont));

            x.Paint(
                new DrawingOptions
                {
                    GraphicsOptions = new GraphicsOptions { Antialias = true },
                },
                canvas => canvas.DrawText(
                    new RichTextOptions(unsplashAttributionFont)
                    {
                        Origin = new PointF(baseImageWidth - measure.Width, baseImageHeight - measure.Height),
                        HorizontalAlignment = HorizontalAlignment.Center
                    },
                    attributionText,
                    new SolidBrush(Color.WhiteSmoke),
                    new SolidPen(Color.WhiteSmoke, 1)
                )
            );
        });
    }
}
