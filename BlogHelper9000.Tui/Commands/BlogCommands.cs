using System.IO.Abstractions;
using BlogHelper9000.Core.Services;
using Microsoft.Extensions.Logging;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace BlogHelper9000.Tui.Commands;

/// <summary>
/// Shared blog command logic used by both the CommandPalette and the MenuBar.
/// </summary>
public class BlogCommands
{
    private readonly IBlogService _blogService;
    private readonly IFileSystem _fileSystem;
    private readonly ILogger<BlogCommands> _logger;

    public BlogCommands(IBlogService blogService, IFileSystem fileSystem, ILogger<BlogCommands> logger)
    {
        _blogService = blogService;
        _fileSystem = fileSystem;
        _logger = logger;
    }

    /// <summary>
    /// Called to open a file in the active editor (Neovim or fallback).
    /// Set by the host after construction.
    /// </summary>
    public Action<string>? OpenFileCallback { get; set; }

    /// <summary>
    /// Called after operations that change files (e.g. publish) so the file browser can refresh.
    /// Set by the host after construction.
    /// </summary>
    public Action? FilesChangedCallback { get; set; }

    /// <summary>
    /// Returns the currently selected file path in the file browser, or null if none.
    /// Set by the host after construction.
    /// </summary>
    public Func<string?>? GetSelectedFilePathCallback { get; set; }

    public record CommandEntry(string Name, string Description);

    public static readonly CommandEntry[] AllCommands =
    [
        new("New Draft", "Create a new draft post"),
        new("New Post", "Create a new published post"),
        new("Publish Draft", "Publish an existing draft"),
        new("Blog Info", "Show blog statistics"),
        new("Fix Metadata: Status", "Fix published status on all posts"),
        new("Fix Metadata: Description", "Migrate legacy descriptions"),
        new("Fix Metadata: Tags", "Fix and normalize tags"),
        new("Delete File", "Delete the selected file"),
    ];

    public void ExecuteCommand(string commandName)
    {
        try
        {
            switch (commandName)
            {
                case "New Draft":
                    PromptAndCreatePost(isDraft: true);
                    break;
                case "New Post":
                    PromptAndCreatePost(isDraft: false);
                    break;
                case "Publish Draft":
                    PromptAndPublishDraft();
                    break;
                case "Blog Info":
                    ShowBlogInfo();
                    break;
                case "Fix Metadata: Status":
                    _blogService.FixMetadata(fixStatus: true, fixDescription: false, fixTags: false);
                    FilesChangedCallback?.Invoke();
                    break;
                case "Fix Metadata: Description":
                    _blogService.FixMetadata(fixStatus: false, fixDescription: true, fixTags: false);
                    FilesChangedCallback?.Invoke();
                    break;
                case "Fix Metadata: Tags":
                    _blogService.FixMetadata(fixStatus: false, fixDescription: false, fixTags: true);
                    FilesChangedCallback?.Invoke();
                    break;
                case "Delete File":
                    PromptAndDeleteFile();
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing command: {Command}", commandName);
        }
    }

    private void PromptAndCreatePost(bool isDraft)
    {
        var dialog = new Dialog
        {
            Title = isDraft ? "New Draft" : "New Post",
            Width = Dim.Percent(50),
            Height = 9,
        };

        var label = new Label { Text = "Title:", X = 0, Y = 0 };
        var titleField = new TextField { X = Pos.Right(label) + 1, Y = 0, Width = Dim.Fill() };
        var createButton = new Button { Text = "Create", X = Pos.Center(), Y = 2 };

        void DoCreate()
        {
            var title = titleField.Text?.Trim();
            if (string.IsNullOrEmpty(title)) return;

            dialog.RequestStop();
            var path = _blogService.AddPost(title, isDraft);
            _logger.LogInformation("Created {Type}: {Path}", isDraft ? "draft" : "post", path);

            FilesChangedCallback?.Invoke();
            OpenFileCallback?.Invoke(path);
        }

        titleField.Accepted += (_, _) => DoCreate();
        createButton.Accepting += (_, _) => DoCreate();

        dialog.KeyDown += (_, e) =>
        {
            if (e.KeyCode == KeyCode.Esc)
            {
                dialog.RequestStop();
                e.Handled = true;
            }
        };

        dialog.Add(label, titleField, createButton);
        titleField.SetFocus();
        Application.Run(dialog);
    }

    private void PromptAndPublishDraft()
    {
        var drafts = _blogService.ListDrafts();
        if (drafts.Count == 0)
        {
            ShowMessage("No Drafts", "There are no drafts to publish.");
            return;
        }

        var dialog = new Dialog
        {
            Title = "Publish Draft",
            Width = Dim.Percent(50),
            Height = Dim.Percent(40),
        };

        var listView = new ListView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
        };

        var items = new System.Collections.ObjectModel.ObservableCollection<string>(drafts);
        listView.SetSource(items);

        listView.Accepted += (_, _) =>
        {
            var selected = listView.SelectedItem;
            if (selected is null || selected < 0 || selected >= drafts.Count)
                return;

            var draftName = drafts[selected.Value];
            dialog.RequestStop();
            var result = _blogService.PublishPost(draftName);
            if (result is not null)
            {
                _logger.LogInformation("Published: {Path}", result);
                FilesChangedCallback?.Invoke();
            }
        };

        dialog.KeyDown += (_, e) =>
        {
            if (e.KeyCode == KeyCode.Esc)
            {
                dialog.RequestStop();
                e.Handled = true;
            }
        };

        dialog.Add(listView);
        listView.SetFocus();
        Application.Run(dialog);
    }

