using Microsoft.Extensions.Logging;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace BlogHelper9000.Tui.Views;

/// <summary>
/// Main workspace window containing the file browser and editor.
/// </summary>
public class BlogWorkspaceWindow : Window
{
    private readonly FileBrowserView _fileBrowser;
    private readonly View _editorView;
    private readonly NvimEditorView? _nvimEditor;
    private readonly EditorSurface? _fallbackEditor;
    private readonly ILogger _logger;
    private bool _browserVisible = true;

    /// <summary>
    /// Construct with the Neovim editor.
    /// </summary>
    public BlogWorkspaceWindow(FileBrowserView fileBrowser, NvimEditorView nvimEditor, ILogger<BlogWorkspaceWindow> logger)
        : this(fileBrowser, (View)nvimEditor, logger)
    {
        _nvimEditor = nvimEditor;

        Initialized += async (_, _) =>
        {
            try
            {
                await _nvimEditor.StartAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start Neovim");
            }
        };
    }

    /// <summary>
    /// Construct with the fallback plain-text editor (--no-nvim mode).
    /// </summary>
    public BlogWorkspaceWindow(FileBrowserView fileBrowser, EditorSurface editor, ILogger<BlogWorkspaceWindow> logger)
        : this(fileBrowser, (View)editor, logger)
    {
        _fallbackEditor = editor;
    }

    private BlogWorkspaceWindow(FileBrowserView fileBrowser, View editorView, ILogger<BlogWorkspaceWindow> logger)
    {
        _fileBrowser = fileBrowser;
        _editorView = editorView;
        _logger = logger;

        Title = "BlogHelper9000";
        Width = Dim.Fill();
        Height = Dim.Fill();

        _fileBrowser.FileSelected += OnFileSelected;

        Add(_fileBrowser, _editorView);

        _fileBrowser.RefreshFiles();
    }

    public void ToggleFileBrowser()
    {
        _browserVisible = !_browserVisible;
        _fileBrowser.Visible = _browserVisible;

        if (!_browserVisible)
        {
            _editorView.X = 0;
            _editorView.SetFocus();
        }
        else
        {
            _editorView.X = Pos.Right(_fileBrowser);
            _fileBrowser.SetFocus();
        }

        SetNeedsDraw();
    }

    private void OnFileSelected(string path)
    {
        if (_nvimEditor is not null)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await _nvimEditor.OpenFileAsync(path);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to open file: {Path}", path);
                }
            });
        }
        else if (_fallbackEditor is not null)
        {
            _fallbackEditor.LoadFile(path);
        }

        _editorView.SetFocus();
    }
}
