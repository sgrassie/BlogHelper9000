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
    private readonly ListView _listView;
    private readonly IFileSystem _fileSystem;
    private readonly string _basePath;
    private readonly ObservableCollection<string> _items = new();

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
        _listView.Accepted += OnItemAccepted;

        Add(_listView);
    }

    public void RefreshFiles()
    {
        _items.Clear();

        AddSection("_drafts", _fileSystem.Path.Combine(_basePath, "_drafts"));
        AddSection("_posts", _fileSystem.Path.Combine(_basePath, "_posts"));

        _listView.SetSource(_items);

        void AddSection(string label, string path)
        {
            _items.Add($"[{label}]");
            if (_fileSystem.Directory.Exists(path))
            {
                foreach (var file in _fileSystem.Directory.EnumerateFiles(path, "*.md", SearchOption.AllDirectories)
                             .OrderByDescending(f => f))
                {
                    _items.Add($"  {_fileSystem.Path.GetFileName(file)}");
                }
            }
        }
    }

    public string? GetSelectedFilePath()
    {
        var selected = _listView.SelectedItem;
        if (selected is null || selected < 0 || selected >= _items.Count)
            return null;

        var item = _items[selected.Value].Trim();
        if (item.StartsWith('[')) return null; // section header

        // Try to find the file in drafts or posts
        var draftsPath = _fileSystem.Path.Combine(_basePath, "_drafts", item);
        if (_fileSystem.File.Exists(draftsPath)) return draftsPath;

        // Search posts recursively
        var postsPath = _fileSystem.Path.Combine(_basePath, "_posts");
        if (_fileSystem.Directory.Exists(postsPath))
        {
            foreach (var file in _fileSystem.Directory.EnumerateFiles(postsPath, item, SearchOption.AllDirectories))
            {
                return file;
            }
        }

        return null;
    }

    private void OnItemAccepted(object? sender, CommandEventArgs e)
    {
        var path = GetSelectedFilePath();
        if (path is not null)
            FileSelected?.Invoke(path);
    }
}
