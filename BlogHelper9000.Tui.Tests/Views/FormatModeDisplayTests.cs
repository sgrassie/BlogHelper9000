using BlogHelper9000.Tui.Views;
using FluentAssertions;

namespace BlogHelper9000.Tui.Tests.Views;

public class FormatModeDisplayTests
{
    [Theory]
    [InlineData("normal", "NORMAL")]
    [InlineData("insert", "INSERT")]
    [InlineData("visual", "VISUAL")]
    [InlineData("visual line", "V-LINE")]
    [InlineData("visual block", "V-BLOCK")]
    [InlineData("replace", "REPLACE")]
    [InlineData("cmdline_normal", "COMMAND")]
    [InlineData("cmdline_insert", "COMMAND")]
    [InlineData("operator", "OPERATOR")]
    public void FormatModeDisplay_ReturnsExpectedFormat(string mode, string expected)
    {
        NvimEditorView.FormatModeDisplay(mode).Should().Be(expected);
    }
}
