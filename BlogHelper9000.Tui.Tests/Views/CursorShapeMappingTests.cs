using BlogHelper9000.Nvim.UiEvents;
using BlogHelper9000.Tui.Views;
using FluentAssertions;
using Terminal.Gui.Drivers;

namespace BlogHelper9000.Tui.Tests.Views;

public class CursorShapeMappingTests
{
    [Theory]
    [InlineData("block", 0, 0, CursorStyle.SteadyBlock)]
    [InlineData("block", 700, 400, CursorStyle.BlinkingBlock)]
    [InlineData("vertical", 0, 0, CursorStyle.SteadyBar)]
    [InlineData("vertical", 700, 400, CursorStyle.BlinkingBar)]
    [InlineData("horizontal", 0, 0, CursorStyle.SteadyUnderline)]
    [InlineData("horizontal", 700, 400, CursorStyle.BlinkingUnderline)]
    public void MapCursorShape_ReturnsCorrectStyle(string shape, int blinkWait, int blinkOn, CursorStyle expected)
    {
        var modeInfo = new ModeInfo
        {
            CursorShape = shape,
            BlinkWait = blinkWait,
            BlinkOn = blinkOn,
        };

        var result = NvimEditorView.MapCursorShape(modeInfo);

        result.Should().Be(expected);
    }

    [Fact]
    public void MapCursorShape_UnknownShape_FallsBackToSteadyBlock()
    {
        var modeInfo = new ModeInfo { CursorShape = "unknown" };

        var result = NvimEditorView.MapCursorShape(modeInfo);

        result.Should().Be(CursorStyle.SteadyBlock);
    }

    [Fact]
    public void MapCursorShape_BlinkWaitOnly_CountsAsBlink()
    {
        var modeInfo = new ModeInfo
        {
            CursorShape = "block",
            BlinkWait = 700,
            BlinkOn = 0,
        };

        var result = NvimEditorView.MapCursorShape(modeInfo);

        result.Should().Be(CursorStyle.BlinkingBlock);
    }
}
