namespace BlogHelper9000.Nvim.UiEvents;

/// <summary>
/// Base type for all Neovim UI events received via ext_linegrid.
/// </summary>
public abstract record NvimUiEvent;

public record GridLineEvent(int Grid, int Row, int ColStart, GridLineCell[] Cells) : NvimUiEvent;

public record GridLineCell(string Text, int? HlId, int Repeat);

public record GridCursorGotoEvent(int Grid, int Row, int Col) : NvimUiEvent;

public record GridScrollEvent(int Grid, int Top, int Bottom, int Left, int Right, int Rows, int Cols) : NvimUiEvent;

public record GridResizeEvent(int Grid, int Width, int Height) : NvimUiEvent;

public record FlushEvent : NvimUiEvent;

public record HlAttrDefineEvent(int Id, HlAttrs Attrs) : NvimUiEvent;

public record HlAttrs
{
    public int? Foreground { get; init; }
    public int? Background { get; init; }
    public int? Special { get; init; }
    public bool Bold { get; init; }
    public bool Italic { get; init; }
    public bool Underline { get; init; }
    public bool Strikethrough { get; init; }
    public bool Reverse { get; init; }
}

public record DefaultColorsSetEvent(int Foreground, int Background, int Special) : NvimUiEvent;

public record ModeChangeEvent(string Mode, int ModeIndex) : NvimUiEvent;

public record GridClearEvent(int Grid) : NvimUiEvent;
