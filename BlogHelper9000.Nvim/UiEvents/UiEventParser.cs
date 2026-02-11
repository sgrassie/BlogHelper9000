using Microsoft.Extensions.Logging;

namespace BlogHelper9000.Nvim.UiEvents;

/// <summary>
/// Parses Neovim "redraw" notification parameters into strongly-typed UI events.
/// Neovim batches UI updates: redraw notification contains an array of event groups,
/// each group is [event_name, ...event_args].
/// </summary>
internal static class UiEventParser
{
    public static IEnumerable<NvimUiEvent> Parse(object?[] redrawArgs, ILogger logger)
    {
        foreach (var group in redrawArgs)
        {
            if (group is not object?[] eventGroup || eventGroup.Length < 1)
                continue;

            var eventName = eventGroup[0]?.ToString() ?? "";

            // Each event group can contain multiple instances: [name, args1, args2, ...]
            for (var i = 1; i < eventGroup.Length; i++)
            {
                if (eventGroup[i] is not object?[] eventArgs)
                    continue;

                var parsed = ParseSingleEvent(eventName, eventArgs, logger);
                if (parsed is not null)
                    yield return parsed;
            }
        }
    }

    private static NvimUiEvent? ParseSingleEvent(string name, object?[] args, ILogger logger)
    {
        try
        {
            return name switch
            {
                "grid_line" => ParseGridLine(args),
                "grid_cursor_goto" => new GridCursorGotoEvent(
                    ToInt(args[0]), ToInt(args[1]), ToInt(args[2])),
                "grid_scroll" => new GridScrollEvent(
                    ToInt(args[0]), ToInt(args[1]), ToInt(args[2]),
                    ToInt(args[3]), ToInt(args[4]), ToInt(args[5]), ToInt(args[6])),
                "grid_resize" => new GridResizeEvent(
                    ToInt(args[0]), ToInt(args[1]), ToInt(args[2])),
                "grid_clear" => new GridClearEvent(ToInt(args[0])),
                "flush" => new FlushEvent(),
                "hl_attr_define" => ParseHlAttrDefine(args),
                "default_colors_set" => new DefaultColorsSetEvent(
                    ToInt(args[0]), ToInt(args[1]), ToInt(args[2])),
                "mode_change" => new ModeChangeEvent(
                    args[0]?.ToString() ?? "", ToInt(args[1])),
                "mode_info_set" => ParseModeInfoSet(args),
                _ => null // Ignore unhandled events
            };
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to parse UI event '{EventName}'", name);
            return null;
        }
    }

    private static GridLineEvent ParseGridLine(object?[] args)
    {
        var grid = ToInt(args[0]);
        var row = ToInt(args[1]);
        var colStart = ToInt(args[2]);
        var cellsRaw = args[3] as object?[] ?? [];

        var cells = new List<GridLineCell>();
        foreach (var cellObj in cellsRaw)
        {
            if (cellObj is not object?[] cell) continue;

            var text = cell[0]?.ToString() ?? " ";
            int? hlId = cell.Length > 1 && cell[1] is not null ? ToInt(cell[1]) : null;
            var repeat = cell.Length > 2 ? ToInt(cell[2]) : 1;
            cells.Add(new GridLineCell(text, hlId, repeat));
        }

        return new GridLineEvent(grid, row, colStart, cells.ToArray());
    }

    private static HlAttrDefineEvent ParseHlAttrDefine(object?[] args)
    {
        var id = ToInt(args[0]);
        var attrsMap = args[1] as Dictionary<object, object> ?? new();

        var attrs = new HlAttrs
        {
            Foreground = GetOptionalInt(attrsMap, "foreground"),
            Background = GetOptionalInt(attrsMap, "background"),
            Special = GetOptionalInt(attrsMap, "special"),
            Bold = GetBool(attrsMap, "bold"),
            Italic = GetBool(attrsMap, "italic"),
            Underline = GetBool(attrsMap, "underline"),
            Strikethrough = GetBool(attrsMap, "strikethrough"),
            Reverse = GetBool(attrsMap, "reverse"),
        };

        return new HlAttrDefineEvent(id, attrs);
    }

    private static int ToInt(object? value) => Convert.ToInt32(value);

    private static int? GetOptionalInt(Dictionary<object, object> map, string key) =>
        map.TryGetValue(key, out var val) ? Convert.ToInt32(val) : null;

    private static bool GetBool(Dictionary<object, object> map, string key) =>
        map.TryGetValue(key, out var val) && Convert.ToBoolean(val);

    private static string GetString(Dictionary<object, object> map, string key, string defaultValue) =>
        map.TryGetValue(key, out var val) ? val?.ToString() ?? defaultValue : defaultValue;

    private static ModeInfoSetEvent ParseModeInfoSet(object?[] args)
    {
        var cursorStyleEnabled = Convert.ToBoolean(args[0]);
        var modeInfoArray = args[1] as object?[] ?? [];
        var modes = new List<ModeInfo>();
        foreach (var modeObj in modeInfoArray)
        {
            if (modeObj is not Dictionary<object, object> map) continue;
            modes.Add(new ModeInfo
            {
                CursorShape = GetString(map, "cursor_shape", "block"),
                CellPercentage = GetOptionalInt(map, "cell_percentage") ?? 100,
                AttrId = GetOptionalInt(map, "attr_id") ?? 0,
                Name = GetString(map, "name", ""),
                ShortName = GetString(map, "short_name", ""),
                BlinkWait = GetOptionalInt(map, "blinkwait") ?? 0,
                BlinkOn = GetOptionalInt(map, "blinkon") ?? 0,
                BlinkOff = GetOptionalInt(map, "blinkoff") ?? 0,
            });
        }
        return new ModeInfoSetEvent(cursorStyleEnabled, modes.ToArray());
    }
}
