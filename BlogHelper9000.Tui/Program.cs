using System.IO.Abstractions;
using BlogHelper9000.Core;
using BlogHelper9000.Core.Helpers;
using BlogHelper9000.Core.Services;
using BlogHelper9000.Nvim;
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

services.AddSingleton<FileBrowserView>();
services.AddSingleton<BlogWorkspaceWindow>(sp =>
{
    var fileBrowser = sp.GetRequiredService<FileBrowserView>();
    var logger = sp.GetRequiredService<ILogger<BlogWorkspaceWindow>>();

    if (noNvim)
    {
        var editor = sp.GetRequiredService<EditorSurface>();
        return new BlogWorkspaceWindow(fileBrowser, editor, logger);
    }
    else
    {
        var nvimEditor = sp.GetRequiredService<NvimEditorView>();
        return new BlogWorkspaceWindow(fileBrowser, nvimEditor, logger);
    }
});

if (!noNvim)
{
    services.AddSingleton<CommandPalette>(sp =>
        new CommandPalette(
            sp.GetRequiredService<IBlogService>(),
            sp.GetRequiredService<NvimEditorView>(),
            sp.GetRequiredService<ILoggerFactory>().CreateLogger<CommandPalette>()));
}

var provider = services.BuildServiceProvider();
var logg = provider.GetRequiredService<ILoggerFactory>().CreateLogger("TUI");

Application.Init();
Application.KeyBindings.Remove(Application.QuitKey);

try
{
    var workspace = provider.GetRequiredService<BlogWorkspaceWindow>();
    var commandPalette = noNvim ? null : provider.GetRequiredService<CommandPalette>();

    // Global key bindings
    Application.KeyDown += (sender, e) =>
    {
        if (e.KeyCode == (KeyCode.B | KeyCode.CtrlMask))
        {
            workspace.ToggleFileBrowser();
            e.Handled = true;
        }
        else if (e.KeyCode == (KeyCode.P | KeyCode.CtrlMask) && commandPalette is not null)
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
