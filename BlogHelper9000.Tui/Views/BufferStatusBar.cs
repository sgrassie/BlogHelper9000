using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace BlogHelper9000.Tui.Views;

/// <summary>
/// A single-line status bar that displays open Neovim buffer filenames.
/// The active buffer is rendered in bold.
/// </summary>
public class BufferStatusBar : View
{
    internal readonly List<BufferInfo> _buffers = [];
    private int _activeHandle;

    public BufferStatusBar()
    {
        Height = 1;
        Width = Dim.Fill();
        CanFocus = false;
    }

    public void AddOrUpdateBuffer(int handle, string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return;

        var existing = _buffers.FindIndex(b => b.Handle == handle);
        if (existing >= 0)
            _buffers[existing] = new BufferInfo(handle, filePath);
        else
            _buffers.Add(new BufferInfo(handle, filePath));

        _activeHandle = handle;
        SetNeedsDraw();
    }

    public void RemoveBuffer(int handle)
    {
        var index = _buffers.FindIndex(b => b.Handle == handle);
        if (index < 0)
            return;

        _buffers.RemoveAt(index);
        SetNeedsDraw();
    }

    public void SetActiveBuffer(int handle)
    {
        _activeHandle = handle;
        SetNeedsDraw();
    }

    protected override bool OnDrawingContent(DrawContext? context)
    {
        var vp = Viewport;
        var normal = GetAttributeForRole(VisualRole.Normal);
        var normalAttr = new Attribute(normal.Foreground, normal.Background);
        var boldAttr = new Attribute(normal.Foreground, normal.Background, TextStyle.Bold);

        var col = 0;
        for (var i = 0; i < _buffers.Count && col < vp.Width; i++)
        {
            if (i > 0)
            {
                SetAttribute(normalAttr);
                var sep = " | ";
                foreach (var ch in sep)
                {
                    if (col >= vp.Width) break;
                    Move(col, 0);
                    AddRune(ch);
                    col++;
                }
            }

            var isActive = _buffers[i].Handle == _activeHandle;
            SetAttribute(isActive ? boldAttr : normalAttr);

            var name = Path.GetFileName(_buffers[i].FilePath);
            foreach (var ch in name)
            {
                if (col >= vp.Width) break;
                Move(col, 0);
                AddRune(ch);
                col++;
            }
        }

        // Clear remaining space
        SetAttribute(normalAttr);
        while (col < vp.Width)
        {
            Move(col, 0);
            AddRune(' ');
            col++;
        }

        return true;
    }

    internal record BufferInfo(int Handle, string FilePath);
}
