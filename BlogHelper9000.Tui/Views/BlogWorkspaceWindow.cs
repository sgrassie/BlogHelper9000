using BlogHelper9000.Tui.Commands;
using Microsoft.Extensions.Logging;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace BlogHelper9000.Tui.Views;

/// <summary>
/// Main workspace window containing the menu bar, file browser and editor.
/// </summary>
public class BlogWorkspaceWindow : Window
{
    private readonly FileBrowserView _fileBrowser;
    private readonly View _editorView;
    private readonly NvimEditorView? _nvimEditor;
    private readonly EditorSurface? _fallbackEditor;
    private readonly BufferStatusBar? _bufferStatusBar;
    private readonly ILogger _logger;
    private bool _browserVisible = true;
    private View? _lastFocusedContentView;

    /// <summary>
    /// Construct with the Neovim editor.
    /// </summary>
    public BlogWorkspaceWindow(
        FileBrowserView fileBrowser,
        NvimEditorView nvimEditor,
        BlogCommands blogCommands,
        CommandPalette commandPalette,
        ILogger<BlogWorkspaceWindow> logger)
        : this(fileBrowser, (View)nvimEditor, blogCommands, commandPalette, logger, new BufferStatusBar())
    {
        _nvimEditor = nvimEditor;

        _nvimEditor.ModeChanged += mode =>
        {
            Title = $"BlogHelper9000 [{mode}]";
            SetNeedsDraw();
        };

        _nvimEditor.FileModified += path => _fileBrowser.MarkFileModified(path);
        _nvimEditor.FileSaved += path => _fileBrowser.MarkFileSaved(path);

        _nvimEditor.BufferEntered += (handle, path) => _bufferStatusBar!.AddOrUpdateBuffer(handle, path);
        _nvimEditor.BufferDeleted += handle => _bufferStatusBar!.RemoveBuffer(handle);

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
    public BlogWorkspaceWindow(
        FileBrowserView fileBrowser,
        EditorSurface editor,
        BlogCommands blogCommands,
        CommandPalette commandPalette,
        ILogger<BlogWorkspaceWindow> logger)
        : this(fileBrowser, (View)editor, blogCommands, commandPalette, logger, null)
    {
        _fallbackEditor = editor;
    }

    private BlogWorkspaceWindow(
        FileBrowserView fileBrowser,
        View editorView,
        BlogCommands blogCommands,
        CommandPalette commandPalette,
        ILogger<BlogWorkspaceWindow> logger,
        BufferStatusBar? bufferStatusBar)
    {
        _fileBrowser = fileBrowser;
        _editorView = editorView;
        _bufferStatusBar = bufferStatusBar;
        _logger = logger;

        Title = "BlogHelper9000";
        Width = Dim.Fill();
        Height = Dim.Fill();

        var menuBar = BuildMenuBar(blogCommands, commandPalette);

        _fileBrowser.HasFocusChanged += (_, e) => { if (e.NewValue) _lastFocusedContentView = _fileBrowser; };
        _editorView.HasFocusChanged += (_, e) => { if (e.NewValue) _lastFocusedContentView = _editorView; };

        _fileBrowser.FileSelected += OnFileSelected;
        _fileBrowser.Y = Pos.Bottom(menuBar);
        _editorView.X = Pos.Right(_fileBrowser);
        _editorView.Y = Pos.Bottom(menuBar);

        if (_bufferStatusBar is not null)
        {
            _fileBrowser.Height = Dim.Fill(1);
            _editorView.Height = Dim.Fill(1);
            _bufferStatusBar.Y = Pos.AnchorEnd();
            Add(menuBar, _fileBrowser, _editorView, _bufferStatusBar);
        }
        else
        {
            Add(menuBar, _fileBrowser, _editorView);
        }

        _fileBrowser.RefreshFiles();
    }

    private MenuBar BuildMenuBar(BlogCommands blogCommands, CommandPalette commandPalette)
    {
        var fileMenu = new MenuBarItem("_File", new PopoverMenu(new View[]
        {
            new MenuItem("_Quit", Key.Q.WithCtrl, () => Application.RequestStop(this)),
        }));

        var editMenu = new MenuBarItem("_Edit", new PopoverMenu(new View[]
        {
            new MenuItem("_Undo", Key.Z.WithCtrl, () => DispatchEdit(n => n.EditUndo(), f => f.EditUndo(), null)),
            new MenuItem("_Redo", Key.Y.WithCtrl, () => DispatchEdit(n => n.EditRedo(), f => f.EditRedo(), null)),
            null!, // separator
            new MenuItem("Cu_t", Key.X.WithCtrl, () => DispatchEdit(n => n.EditCut(), f => f.EditCut(), fb => fb.CutSelectedFile())),
            new MenuItem("_Copy", Key.C.WithCtrl, () => DispatchEdit(n => n.EditCopy(), f => f.EditCopy(), fb => fb.CopySelectedFile())),
            new MenuItem("_Paste", Key.V.WithCtrl, () => DispatchEdit(n => n.EditPaste(), f => f.EditPaste(), fb => fb.PasteFile())),
            new MenuItem("_Delete", "", () => DispatchEdit(n => n.EditDelete(), f => f.EditDelete(), fb => fb.DeleteSelectedFile())),
            null!, // separator
            new MenuItem("Select _All", Key.A.WithCtrl, () => DispatchEdit(n => n.EditSelectAll(), f => f.EditSelectAll(), null)),
        }));

        var commandsMenu = new MenuBarItem("_Commands", new PopoverMenu(new View[]
        {
            new MenuItem("New _Draft", "", () => blogCommands.ExecuteCommand("New Draft")),
            new MenuItem("New _Post", "", () => blogCommands.ExecuteCommand("New Post")),
            new MenuItem("Pu_blish Draft", "", () => blogCommands.ExecuteCommand("Publish Draft")),
            new MenuItem("Blog _Info", "", () => blogCommands.ExecuteCommand("Blog Info")),
            null!, // separator
            new MenuItem("Fix Metadata: _Status", "", () => blogCommands.ExecuteCommand("Fix Metadata: Status")),
            new MenuItem("Fix Metadata: D_escription", "", () => blogCommands.ExecuteCommand("Fix Metadata: Description")),
            new MenuItem("Fix Metadata: _Tags", "", () => blogCommands.ExecuteCommand("Fix Metadata: Tags")),
            null!, // separator
            new MenuItem("_Delete File", "", () => blogCommands.ExecuteCommand("Delete File")),
        }));

        var viewMenu = new MenuBarItem("_View", new PopoverMenu(new View[]
        {
            new MenuItem("Toggle File _Browser", Key.B.WithCtrl, () => ToggleFileBrowser()),
            new MenuItem("Command _Palette", Key.P.WithCtrl, () => commandPalette.Show()),
        }));

        var helpMenu = new MenuBarItem("_Help", new PopoverMenu(new View[]
        {
            new MenuItem("_About", "", () => ShowAbout()),
        }));

        return new MenuBar(new[] { fileMenu, editMenu, commandsMenu, viewMenu, helpMenu });
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

    private void DispatchEdit(
        Action<NvimEditorView>? nvimAction,
        Action<EditorSurface>? fallbackAction,
        Action<FileBrowserView>? fileBrowserAction)
    {
        if (_lastFocusedContentView == _fileBrowser && fileBrowserAction is not null)
        {
            fileBrowserAction(_fileBrowser);
            return;
        }

        if (_nvimEditor is not null && nvimAction is not null)
            nvimAction(_nvimEditor);
        else if (_fallbackEditor is not null && fallbackAction is not null)
            fallbackAction(_fallbackEditor);
    }

    private static void ShowAbout()
    {
        var dialog = new Dialog
        {
            Title = "About",
            Width = Dim.Percent(40),
            Height = 7,
        };

        var label = new Label
        {
            Text = "BlogHelper9000\nA Jekyll blog management tool",
            X = Pos.Center(),
            Y = Pos.Center(),
            TextAlignment = Alignment.Center,
        };

        dialog.KeyDown += (_, e) =>
        {
            if (e.KeyCode == KeyCode.Esc || e.KeyCode == KeyCode.Enter)
            {
                dialog.RequestStop();
                e.Handled = true;
            }
        };

        dialog.Add(label);
        Application.Run(dialog);
    }
}
