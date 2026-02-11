using Microsoft.Extensions.Logging;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;
using Terminal.Gui.Input;
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

        _nvimEditor.ModeChanged += mode =>
        {
            Title = $"BlogHelper9000 [{mode}]";
            SetNeedsDraw();
        };

        Initialized += async (_, _) =>
        {
            try
            {
                await _nvimEditor.StartAsync();
                Application.Invoke(() => _editorView.SetFocus());
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

        _editorView.X = Pos.Right(_fileBrowser);
        Add(_fileBrowser, _editorView);

        _fileBrowser.RefreshFiles();
    }

    protected override bool OnKeyDown(Key key)
    {
        // When the editor view has focus, forward ALL keys to Neovim.
        // We cannot rely on Terminal.Gui's internal dispatch chain
        // (base.OnKeyDown) to propagate keys to the focused subview
        // in the develop track — so we forward explicitly.
        // Global shortcuts (Ctrl+B/P/Q) are handled by Application.KeyDown
        // which fires before OnKeyDown and sets Handled=true, so they
        // never reach here.
        if (_nvimEditor is not null && _editorView.HasFocus)
        {
            _nvimEditor.SendKeyToNvim(key);
            return true;
        }

        return base.OnKeyDown(key);
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
