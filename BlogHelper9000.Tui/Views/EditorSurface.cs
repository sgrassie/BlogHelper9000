using System.IO.Abstractions;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace BlogHelper9000.Tui.Views;

/// <summary>
/// Fallback editor surface (--no-nvim mode) using Terminal.Gui's TextView.
/// </summary>
public class EditorSurface : FrameView
{
    internal readonly TextView _textView;
    private readonly IFileSystem _fileSystem;
    private string? _currentFilePath;
    private bool _isModified;
    private bool _suppressChangeTracking;

    public EditorSurface(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;

        Title = "Editor";
        Width = Dim.Fill();
        Height = Dim.Fill();
        CanFocus = true;

        _textView = new TextView
        {
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            ReadOnly = false,
        };

        _textView.ContentsChanged += (_, _) =>
        {
            if (_suppressChangeTracking || _currentFilePath is null || _isModified) return;
            _isModified = true;
            FileModified?.Invoke(_currentFilePath);
        };

        Add(_textView);
    }

    public string? CurrentFilePath => _currentFilePath;
    public bool IsModified => _isModified;

    public event Action<string>? FileModified;
    public event Action<string>? FileSaved;

    public void LoadFile(string path)
    {
        if (!_fileSystem.File.Exists(path)) return;

        _suppressChangeTracking = true;
        try
        {
            _currentFilePath = path;
            var content = _fileSystem.File.ReadAllText(path);
            _textView.Text = content;
            _isModified = false;
            Title = $"Editor - {_fileSystem.Path.GetFileName(path)}";
            SetNeedsDraw();
        }
        finally
        {
            _suppressChangeTracking = false;
        }
    }

    /// <summary>
    /// Writes the current buffer contents back to <see cref="CurrentFilePath"/>. No-op if no file is loaded.
    /// </summary>
    public void Save()
    {
        if (_currentFilePath is null) return;

        _fileSystem.File.WriteAllText(_currentFilePath, _textView.Text ?? string.Empty);
        _isModified = false;
        FileSaved?.Invoke(_currentFilePath);
    }

    public void Clear()
    {
        _currentFilePath = null;
        _textView.Text = "";
        _isModified = false;
        Title = "Editor";
    }

    public void EditUndo() => _textView.Undo();
    public void EditRedo() => _textView.Redo();
    public void EditCut() => _textView.Cut();
    public void EditCopy() => _textView.Copy();
    public void EditPaste() => _textView.Paste();
    public void EditSelectAll() => _textView.SelectAll();

    public void EditDelete()
    {
        if (_textView.SelectedLength > 0)
            _textView.Cut();
    }
}
