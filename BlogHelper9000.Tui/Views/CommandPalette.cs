using System.Collections.ObjectModel;
using BlogHelper9000.Core.Services;
using Microsoft.Extensions.Logging;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace BlogHelper9000.Tui.Views;

/// <summary>
/// Modal command palette triggered by Ctrl+P.
/// Provides filtered access to blog operations.
/// </summary>
public class CommandPalette
{
    private readonly IBlogService _blogService;
    private readonly NvimEditorView _nvimEditor;
    private readonly ILogger _logger;

    public CommandPalette(IBlogService blogService, NvimEditorView nvimEditor, ILogger logger)
    {
        _blogService = blogService;
        _nvimEditor = nvimEditor;
        _logger = logger;
    }

    private static readonly CommandEntry[] AllCommands =
    [
        new("New Draft", "Create a new draft post"),
        new("New Post", "Create a new published post"),
        new("Publish Draft", "Publish an existing draft"),
        new("Blog Info", "Show blog statistics"),
        new("Fix Metadata: Status", "Fix published status on all posts"),
        new("Fix Metadata: Description", "Migrate legacy descriptions"),
        new("Fix Metadata: Tags", "Fix and normalize tags"),
    ];

    public void Show()
    {
        var dialog = new Dialog
        {
            Title = "Command Palette",
            Width = Dim.Percent(60),
            Height = Dim.Percent(50),
        };

        var filterField = new TextField
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Text = "",
        };

        var commandList = new ListView
        {
            X = 0,
            Y = Pos.Bottom(filterField) + 1,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
        };

        var displayedCommands = AllCommands.ToList();
        var items = new ObservableCollection<string>(displayedCommands.Select(c => $"{c.Name}  —  {c.Description}"));
        commandList.SetSource(items);

        filterField.TextChanged += (_, _) =>
        {
            var filter = filterField.Text?.Trim() ?? "";
            displayedCommands = string.IsNullOrEmpty(filter)
                ? AllCommands.ToList()
                : AllCommands.Where(c =>
                    c.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                    c.Description.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();

            items.Clear();
            foreach (var cmd in displayedCommands)
                items.Add($"{cmd.Name}  —  {cmd.Description}");
        };

        commandList.Accepted += (_, e) =>
        {
            var selected = commandList.SelectedItem;
            if (selected is null || selected < 0 || selected >= displayedCommands.Count)
                return;

            var command = displayedCommands[selected.Value];
            dialog.RequestStop();
            ExecuteCommand(command.Name);
        };

        // Esc closes the dialog
        dialog.KeyDown += (_, e) =>
        {
            if (e.KeyCode == KeyCode.Esc)
            {
                dialog.RequestStop();
                e.Handled = true;
            }
        };

        dialog.Add(filterField, commandList);
        filterField.SetFocus();

        Application.Run(dialog);
    }

    private void ExecuteCommand(string commandName)
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
                    break;
                case "Fix Metadata: Description":
                    _blogService.FixMetadata(fixStatus: false, fixDescription: true, fixTags: false);
                    break;
                case "Fix Metadata: Tags":
                    _blogService.FixMetadata(fixStatus: false, fixDescription: false, fixTags: true);
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
            Height = 7,
        };

        var label = new Label { Text = "Title:", X = 0, Y = 0 };
        var titleField = new TextField { X = Pos.Right(label) + 1, Y = 0, Width = Dim.Fill() };

        titleField.Accepted += (_, _) =>
        {
            var title = titleField.Text?.Trim();
            if (string.IsNullOrEmpty(title)) return;

            dialog.RequestStop();
            var path = _blogService.AddPost(title, isDraft);
            _logger.LogInformation("Created {Type}: {Path}", isDraft ? "draft" : "post", path);

            _ = Task.Run(async () =>
            {
                try { await _nvimEditor.OpenFileAsync(path); }
                catch (Exception ex) { _logger.LogError(ex, "Failed to open new file"); }
            });
        };

        dialog.KeyDown += (_, e) =>
        {
            if (e.KeyCode == KeyCode.Esc)
            {
                dialog.RequestStop();
                e.Handled = true;
            }
        };

        dialog.Add(label, titleField);
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

        var items = new ObservableCollection<string>(drafts);
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
                _logger.LogInformation("Published: {Path}", result);
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

    private record CommandEntry(string Name, string Description);
}
