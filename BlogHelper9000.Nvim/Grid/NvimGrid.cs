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
    private readonly object _gridLock = new();

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

    public NvimGridCell this[int row, int col]
    {
        get { lock (_gridLock) return _cells[row, col]; }
    }

    public IReadOnlySet<int> DirtyRows => _dirtyRows;

    public void ClearDirtyRows() => _dirtyRows.Clear();

    public void MarkAllDirty()
    {
        for (var r = 0; r < Height; r++)
            _dirtyRows.Add(r);
    }

    public void Clear()
    {
        lock (_gridLock)
        {
            for (var r = 0; r < Height; r++)
            for (var c = 0; c < Width; c++)
                _cells[r, c] = new NvimGridCell();
        }
        MarkAllDirty();
    }

    public void Resize(int width, int height)
    {
        lock (_gridLock)
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
        }
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
        if (line.Row < 0 || line.Row >= Height) return;

        lock (_gridLock)
        {
            var col = line.ColStart;
            var currentHlId = 0;

            foreach (var cell in line.Cells)
            {
                var hlId = cell.HlId ?? currentHlId;
                currentHlId = hlId;

                for (var r = 0; r < cell.Repeat; r++)
                {
                    if (col >= 0 && col < Width)
                    {
                        _cells[line.Row, col] = new NvimGridCell(cell.Text, hlId);
                    }
                    col++;
                }
            }
        }

        _dirtyRows.Add(line.Row);
    }

    internal void ApplyScroll(GridScrollEvent scroll)
    {
        var top = Math.Clamp(scroll.Top, 0, Height);
        var bottom = Math.Clamp(scroll.Bottom, 0, Height);
        var left = Math.Clamp(scroll.Left, 0, Width);
        var right = Math.Clamp(scroll.Right, 0, Width);

        if (top >= bottom || left >= right) return;

        lock (_gridLock)
        {
            if (scroll.Rows > 0)
            {
                // Scroll up: move rows up, clear bottom
                var rows = Math.Min(scroll.Rows, bottom - top);
                for (var r = top; r < bottom - rows; r++)
                {
                    for (var c = left; c < right; c++)
                        _cells[r, c] = _cells[r + rows, c];
                }
                for (var r = bottom - rows; r < bottom; r++)
                {
                    for (var c = left; c < right; c++)
                        _cells[r, c] = new NvimGridCell();
                }
            }
            else if (scroll.Rows < 0)
            {
                // Scroll down: move rows down, clear top
                var amount = Math.Min(-scroll.Rows, bottom - top);
                for (var r = bottom - 1; r >= top + amount; r--)
                {
                    for (var c = left; c < right; c++)
                        _cells[r, c] = _cells[r - amount, c];
                }
                for (var r = top; r < top + amount; r++)
                {
                    for (var c = left; c < right; c++)
                        _cells[r, c] = new NvimGridCell();
                }
            }
        }

        // Mark all rows in scroll region as dirty
        for (var r = top; r < bottom; r++)
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
            var text = this[row, c].Text;
            chars[c] = string.IsNullOrEmpty(text) ? ' ' : text[0];
        }
        return new string(chars);
    }
}
