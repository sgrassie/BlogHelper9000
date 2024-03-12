using BlogHelper9000.Helpers;
using SixLabors.ImageSharp;

namespace BlogHelper9000.Imager;

public interface IImageProcessor
{
    Task Process(MarkdownFile postMarkdown, Stream imageSource, string brandingPath);
}