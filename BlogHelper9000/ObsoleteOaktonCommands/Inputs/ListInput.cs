namespace BlogHelper9000.ObsoleteOaktonCommands.Inputs;

public class ListInput : BaseInput
{
    public string FilterFlag { get; set; } = "*.md";
    public bool ShowDetailFlag { get; set; }
}