    private void ShowBlogInfo()
    {
        var info = _blogService.GetBlogInfo();

        var lines = new List<string>
        {
            $"Total posts: {info.PostCount}",
            $"Drafts: {info.UnPublishedCount}",
            $"Days since last post: {info.DaysSinceLastPost.Days}",
        };

        if (info.LastPost is not null)
            lines.Insert(0, $"Last post: {info.LastPost.Title} ({info.LastPost.PublishedOn:yyyy-MM-dd})");

        if (info.LatestPosts is { Count: > 0 })
        {
            lines.Add("");
            lines.Add("Recent posts:");
            foreach (var post in info.LatestPosts)
                lines.Add($"  {post.PublishedOn:yyyy-MM-dd} — {post.Title}");
        }

        if (info.Unpublished?.Any() == true)
        {
            lines.Add("");
            lines.Add("Drafts:");
            foreach (var draft in info.Unpublished)
                lines.Add($"  {draft.Title}");
        }

        ShowMessage("Blog Info", string.Join("\n", lines));
    }

    private void PromptAndDeleteFile()
    {
        var path = GetSelectedFilePathCallback?.Invoke();
        if (path is null) return;

        var fileName = _fileSystem.Path.GetFileName(path);

        var dialog = new Dialog
        {
            Title = "Delete File",
            Width = Dim.Percent(40),
            Height = 7,
        };

        var label = new Label
        {
            Text = $"Delete \"{fileName}\"?",
            X = Pos.Center(),
            Y = 0,
        };

        var yesButton = new Button { Text = "Yes", X = Pos.Center() - 6, Y = 2 };
        var noButton = new Button { Text = "No", X = Pos.Center() + 2, Y = 2 };

        yesButton.Accepting += (_, _) =>
        {
            dialog.RequestStop();
            DeleteFileAt(path);
        };

        noButton.Accepting += (_, _) => dialog.RequestStop();

        dialog.KeyDown += (_, e) =>
        {
            if (e.KeyCode == KeyCode.Esc)
            {
                dialog.RequestStop();
                e.Handled = true;
            }
        };

        dialog.Add(label, yesButton, noButton);
        noButton.SetFocus();
        Application.Run(dialog);
    }

    internal void DeleteFileAt(string path)
    {
        if (!_fileSystem.File.Exists(path)) return;
        _fileSystem.File.Delete(path);
        _logger.LogInformation("Deleted file: {Path}", path);
        FilesChangedCallback?.Invoke();
    }

    private static void ShowMessage(string title, string message)
    {
        var dialog = new Dialog
        {
            Title = title,
            Width = Dim.Percent(60),
            Height = Dim.Percent(50),
        };

        var textView = new TextView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            ReadOnly = true,
            Text = message,
        };

        dialog.KeyDown += (_, e) =>
        {
            if (e.KeyCode == KeyCode.Esc || e.KeyCode == KeyCode.Enter)
            {
                dialog.RequestStop();
                e.Handled = true;
            }
        };

        dialog.Add(textView);
        Application.Run(dialog);
    }
}
