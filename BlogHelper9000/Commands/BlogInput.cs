namespace BlogHelper9000.Commands;

public class BlogInput
{
    public string BaseDirectoryFlag { get; set; } = AppContext.BaseDirectory;
    public bool DraftFlag { get; set; }
    public string Title { get; set; }
    public string[] Tags { get; set; }
    public bool IsFeaturedFlag { get; set; } = false;
    public bool IsHiddenFlag { get; set; } = false;
    public string LayoutFlag { get; set; } = "post";
    public string? Image { get; set; }
}
