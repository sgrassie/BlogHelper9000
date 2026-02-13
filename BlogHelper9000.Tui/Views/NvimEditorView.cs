using BlogHelper9000.Nvim;
using BlogHelper9000.Nvim.Grid;
using BlogHelper9000.Nvim.UiEvents;
using BlogHelper9000.Tui.Input;
using Microsoft.Extensions.Logging;
using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.Drivers;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Attribute = Terminal.Gui.Drawing.Attribute;
using Color = Terminal.Gui.Drawing.Color;

namespace BlogHelper9000.Tui.Views;

/// <summary>
/// Custom Terminal.Gui View that renders an embedded Neovim grid.
/// Forwards keyboard input to Neovim and redraws the grid on UI events.
/// </summary>
public class NvimEditorView : View
{
    private readonly NvimClient _nvim;
    private readonly NvimGrid _grid;
    private readonly ILogger _logger;
    private readonly Dictionary<int, HlAttrs> _hlAttrs = new();
    private int _defaultFg = 0xCCCCCC;
    private int _defaultBg = 0x1E1E1E;
    private string _currentMode = "normal";
    private ModeInfo[] _modeInfoTable = [];
    private bool _cursorStyleEnabled;
    private int _currentModeIndex;
    private CursorStyle _currentCursorStyle = CursorStyle.SteadyBlock;
    private Task? _eventLoop;
    private bool _started;

    public NvimEditorView(NvimClient nvimClient, ILogger logger)
    {
        _nvim = nvimClient;
        _logger = logger;
        _grid = new NvimGrid(80, 24);

        CanFocus = true;
        Width = Dim.Fill();
        Height = Dim.Fill();
    }

    public string CurrentMode => _currentMode;

    public event Action<string>? ModeChanged;
    public event Action<string>? FileModified;
    public event Action<string>? FileSaved;
    public event Action<int, string>? BufferEntered;
    public event Action<int>? BufferDeleted;

    /// <summary>
    /// Starts the Neovim process and attaches the UI.
    /// Must be called after the view has been initialized and has a valid viewport.
    /// </summary>
    public async Task StartAsync()
    {
        if (_started) return;
        _started = true;

        await _nvim.StartAsync();

        var vp = Viewport;
        var width = Math.Max(vp.Width, 10);
        var height = Math.Max(vp.Height, 5);

        _grid.Resize(width, height);
        await _nvim.UiAttachAsync(width, height);

        // Intercept :q/:q!/:qa/:qa! — close buffer instead of quitting Neovim
        await _nvim.CommandAsync(
            "cnoreabbrev <expr> q (getcmdtype() == ':' && getcmdline() ==# 'q') ? 'bdelete' : 'q'");
        await _nvim.CommandAsync(
            "cnoreabbrev <expr> q! (getcmdtype() == ':' && getcmdline() ==# 'q!') ? 'bdelete!' : 'q!'");
        await _nvim.CommandAsync(
            "cnoreabbrev <expr> qa (getcmdtype() == ':' && getcmdline() ==# 'qa') ? 'bdelete' : 'qa'");

        // Notify the TUI when a buffer is modified or saved
        await _nvim.CommandAsync(
            "autocmd BufModifiedSet * if &modified | call rpcnotify(0, 'blog_buf_modified', expand('%:p')) | endif");
        await _nvim.CommandAsync(
            "autocmd BufWritePost * call rpcnotify(0, 'blog_buf_saved', expand('%:p'))");

        // Notify the TUI when a buffer is entered or deleted
        await _nvim.CommandAsync(
            "autocmd BufEnter * if buflisted(bufnr('%')) | call rpcnotify(0, 'blog_buf_enter', bufnr('%'), expand('%:p')) | endif");
        await _nvim.CommandAsync(
            "autocmd BufDelete * call rpcnotify(0, 'blog_buf_deleted', str2nr(expand('<abuf>')))");

        // Subscribe to viewport size changes for resize handling
        FrameChanged += (_, _) =>
        {
            _ = Task.Run(async () =>
            {
                try { await HandleResizeAsync(); }
                catch (Exception ex) { _logger.LogError(ex, "Resize failed"); }
            });
        };

        _eventLoop = Task.Run(ProcessUiEvents);
    }

