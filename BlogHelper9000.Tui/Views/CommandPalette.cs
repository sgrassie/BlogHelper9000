using System.Collections.ObjectModel;
using BlogHelper9000.Tui.Commands;
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
    private readonly BlogCommands _commands;

    public CommandPalette(BlogCommands commands)
    {
        _commands = commands;
    }

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

        var allCommands = BlogCommands.AllCommands;
        var displayedCommands = allCommands.ToList();
        var items = new ObservableCollection<string>(displayedCommands.Select(c => $"{c.Name}  —  {c.Description}"));
        commandList.SetSource(items);

        filterField.TextChanged += (_, _) =>
        {
            var filter = filterField.Text?.Trim() ?? "";
            displayedCommands = string.IsNullOrEmpty(filter)
                ? allCommands.ToList()
                : allCommands.Where(c =>
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
            _commands.ExecuteCommand(command.Name);
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
}
