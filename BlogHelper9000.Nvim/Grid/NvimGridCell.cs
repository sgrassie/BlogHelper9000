namespace BlogHelper9000.Nvim.Grid;

/// <summary>
/// Represents a single cell in the Neovim grid.
/// </summary>
public struct NvimGridCell
{
    public string Text;
    public int HlId;

    public NvimGridCell()
    {
        Text = " ";
        HlId = 0;
    }

    public NvimGridCell(string text, int hlId)
    {
        Text = text;
        HlId = hlId;
    }
}
