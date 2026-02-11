using BlogHelper9000.Nvim.UiEvents;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace BlogHelper9000.Nvim.Tests.UiEvents;

public class UiEventParserTests
{
    private static readonly NullLogger Logger = NullLogger.Instance;

    [Fact]
    public void Parse_Flush_Event()
    {
        // Neovim sends: ["flush", []]
        object?[] redrawArgs = [
            new object?[] { "flush", Array.Empty<object?>() }
        ];

        var events = UiEventParser.Parse(redrawArgs, Logger).ToList();

        events.Should().ContainSingle().Which.Should().BeOfType<FlushEvent>();
    }

    [Fact]
    public void Parse_GridCursorGoto_Event()
    {
        // ["grid_cursor_goto", [1, 5, 10]]
        object?[] redrawArgs = [
            new object?[] { "grid_cursor_goto", new object?[] { 1, 5, 10 } }
        ];

        var events = UiEventParser.Parse(redrawArgs, Logger).ToList();

        events.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new GridCursorGotoEvent(1, 5, 10));
    }

    [Fact]
    public void Parse_GridResize_Event()
    {
        object?[] redrawArgs = [
            new object?[] { "grid_resize", new object?[] { 1, 80, 24 } }
        ];

        var events = UiEventParser.Parse(redrawArgs, Logger).ToList();

        events.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new GridResizeEvent(1, 80, 24));
    }

    [Fact]
    public void Parse_GridScroll_Event()
    {
        object?[] redrawArgs = [
            new object?[] { "grid_scroll", new object?[] { 1, 0, 24, 0, 80, 1, 0 } }
        ];

        var events = UiEventParser.Parse(redrawArgs, Logger).ToList();

        events.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new GridScrollEvent(1, 0, 24, 0, 80, 1, 0));
    }

    [Fact]
    public void Parse_DefaultColorsSet_Event()
    {
        object?[] redrawArgs = [
            new object?[] { "default_colors_set", new object?[] { 0xFFFFFF, 0x000000, 0xFF0000 } }
        ];

        var events = UiEventParser.Parse(redrawArgs, Logger).ToList();

        var evt = events.Should().ContainSingle().Which.Should().BeOfType<DefaultColorsSetEvent>().Which;
        evt.Foreground.Should().Be(0xFFFFFF);
        evt.Background.Should().Be(0x000000);
        evt.Special.Should().Be(0xFF0000);
    }

    [Fact]
    public void Parse_ModeChange_Event()
    {
        object?[] redrawArgs = [
            new object?[] { "mode_change", new object?[] { "insert", 1 } }
        ];

        var events = UiEventParser.Parse(redrawArgs, Logger).ToList();

        var evt = events.Should().ContainSingle().Which.Should().BeOfType<ModeChangeEvent>().Which;
        evt.Mode.Should().Be("insert");
        evt.ModeIndex.Should().Be(1);
    }

    [Fact]
    public void Parse_GridLine_Event()
    {
        // ["grid_line", [1, 0, 0, [["H", 1], ["e"], ["l"], ["l"], ["o"]]]]
        object?[] redrawArgs = [
            new object?[] {
                "grid_line",
                new object?[] {
                    1, 0, 0,
                    new object?[] {
                        new object?[] { "H", 1 },
                        new object?[] { "e" },
                        new object?[] { "l" },
                        new object?[] { "l" },
                        new object?[] { "o" },
                    }
                }
            }
        ];

        var events = UiEventParser.Parse(redrawArgs, Logger).ToList();

        var evt = events.Should().ContainSingle().Which.Should().BeOfType<GridLineEvent>().Which;
        evt.Grid.Should().Be(1);
        evt.Row.Should().Be(0);
        evt.ColStart.Should().Be(0);
        evt.Cells.Should().HaveCount(5);
        evt.Cells[0].Text.Should().Be("H");
        evt.Cells[0].HlId.Should().Be(1);
        evt.Cells[1].HlId.Should().BeNull(); // inherited
    }

    [Fact]
    public void Parse_GridLine_Event_With_Repeat()
    {
        object?[] redrawArgs = [
            new object?[] {
                "grid_line",
                new object?[] {
                    1, 0, 0,
                    new object?[] {
                        new object?[] { " ", 0, 80 },
                    }
                }
            }
        ];

        var events = UiEventParser.Parse(redrawArgs, Logger).ToList();

        var evt = events.Should().ContainSingle().Which.Should().BeOfType<GridLineEvent>().Which;
        evt.Cells.Should().HaveCount(1);
        evt.Cells[0].Repeat.Should().Be(80);
    }

    [Fact]
    public void Parse_Multiple_Event_Groups_In_Single_Redraw()
    {
        object?[] redrawArgs = [
            new object?[] { "grid_cursor_goto", new object?[] { 1, 0, 0 } },
            new object?[] { "flush", Array.Empty<object?>() }
        ];

        var events = UiEventParser.Parse(redrawArgs, Logger).ToList();

        events.Should().HaveCount(2);
        events[0].Should().BeOfType<GridCursorGotoEvent>();
        events[1].Should().BeOfType<FlushEvent>();
    }

    [Fact]
    public void Parse_HlAttrDefine_Event()
    {
        var attrs = new Dictionary<object, object>
        {
            { "foreground", 0xFFFFFF },
            { "bold", true },
        };

        object?[] redrawArgs = [
            new object?[] { "hl_attr_define", new object?[] { 1, attrs, attrs, Array.Empty<object>() } }
        ];

        var events = UiEventParser.Parse(redrawArgs, Logger).ToList();

        var evt = events.Should().ContainSingle().Which.Should().BeOfType<HlAttrDefineEvent>().Which;
        evt.Id.Should().Be(1);
        evt.Attrs.Foreground.Should().Be(0xFFFFFF);
        evt.Attrs.Bold.Should().BeTrue();
    }

    [Fact]
    public void Parse_ModeInfoSet_Event()
    {
        var normalMode = new Dictionary<object, object>
        {
            { "cursor_shape", "block" },
            { "cell_percentage", 0 },
            { "attr_id", 0 },
            { "name", "normal" },
            { "short_name", "n" },
            { "blinkwait", 0 },
            { "blinkon", 0 },
            { "blinkoff", 0 },
        };
        var insertMode = new Dictionary<object, object>
        {
            { "cursor_shape", "vertical" },
            { "cell_percentage", 25 },
            { "attr_id", 1 },
            { "name", "insert" },
            { "short_name", "i" },
            { "blinkwait", 700 },
            { "blinkon", 400 },
            { "blinkoff", 250 },
        };
        var replaceMode = new Dictionary<object, object>
        {
            { "cursor_shape", "horizontal" },
            { "cell_percentage", 20 },
            { "attr_id", 2 },
            { "name", "replace" },
            { "short_name", "r" },
            { "blinkwait", 0 },
            { "blinkon", 0 },
            { "blinkoff", 0 },
        };

        object?[] redrawArgs = [
            new object?[] {
                "mode_info_set",
                new object?[] { true, new object?[] { normalMode, insertMode, replaceMode } }
            }
        ];

        var events = UiEventParser.Parse(redrawArgs, Logger).ToList();

        var evt = events.Should().ContainSingle().Which.Should().BeOfType<ModeInfoSetEvent>().Which;
        evt.CursorStyleEnabled.Should().BeTrue();
        evt.ModeInfo.Should().HaveCount(3);

        evt.ModeInfo[0].CursorShape.Should().Be("block");
        evt.ModeInfo[0].Name.Should().Be("normal");
        evt.ModeInfo[0].ShortName.Should().Be("n");
        evt.ModeInfo[0].BlinkWait.Should().Be(0);

        evt.ModeInfo[1].CursorShape.Should().Be("vertical");
        evt.ModeInfo[1].CellPercentage.Should().Be(25);
        evt.ModeInfo[1].AttrId.Should().Be(1);
        evt.ModeInfo[1].Name.Should().Be("insert");
        evt.ModeInfo[1].BlinkWait.Should().Be(700);
        evt.ModeInfo[1].BlinkOn.Should().Be(400);
        evt.ModeInfo[1].BlinkOff.Should().Be(250);

        evt.ModeInfo[2].CursorShape.Should().Be("horizontal");
        evt.ModeInfo[2].Name.Should().Be("replace");
    }

    [Fact]
    public void Parse_ModeInfoSet_WithMissingKeys_UsesDefaults()
    {
        var sparseMode = new Dictionary<object, object>
        {
            { "name", "normal" },
        };

        object?[] redrawArgs = [
            new object?[] {
                "mode_info_set",
                new object?[] { false, new object?[] { sparseMode } }
            }
        ];

        var events = UiEventParser.Parse(redrawArgs, Logger).ToList();

        var evt = events.Should().ContainSingle().Which.Should().BeOfType<ModeInfoSetEvent>().Which;
        evt.CursorStyleEnabled.Should().BeFalse();
        evt.ModeInfo.Should().HaveCount(1);

        var mode = evt.ModeInfo[0];
        mode.CursorShape.Should().Be("block");
        mode.CellPercentage.Should().Be(100);
        mode.AttrId.Should().Be(0);
        mode.Name.Should().Be("normal");
        mode.ShortName.Should().Be("");
        mode.BlinkWait.Should().Be(0);
        mode.BlinkOn.Should().Be(0);
        mode.BlinkOff.Should().Be(0);
    }

    [Fact]
    public void Parse_Unknown_Event_Returns_Empty()
    {
        object?[] redrawArgs = [
            new object?[] { "some_unknown_event", new object?[] { 1, 2, 3 } }
        ];

        var events = UiEventParser.Parse(redrawArgs, Logger).ToList();

        events.Should().BeEmpty();
    }
}