    /// <summary>
    /// Opens a file in the embedded Neovim instance.
    /// </summary>
    public async Task OpenFileAsync(string path)
    {
        if (!_started) return;
        // Escape special characters in path
        var escaped = path.Replace("\\", "\\\\").Replace(" ", "\\ ");
        await _nvim.CommandAsync($":e {escaped}");
    }

    /// <summary>
    /// Handles terminal resize by notifying Neovim.
    /// </summary>
    public async Task HandleResizeAsync()
    {
        if (!_started) return;

        var vp = Viewport;
        if (vp.Width > 0 && vp.Height > 0)
        {
            _grid.Resize(vp.Width, vp.Height);
            await _nvim.UiTryResizeAsync(vp.Width, vp.Height);
        }
    }

    /// <summary>
    /// Override OnDrawingContent to render the Neovim grid cells.
    /// </summary>
    protected override bool OnDrawingContent(DrawContext? context)
    {
        var vp = Viewport;

        for (var row = 0; row < Math.Min(_grid.Height, vp.Height); row++)
        {
            for (var col = 0; col < Math.Min(_grid.Width, vp.Width); col++)
            {
                var cell = _grid[row, col];
                var isCursor = HasFocus && row == _grid.CursorRow && col == _grid.CursorCol;
                var attr = isCursor ? GetCursorAttribute(cell.HlId) : GetAttribute(cell.HlId);
                SetAttribute(attr);
                Move(col, row);

                var text = cell.Text;
                if (string.IsNullOrEmpty(text))
                    AddRune(' ');
                else
                    AddRune(text[0]);
            }
        }

        // Terminal cursor (supplements the software cursor above)
        if (_grid.CursorRow < vp.Height && _grid.CursorCol < vp.Width)
        {
            var viewportPos = new System.Drawing.Point(_grid.CursorCol, _grid.CursorRow);
            var screenPos = ViewportToScreen(ref viewportPos);
            Cursor = new Cursor { Position = screenPos, Style = _currentCursorStyle };
            SetCursorNeedsUpdate();
        }

        _grid.ClearDirtyRows();
        return true;
    }

    /// <summary>
    /// Public entry point so that parent views (e.g. BlogWorkspaceWindow)
    /// can forward keys to Neovim when this view does not have focus.
    /// </summary>
    public bool SendKeyToNvim(Key key)
    {
        return OnKeyDown(key);
    }

