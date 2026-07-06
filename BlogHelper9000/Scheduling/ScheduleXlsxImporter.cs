using System.Data;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using BlogHelper9000.Core.Scheduling;
using ExcelDataReader;

namespace BlogHelper9000.Scheduling;

public sealed record ImportSummary(IReadOnlyList<(string Series, int Entries)> SeriesCounts);

/// <summary>
/// Reads the publishing-schedule spreadsheet with ExcelDataReader. ClosedXML cannot be
/// used here: it requires SixLabors.Fonts 1.x while BlogHelper9000.Imaging pins 3.x,
/// and loading a workbook under the unified version throws TypeLoadException.
/// </summary>
public static partial class ScheduleXlsxImporter
{
    private static readonly string[] SkippedSheets = ["Dashboard", "Progress"];

    [GeneratedRegex(@"\(baseline,\s*([^)]+)\)")]
    private static partial Regex BaselineDatePattern();

    public static ImportSummary Import(Stream xlsxStream, ScheduleDatabase database)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        using var reader = ExcelReaderFactory.CreateReader(xlsxStream);
        var workbook = reader.AsDataSet();

        var repository = new ScheduleRepository(database);
        var seriesNameBySheet = ReadDashboard(workbook, repository);

        var counts = new List<(string, int)>();
        var sortOrder = 0;
        foreach (DataTable sheet in workbook.Tables)
        {
            if (SkippedSheets.Contains(sheet.TableName)) continue;

            var seriesName = seriesNameBySheet.GetValueOrDefault(sheet.TableName, sheet.TableName);
            var seriesId = repository.AddSeries(seriesName, sortOrder++);
            counts.Add((seriesName, ImportSheet(sheet, repository, seriesId)));
        }
        return new ImportSummary(counts);
    }

    private static Dictionary<string, string> ReadDashboard(DataSet workbook, ScheduleRepository repository)
    {
        var map = new Dictionary<string, string>();
        if (!workbook.Tables.Contains("Dashboard")) return map;

        var sheetNames = workbook.Tables.Cast<DataTable>().Select(t => t.TableName).ToHashSet();
        var dashboard = workbook.Tables["Dashboard"]!;
        foreach (DataRow row in dashboard.Rows)
        {
            var label = GetString(row, 0) ?? string.Empty;

            if (label.StartsWith("Published posts (baseline", StringComparison.Ordinal))
            {
                if (GetInt(row, 1) is { } baseline)
                    repository.SetMeta("baseline_published_count",
                        baseline.ToString(CultureInfo.InvariantCulture));

                var match = BaselineDatePattern().Match(label);
                if (match.Success && DateTime.TryParse(match.Groups[1].Value.Trim(),
                        CultureInfo.GetCultureInfo("en-GB"), out var baselineDate))
                    repository.SetMeta("baseline_date", baselineDate.ToString("yyyy-MM-dd"));
            }
            else if (label.Length > 0 && GetString(row, 1) is { } sheetName && sheetNames.Contains(sheetName))
            {
                map[sheetName] = label;
            }
        }
        return map;
    }

    private static int ImportSheet(DataTable sheet, ScheduleRepository repository, long seriesId)
    {
        if (sheet.Rows.Count == 0)
            throw new InvalidOperationException($"Sheet '{sheet.TableName}' has no header row.");

        var columns = new Dictionary<string, int>();
        var header = sheet.Rows[0];
        for (var i = 0; i < sheet.Columns.Count; i++)
        {
            if (GetString(header, i) is { } name)
                columns[name.Trim()] = i;
        }

        if (!columns.TryGetValue("Post title", out var titleColumn) ||
            !columns.ContainsKey("Draft filename"))
            throw new InvalidOperationException(
                $"Sheet '{sheet.TableName}' is missing the 'Post title'/'Draft filename' header columns.");

        var count = 0;
        for (var rowNumber = 1; rowNumber < sheet.Rows.Count; rowNumber++)
        {
            var row = sheet.Rows[rowNumber];
            var title = GetString(row, titleColumn);
            if (title is null) break;

            repository.AddEntry(seriesId, new NewScheduleEntry(
                Position: GetInt(row, columns, "#"),
                Week: GetInt(row, columns, "Week"),
                PublishDate: GetDate(row, columns, "Publish date"),
                Topic: GetString(row, columns, "Series"),
                Title: title,
                DraftFilename: GetString(row, columns, "Draft filename")
                    ?? throw new InvalidOperationException(
                        $"'{sheet.TableName}' row {rowNumber + 1}: 'Draft filename' is empty for '{title}'."),
                Tags: GetString(row, columns, "Tags"),
                Source: GetString(row, columns, "Source"),
                Published: GetBool(row, columns, "Published?"),
                Notes: GetString(row, columns, "Notes")));
            count++;
        }
        return count;
    }

    private static object? Cell(DataRow row, int column) =>
        column < row.Table.Columns.Count && row[column] is not DBNull ? row[column] : null;

    private static object? Cell(DataRow row, Dictionary<string, int> columns, string name) =>
        columns.TryGetValue(name, out var column) ? Cell(row, column) : null;

    private static string? GetString(DataRow row, int column)
    {
        var text = Cell(row, column)?.ToString()?.Trim();
        return string.IsNullOrEmpty(text) ? null : text;
    }

    private static string? GetString(DataRow row, Dictionary<string, int> columns, string name) =>
        columns.TryGetValue(name, out var column) ? GetString(row, column) : null;

    private static int? GetInt(DataRow row, int column) =>
        Cell(row, column) is { } value ? Convert.ToInt32(value, CultureInfo.InvariantCulture) : null;

    private static int? GetInt(DataRow row, Dictionary<string, int> columns, string name) =>
        columns.TryGetValue(name, out var column) ? GetInt(row, column) : null;

    private static DateOnly? GetDate(DataRow row, Dictionary<string, int> columns, string name) =>
        Cell(row, columns, name) is DateTime value ? DateOnly.FromDateTime(value) : null;

    private static bool GetBool(DataRow row, Dictionary<string, int> columns, string name) =>
        Cell(row, columns, name) is true;
}
