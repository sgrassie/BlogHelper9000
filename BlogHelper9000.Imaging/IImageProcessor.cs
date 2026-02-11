using BlogHelper9000.Core.Helpers;

namespace BlogHelper9000.Imaging;

public interface IImageProcessor
{
    Task Process(MarkdownFile postMarkdown, Stream imageSource, string brandingPath);
}
