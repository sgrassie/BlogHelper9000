namespace BlogHelper9000.Commands;

public class BaseInput
{
    public string BaseDirectoryFlag { get; set; } = AppContext.BaseDirectory;
    public bool DraftFlag { get; set; } = true;
    public string Title { get; set; } = string.Empty;
    public IEnumerable<string> Tags { get; set; } = Enumerable.Empty<string>();
    public string? SeriesFlag { get; set; }
    public bool IsFeaturedFlag { get; set; } = false;
    public bool IsHiddenFlag { get; set; } = false;
    public string LayoutFlag { get; set; } = "post";
    public string? ImageFlag { get; set; }
}
