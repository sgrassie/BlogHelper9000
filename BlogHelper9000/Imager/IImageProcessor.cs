namespace BlogHelper9000.Imager;

public interface IImageProcessor
{
    Task Process(MarkdownFile postMarkdown, Stream imageSource, string authorBrandingPath);
}