namespace BlogHelper9000.Commands;

public class ListInput : BlogInput
{
    public string FilterFlag { get; set; } = "*.md";
    public bool ShowDetailFlag { get; set; }
}