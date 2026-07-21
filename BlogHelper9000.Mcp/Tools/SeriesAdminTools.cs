using BlogHelper9000.Core.Scheduling;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace BlogHelper9000.Mcp.Tools;

[McpServerToolType]
public static class SeriesAdminTools
{
    [McpServerTool(Name = "rename_series", Title = "Rename a series", UseStructuredContent = true, ReadOnly = false, Destructive = true, Idempotent = true),
     Description("Renames a schedule series. A pure casing change of the same series is allowed.")]
    public static ToolResponse<RenameSeriesResult> RenameSeries(
        IScheduleService scheduleService,
        [Description("The series' current name.")] string oldName,
        [Description("The new name.")] string newName)
    {
        if (!scheduleService.DatabaseExists)
            return ToolResponse<RenameSeriesResult>.Fail(ListSeriesTool.NoDatabaseError);

        if (string.IsNullOrWhiteSpace(oldName))
            return ToolResponse<RenameSeriesResult>.Fail("oldName is required and cannot be blank.");

        if (string.IsNullOrWhiteSpace(newName))
            return ToolResponse<RenameSeriesResult>.Fail("newName is required and cannot be blank.");

        var outcome = scheduleService.RenameSeries(oldName, newName);
        return outcome switch
        {
            RenameSeriesOutcome.Renamed => ToolResponse<RenameSeriesResult>.Ok(new RenameSeriesResult(oldName, newName)),
            RenameSeriesOutcome.NotFound => ToolResponse<RenameSeriesResult>.Fail(
                $"No series named '{oldName}'. Call list_series for valid names."),
            RenameSeriesOutcome.NameTaken => ToolResponse<RenameSeriesResult>.Fail(
                $"A series named '{newName}' already exists."),
            _ => ToolResponse<RenameSeriesResult>.Fail("Unknown outcome.")
        };
    }

    [McpServerTool(Name = "delete_series", Title = "Delete a series", UseStructuredContent = true, ReadOnly = false, Destructive = true, Idempotent = true),
     Description("Deletes a series, but only when it has no entries — this never cascade-deletes schedule entries. " +
                 "Defaults to a dry run that reports what would happen without changing anything.")]
    public static ToolResponse<DeleteSeriesResult> DeleteSeries(
        IScheduleService scheduleService,
        [Description("The series name.")] string name,
        [Description("Preview the deletion without changing anything. Defaults to true — set false to apply.")] bool dryRun = true)
    {
        if (!scheduleService.DatabaseExists)
            return ToolResponse<DeleteSeriesResult>.Fail(ListSeriesTool.NoDatabaseError);

        var exists = scheduleService.ListSeries()
            .Any(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));
        if (!exists)
            return ToolResponse<DeleteSeriesResult>.Fail($"No series named '{name}'. Call list_series for valid names.");

        var entryCount = scheduleService.GetSeriesEntries(name).Count;
        if (entryCount > 0)
            return ToolResponse<DeleteSeriesResult>.Fail(
                $"'{name}' still has {entryCount} entr{(entryCount == 1 ? "y" : "ies")} — remove or move them first " +
                "(see remove_schedule_entry / update_schedule_entry) before deleting the series.");

        if (dryRun)
            return ToolResponse<DeleteSeriesResult>.Ok(new DeleteSeriesResult(name, true, false));

        var outcome = scheduleService.DeleteSeries(name);
        return outcome switch
        {
            DeleteSeriesOutcome.Deleted => ToolResponse<DeleteSeriesResult>.Ok(new DeleteSeriesResult(name, false, true)),
            DeleteSeriesOutcome.NotFound => ToolResponse<DeleteSeriesResult>.Fail(
                $"No series named '{name}'. Call list_series for valid names."),
            DeleteSeriesOutcome.NotEmpty => ToolResponse<DeleteSeriesResult>.Fail(
                $"'{name}' still has entries — remove or move them first before deleting the series."),
            _ => ToolResponse<DeleteSeriesResult>.Fail("Unknown outcome.")
        };
    }

    [McpServerTool(Name = "set_series_cadence", Title = "Set a series' publish cadence", UseStructuredContent = true, ReadOnly = false, Destructive = false, Idempotent = true),
     Description("Sets the weekly cadence (day of week + the date of week 1) used to project week-numbered entries " +
                 "onto calendar dates for a series. The start date must fall on the cadence day.")]
    public static ToolResponse<SetSeriesCadenceResult> SetSeriesCadence(
        IScheduleService scheduleService,
        [Description("The series name.")] string series,
        [Description("Day of week the cadence lands on, e.g. 'Monday'.")] string dayOfWeek,
        [Description("Date of week 1 in the cadence, yyyy-MM-dd. Must fall on dayOfWeek.")] string startDate)
    {
        if (!scheduleService.DatabaseExists)
            return ToolResponse<SetSeriesCadenceResult>.Fail(ListSeriesTool.NoDatabaseError);

        if (!Enum.TryParse<DayOfWeek>(dayOfWeek, ignoreCase: true, out var day))
            return ToolResponse<SetSeriesCadenceResult>.Fail(
                $"'{dayOfWeek}' is not a valid day of week. Use one of: {string.Join(", ", Enum.GetNames<DayOfWeek>())}.");

        if (!DateOnly.TryParse(startDate, out var start))
            return ToolResponse<SetSeriesCadenceResult>.Fail($"'{startDate}' is not a valid yyyy-MM-dd date.");

        var outcome = scheduleService.SetSeriesCadence(series, day, start);
        return outcome switch
        {
            SetCadenceOutcome.Set => ToolResponse<SetSeriesCadenceResult>.Ok(
                new SetSeriesCadenceResult(series, day.ToString(), start.ToString("yyyy-MM-dd"))),
            SetCadenceOutcome.SeriesNotFound => ToolResponse<SetSeriesCadenceResult>.Fail(
                $"No series named '{series}'. Call list_series for valid names."),
            SetCadenceOutcome.DayMismatch => ToolResponse<SetSeriesCadenceResult>.Fail(
                $"'{startDate}' falls on a {start.DayOfWeek}, not {day} — the cadence start date must fall on the cadence day."),
            _ => ToolResponse<SetSeriesCadenceResult>.Fail("Unknown outcome.")
        };
    }
}
