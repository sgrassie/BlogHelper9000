using BlogHelper9000.Nvim.UiEvents;

namespace BlogHelper9000.Nvim.Grid;

/// <summary>
/// 2D grid buffer that tracks Neovim's screen state.
/// Applies UI events and maintains dirty-row tracking for efficient redraw.
/// </summary>
public class NvimGrid
{
    private NvimGridCell[,] _cells;
    private readonly HashSet<int> _dirtyRows = new();

    public int Width { get; private set; }
    public int Height { get; private set; }
    public int CursorRow { get; private set; }
    public int CursorCol { get; private set; }

    public NvimGrid(int width, int height)
    {
        Width = width;
        Height = height;
        _cells = new NvimGridCell[height, width];
        Clear();
    }

    public NvimGridCell this[int row, int col] => _cells[row, col];

    public IReadOnlySet<int> DirtyRows => _dirtyRows;

    public void ClearDirtyRows() => _dirtyRows.Clear();

    public void MarkAllDirty()
    {
        for (var r = 0; r < Height; r++)
            _dirtyRows.Add(r);
    }

    public void Clear()
    {
        for (var r = 0; r < Height; r++)
        for (var c = 0; c < Width; c++)
            _cells[r, c] = new NvimGridCell();
        MarkAllDirty();
    }

    public void Resize(int width, int height)
    {
        var newCells = new NvimGridCell[height, width];
        var copyRows = Math.Min(Height, height);
        var copyCols = Math.Min(Width, width);

        for (var r = 0; r < copyRows; r++)
        for (var c = 0; c < copyCols; c++)
            newCells[r, c] = _cells[r, c];

        // Fill new cells with defaults
        for (var r = 0; r < height; r++)
        for (var c = 0; c < width; c++)
            if (r >= copyRows || c >= copyCols)
                newCells[r, c] = new NvimGridCell();

        _cells = newCells;
        Width = width;
        Height = height;
        MarkAllDirty();
    }

    public void ApplyEvent(NvimUiEvent evt)
    {
        switch (evt)
        {
            case GridResizeEvent resize:
                Resize(resize.Width, resize.Height);
                break;
            case GridClearEvent:
                Clear();
                break;
            case GridLineEvent line:
                ApplyLine(line);
                break;
            case GridCursorGotoEvent cursor:
                CursorRow = cursor.Row;
                CursorCol = cursor.Col;
                break;
            case GridScrollEvent scroll:
                ApplyScroll(scroll);
                break;
        }
    }

    internal void ApplyLine(GridLineEvent line)
    {
        var col = line.ColStart;
        var currentHlId = 0;

        foreach (var cell in line.Cells)
        {
            var hlId = cell.HlId ?? currentHlId;
            currentHlId = hlId;

            for (var r = 0; r < cell.Repeat; r++)
            {
                if (col < Width)
                {
                    _cells[line.Row, col] = new NvimGridCell(cell.Text, hlId);
                    col++;
                }
            }
        }

        _dirtyRows.Add(line.Row);
    }

    internal void ApplyScroll(GridScrollEvent scroll)
    {
        if (scroll.Rows > 0)
        {
            // Scroll up: move rows up, clear bottom
            for (var r = scroll.Top; r < scroll.Bottom - scroll.Rows; r++)
            {
                for (var c = scroll.Left; c < scroll.Right; c++)
                    _cells[r, c] = _cells[r + scroll.Rows, c];
            }
            for (var r = scroll.Bottom - scroll.Rows; r < scroll.Bottom; r++)
            {
                for (var c = scroll.Left; c < scroll.Right; c++)
                    _cells[r, c] = new NvimGridCell();
            }
        }
        else if (scroll.Rows < 0)
        {
            // Scroll down: move rows down, clear top
            var amount = -scroll.Rows;
            for (var r = scroll.Bottom - 1; r >= scroll.Top + amount; r--)
            {
                for (var c = scroll.Left; c < scroll.Right; c++)
                    _cells[r, c] = _cells[r - amount, c];
            }
            for (var r = scroll.Top; r < scroll.Top + amount; r++)
            {
                for (var c = scroll.Left; c < scroll.Right; c++)
                    _cells[r, c] = new NvimGridCell();
            }
        }

        // Mark all rows in scroll region as dirty
        for (var r = scroll.Top; r < scroll.Bottom; r++)
            _dirtyRows.Add(r);
    }

    /// <summary>
    /// Gets the text content of a row as a string (for debugging/testing).
    /// </summary>
    public string GetRowText(int row)
    {
        var chars = new char[Width];
        for (var c = 0; c < Width; c++)
        {
            var text = _cells[row, c].Text;
            chars[c] = string.IsNullOrEmpty(text) ? ' ' : text[0];
        }
        return new string(chars);
    }
}
