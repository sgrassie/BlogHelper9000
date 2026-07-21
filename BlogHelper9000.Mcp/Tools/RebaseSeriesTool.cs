using BlogHelper9000.Core.Scheduling;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace BlogHelper9000.Mcp.Tools;

[McpServerToolType]
public static class RebaseSeriesTool
{
    [McpServerTool(Name = "rebase_series", Title = "Rebase a series' schedule", UseStructuredContent = true, ReadOnly = false, Destructive = true, Idempotent = false),
     Description("Pushes back (or pulls forward) a slipped series' whole schedule in one atomic operation: moves the " +
                 "series' first unpublished entry to a new start date and shifts every later unpublished entry by the " +
                 "same delta, preserving relative spacing. Published entries and entries without a publish date are " +
                 "never touched. Supply newStartDate for an absolute rebase, days/weeks for a relative shift, or " +
                 "nothing to default to the next occurrence of the series' weekday after today.")]
    public static ToolResponse<RebaseSeriesToolResult> RebaseSeries(
        IScheduleService scheduleService,
        [Description("The series to rebase, e.g. 'FootballData'.")] string series,
        [Description("The new start date for the first unpublished entry, yyyy-MM-dd. Mutually exclusive with days/weeks.")] string? newStartDate = null,
        [Description("Shift every unpublished entry by this many days (negative pulls forward). Mutually exclusive with newStartDate and weeks.")] int? days = null,
        [Description("Shift every unpublished entry by this many weeks (negative pulls forward). Mutually exclusive with newStartDate and days.")] int? weeks = null)
    {
        if (!scheduleService.DatabaseExists)
            return ToolResponse<RebaseSeriesToolResult>.Fail(ListSeriesTool.NoDatabaseError);

        if (newStartDate is not null && (days is not null || weeks is not null))
            return ToolResponse<RebaseSeriesToolResult>.Fail(
                "newStartDate and days/weeks are contradictory — rebase to a date or shift by a delta, not both.");

        if (days is not null && weeks is not null)
            return ToolResponse<RebaseSeriesToolResult>.Fail(
                "days and weeks are contradictory — supply one delta, not both.");

        if (days is 0 || weeks is 0)
            return ToolResponse<RebaseSeriesToolResult>.Fail("The shift delta must be non-zero.");

        DateOnly? date = null;
        if (newStartDate is not null)
        {
            if (!DateOnly.TryParse(newStartDate, out var parsed))
                return ToolResponse<RebaseSeriesToolResult>.Fail($"'{newStartDate}' is not a valid yyyy-MM-dd date.");
            date = parsed;
        }

        var result = days is not null || weeks is not null
            ? scheduleService.ShiftSeries(series, days ?? weeks!.Value * 7)
            : scheduleService.RebaseSeries(series, date);

        return result.Outcome switch
        {
            RebaseSeriesOutcome.Rebased => ToolResponse<RebaseSeriesToolResult>.Ok(
                new RebaseSeriesToolResult(series, result.EntriesMoved,
                    result.OldStartDate!.Value, result.NewStartDate!.Value)),
            RebaseSeriesOutcome.SeriesNotFound => ToolResponse<RebaseSeriesToolResult>.Fail(
                $"No series named '{series}'. Call list_series for valid names."),
            RebaseSeriesOutcome.NothingToMove => ToolResponse<RebaseSeriesToolResult>.Fail(
                $"'{series}' has no unpublished entries with a publish date — nothing to rebase."),
            _ => ToolResponse<RebaseSeriesToolResult>.Fail("Unknown outcome.")
        };
    }
}
