using System.Collections.ObjectModel;
using System.IO.Abstractions;
using BlogHelper9000.Core;
using Microsoft.Extensions.Options;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace BlogHelper9000.Tui.Views;

/// <summary>
/// Jekyll-aware file browser using ListView.
/// Shows _posts, _drafts, and assets directories.
/// </summary>
public class FileBrowserView : FrameView
{
    internal readonly ListView _listView;
    private readonly IFileSystem _fileSystem;
    private readonly string _basePath;
    internal readonly ObservableCollection<string> _items = new();
    internal readonly List<string?> _filePaths = new();
    private readonly HashSet<string> _modifiedFiles = new();

    public event Action<string>? FileSelected;

    public FileBrowserView(IFileSystem fileSystem, IOptions<BlogHelperOptions> options)
    {
        _fileSystem = fileSystem;
        _basePath = options.Value.BaseDirectory;

        Title = "Files";
        Width = Dim.Percent(25);
        Height = Dim.Fill();
        CanFocus = true;

        _listView = new ListView
        {
            Width = Dim.Fill(),
            Height = Dim.Fill(),
        };
        _listView.KeyDown += (_, e) =>
        {
            if (e.KeyCode == KeyCode.Enter)
            {
                var path = GetSelectedFilePath();
                if (path is not null)
                    FileSelected?.Invoke(path);
                e.Handled = true;
            }
        };

        Add(_listView);
    }

    public void RefreshFiles()
    {
        _items.Clear();
        _filePaths.Clear();

        AddSection("_drafts", _fileSystem.Path.Combine(_basePath, "_drafts"));
        AddSection("_posts", _fileSystem.Path.Combine(_basePath, "_posts"));

        _listView.SetSource(_items);

        void AddSection(string label, string path)
        {
            _items.Add($"[{label}]");
            _filePaths.Add(null);
            if (_fileSystem.Directory.Exists(path))
            {
                foreach (var file in _fileSystem.Directory.EnumerateFiles(path, "*.md", SearchOption.AllDirectories)
                             .OrderByDescending(f => f))
                {
                    var prefix = _modifiedFiles.Contains(file) ? "* " : "  ";
                    _items.Add($"{prefix}{_fileSystem.Path.GetFileName(file)}");
                    _filePaths.Add(file);
                }
            }
        }
    }

    public void MarkFileModified(string fullPath)
    {
        if (!_modifiedFiles.Add(fullPath)) return;
        UpdateDisplayItem(fullPath);
    }

    public void MarkFileSaved(string fullPath)
    {
        if (!_modifiedFiles.Remove(fullPath)) return;
        UpdateDisplayItem(fullPath);
    }

    private void UpdateDisplayItem(string fullPath)
    {
        var index = _filePaths.IndexOf(fullPath);
        if (index < 0) return;

        var fileName = _fileSystem.Path.GetFileName(fullPath);
        var prefix = _modifiedFiles.Contains(fullPath) ? "* " : "  ";
        _items[index] = $"{prefix}{fileName}";
    }

    public string? GetSelectedFilePath()
    {
        var selected = _listView.SelectedItem;
        if (selected is null || selected < 0 || selected >= _filePaths.Count)
            return null;

        return _filePaths[selected.Value];
    }

}