    /// <summary>
    /// Override OnKeyDown to forward key events to Neovim.
    /// </summary>
    protected override bool OnKeyDown(Key key)
    {
        // Intercept :q and :q! to prevent quitting the editor
        // (This is handled at a higher level if needed)

        var nvimKey = KeyTranslator.ToNvimNotation(key);
        if (nvimKey is null)
        {
            _logger.LogDebug("Untranslated key: {Key}", key);
            return false;
        }

        // Fire and forget - input is async but we don't want to block the UI
        _ = Task.Run(async () =>
        {
            try
            {
                await _nvim.InputAsync(nvimKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending key to Neovim: {Key}", nvimKey);
            }
        });

        return true; // Mark as handled
    }

    private async Task ProcessUiEvents()
    {
        try
        {
            await foreach (var evt in _nvim.UiEvents.ReadAllAsync())
            {
                switch (evt)
                {
                    case HlAttrDefineEvent hlDef:
                        _hlAttrs[hlDef.Id] = hlDef.Attrs;
                        break;

                    case DefaultColorsSetEvent colors:
                        _defaultFg = colors.Foreground;
                        _defaultBg = colors.Background;
                        break;

                    case ModeInfoSetEvent modeInfoSet:
                        _modeInfoTable = modeInfoSet.ModeInfo;
                        _cursorStyleEnabled = modeInfoSet.CursorStyleEnabled;
                        break;

                    case ModeChangeEvent modeChange:
                        _currentMode = modeChange.Mode;
                        _currentModeIndex = modeChange.ModeIndex;
                        if (_cursorStyleEnabled && _currentModeIndex < _modeInfoTable.Length)
                            _currentCursorStyle = MapCursorShape(_modeInfoTable[_currentModeIndex]);
                        Application.Invoke(() =>
                        {
                            ModeChanged?.Invoke(FormatModeDisplay(_currentMode));
                            SetNeedsDraw();
                        });
                        break;

                    case FlushEvent:
                        // Schedule a redraw on the UI thread
                        Application.Invoke(() => SetNeedsDraw());
                        break;

                    case BufferModifiedEvent modified:
                        Application.Invoke(() => FileModified?.Invoke(modified.FilePath));
                        break;

                    case BufferSavedEvent saved:
                        Application.Invoke(() => FileSaved?.Invoke(saved.FilePath));
                        break;

                    case BufferEnteredEvent entered:
                        Application.Invoke(() => BufferEntered?.Invoke(entered.BufferHandle, entered.FilePath));
                        break;

                    case BufferDeletedEvent deleted:
                        Application.Invoke(() => BufferDeleted?.Invoke(deleted.BufferHandle));
                        break;

                    default:
                        // Grid events are applied to the model
                        _grid.ApplyEvent(evt);
                        break;
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in NvimEditorView event loop");
        }
    }

    internal static CursorStyle MapCursorShape(ModeInfo modeInfo)
    {
        var hasBlink = modeInfo.BlinkWait > 0 || modeInfo.BlinkOn > 0;
        return modeInfo.CursorShape switch
        {
            "block" => hasBlink ? CursorStyle.BlinkingBlock : CursorStyle.SteadyBlock,
            "horizontal" => hasBlink ? CursorStyle.BlinkingUnderline : CursorStyle.SteadyUnderline,
            "vertical" => hasBlink ? CursorStyle.BlinkingBar : CursorStyle.SteadyBar,
            _ => CursorStyle.SteadyBlock,
        };
    }

    internal static string FormatModeDisplay(string mode) => mode.ToUpperInvariant() switch
    {
        "NORMAL" => "NORMAL",
        "INSERT" => "INSERT",
        "VISUAL" => "VISUAL",
        "VISUAL LINE" => "V-LINE",
        "VISUAL BLOCK" => "V-BLOCK",
        "REPLACE" => "REPLACE",
        "CMDLINE_NORMAL" or "CMDLINE_INSERT" => "COMMAND",
        _ => mode.ToUpperInvariant(),
    };

    private Attribute GetCursorAttribute(int hlId)
    {
        var fg = _defaultFg;
        var bg = _defaultBg;

        if (hlId != 0 && _hlAttrs.TryGetValue(hlId, out var attrs))
        {
            if (attrs.Foreground.HasValue) fg = attrs.Foreground.Value;
            if (attrs.Background.HasValue) bg = attrs.Background.Value;
        }

        // Swap fg/bg for cursor visibility
        var fgColor = new Color((bg >> 16) & 0xFF, (bg >> 8) & 0xFF, bg & 0xFF);
        var bgColor = new Color((fg >> 16) & 0xFF, (fg >> 8) & 0xFF, fg & 0xFF);

        return new Attribute(fgColor, bgColor);
    }

    private Attribute GetAttribute(int hlId)
    {
        var fg = _defaultFg;
        var bg = _defaultBg;

        if (hlId != 0 && _hlAttrs.TryGetValue(hlId, out var attrs))
        {
            if (attrs.Reverse)
            {
                fg = attrs.Background ?? _defaultBg;
                bg = attrs.Foreground ?? _defaultFg;
            }
            else
            {
                if (attrs.Foreground.HasValue) fg = attrs.Foreground.Value;
                if (attrs.Background.HasValue) bg = attrs.Background.Value;
            }
        }

        var fgColor = new Color((fg >> 16) & 0xFF, (fg >> 8) & 0xFF, fg & 0xFF);
        var bgColor = new Color((bg >> 16) & 0xFF, (bg >> 8) & 0xFF, bg & 0xFF);

        return new Attribute(fgColor, bgColor);
    }
}
