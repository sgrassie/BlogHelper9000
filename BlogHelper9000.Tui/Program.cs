using System.IO.Abstractions;
using BlogHelper9000.Core;
using BlogHelper9000.Core.Helpers;
using BlogHelper9000.Core.Services;
using BlogHelper9000.Imaging;
using BlogHelper9000.Nvim;
using BlogHelper9000.Tui.Commands;
using BlogHelper9000.Tui.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;
using Terminal.Gui.Input;

var baseDirectory = args.FirstOrDefault(a => a.StartsWith("--base-directory="))
    ?.Split('=', 2).ElementAtOrDefault(1)
    ?? Directory.GetCurrentDirectory();

// Also support --base-directory <value> (space-separated)
for (var i = 0; i < args.Length - 1; i++)
{
    if (args[i] == "--base-directory")
    {
        baseDirectory = args[i + 1];
        break;
    }
}

var noNvim = args.Contains("--no-nvim");

var services = new ServiceCollection();
services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
services.AddSingleton<IFileSystem, FileSystem>();
services.AddSingleton(TimeProvider.System);
services.AddSingleton<IOptions<BlogHelperOptions>>(
    Options.Create(new BlogHelperOptions { BaseDirectory = baseDirectory }));
services.AddSingleton<MarkdownHandler>();
services.AddSingleton<PostManager>();
services.AddSingleton<IBlogService, BlogService>();
services.AddSingleton<IUnsplashClient>(sp =>
    new UnsplashClient(sp.GetRequiredService<ILoggerFactory>().CreateLogger<UnsplashClient>()));
services.AddSingleton<IImageProcessor>(sp =>
    new ImageProcessor(
        sp.GetRequiredService<ILoggerFactory>().CreateLogger<ImageProcessor>(),
        sp.GetRequiredService<PostManager>()));

if (!noNvim)
{
    services.AddSingleton<NvimClient>(sp =>
        new NvimClient(sp.GetRequiredService<ILoggerFactory>().CreateLogger<NvimClient>()));
    services.AddSingleton<NvimEditorView>(sp =>
        new NvimEditorView(
            sp.GetRequiredService<NvimClient>(),
            sp.GetRequiredService<ILoggerFactory>().CreateLogger<NvimEditorView>()));
}
else
{
    services.AddSingleton<EditorSurface>();
}

services.AddSingleton<BlogCommands>();
services.AddSingleton<CommandPalette>();

services.AddSingleton<FileBrowserView>();
services.AddSingleton<BlogWorkspaceWindow>(sp =>
{
    var fileBrowser = sp.GetRequiredService<FileBrowserView>();
    var blogCommands = sp.GetRequiredService<BlogCommands>();
    var commandPalette = sp.GetRequiredService<CommandPalette>();
    var logger = sp.GetRequiredService<ILogger<BlogWorkspaceWindow>>();

    if (noNvim)
    {
        var editor = sp.GetRequiredService<EditorSurface>();
        return new BlogWorkspaceWindow(fileBrowser, editor, blogCommands, commandPalette, logger);
    }
    else
    {
        var nvimEditor = sp.GetRequiredService<NvimEditorView>();
        return new BlogWorkspaceWindow(fileBrowser, nvimEditor, blogCommands, commandPalette, logger);
    }
});

var provider = services.BuildServiceProvider();
var logg = provider.GetRequiredService<ILoggerFactory>().CreateLogger("TUI");

// Wire callbacks on BlogCommands based on editor mode
var commands = provider.GetRequiredService<BlogCommands>();
var fileBrowserView = provider.GetRequiredService<FileBrowserView>();
commands.FilesChangedCallback = () => Application.Invoke(() => fileBrowserView.RefreshFiles());
commands.GetSelectedFilePathCallback = () => fileBrowserView.GetSelectedFilePath();

if (!noNvim)
{
    var nvimEditor = provider.GetRequiredService<NvimEditorView>();
    commands.OpenFileCallback = path =>
        Task.Run(async () =>
        {
            try { await nvimEditor.OpenFileAsync(path); }
            catch (Exception ex) { logg.LogError(ex, "Failed to open new file"); }
        });
    commands.GetActiveFilePathCallback = () => nvimEditor.CurrentBufferPath;
}
else
{
    var editor = provider.GetRequiredService<EditorSurface>();
    commands.OpenFileCallback = path => editor.LoadFile(path);
    commands.GetActiveFilePathCallback = () => editor.CurrentFilePath;
}

Application.Init();
Application.KeyBindings.Remove(Application.QuitKey);

try
{
    var workspace = provider.GetRequiredService<BlogWorkspaceWindow>();
    var commandPalette = provider.GetRequiredService<CommandPalette>();

    // Global key bindings
    Application.KeyDown += (sender, e) =>
    {
        if (e.KeyCode == KeyCode.Tab)
        {
            if (workspace.HandleTabFocusToggle())
                e.Handled = true;
        }
        else if (e.KeyCode == (KeyCode.B | KeyCode.CtrlMask))
        {
            workspace.ToggleFileBrowser();
            e.Handled = true;
        }
        else if (e.KeyCode == (KeyCode.P | KeyCode.CtrlMask))
        {
            commandPalette.Show();
            e.Handled = true;
        }
        else if (e.KeyCode == (KeyCode.Q | KeyCode.CtrlMask))
        {
            Application.RequestStop(workspace);
            e.Handled = true;
        }
    };

    Application.Run(workspace);
}
finally
{
    // Graceful Neovim shutdown
    if (!noNvim)
    {
        var nvim = provider.GetRequiredService<NvimClient>();
        await nvim.ShutdownAsync();
        await nvim.DisposeAsync();
    }

    Application.Shutdown();
}
