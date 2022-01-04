namespace BlogHelper9000.Commands.Inputs;

public class ListInput : BaseInput
{
    public string FilterFlag { get; set; } = "*.md";
    public bool ShowDetailFlag { get; set; }
}