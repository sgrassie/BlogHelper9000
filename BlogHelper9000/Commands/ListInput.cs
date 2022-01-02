namespace BlogHelper9000.Commands;

public class ListInput : BaseInput
{
    public string FilterFlag { get; set; } = "*.md";
    public bool ShowDetailFlag { get; set; }
}