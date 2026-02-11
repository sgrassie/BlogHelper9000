using System.IO.Abstractions;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace BlogHelper9000.Tui.Views;

/// <summary>
/// Placeholder editor surface using Terminal.Gui's TextView.
/// Will be replaced by NvimEditorView in Milestone 4.
/// </summary>
public class EditorSurface : FrameView
{
    private readonly TextView _textView;
    private readonly IFileSystem _fileSystem;
    private string? _currentFilePath;

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

        Add(_textView);
    }

    public string? CurrentFilePath => _currentFilePath;

    public void LoadFile(string path)
    {
        if (!_fileSystem.File.Exists(path)) return;

        _currentFilePath = path;
        var content = _fileSystem.File.ReadAllText(path);
        _textView.Text = content;
        Title = $"Editor - {_fileSystem.Path.GetFileName(path)}";
    }

    public void Clear()
    {
        _currentFilePath = null;
        _textView.Text = "";
        Title = "Editor";
    }
}
